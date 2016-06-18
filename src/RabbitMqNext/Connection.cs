﻿namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using RabbitMqNext.Io;
	using RabbitMqNext.Internals;
	using Recovery;


	public sealed class Connection : IConnection
	{
		internal readonly ConnectionIO _io;

		internal const int MaxChannels = 10;
		private readonly Channel[] _channels = new Channel[MaxChannels + 1]; // 1-based index
		
		private int _channelNumbers;
		private ConnectionInfo _connectionInfo;
		private CancellationTokenSource _channelCancellationTokenSource = new CancellationTokenSource();
		private readonly List<Func<AmqpError, Task>> _errorsCallbacks = new List<Func<AmqpError, Task>>();

		public Connection()
		{
			_io = new ConnectionIO(this)
			{
				ErrorCallbacks = _errorsCallbacks
			};
		}

		public RecoveryEnabledConnection Recovery { get; internal set; }

		public void AddErrorCallback(Func<AmqpError, Task> errorCallback)
		{
			Contract.Requires(errorCallback != null);

			lock(_errorsCallbacks) _errorsCallbacks.Add(errorCallback);
		}

		public void RemoveErrorCallback(Func<AmqpError, Task> errorCallback)
		{
			Contract.Requires(errorCallback != null);

			lock (_errorsCallbacks) _errorsCallbacks.Remove(errorCallback);
		}

		internal Task<bool> Connect(string hostname, string vhost, 
									string username, string password, 
									int port, string connectionName, bool throwOnError = true)
		{
			// Saves info for reconnection scenarios
			_connectionInfo = new ConnectionInfo 
			{ 
				hostname = hostname, 
				vhost = vhost, 
				username = username, 
				password = password, 
				port = port,
				connectionName = connectionName
			};

			return InternalConnect(hostname);
		}

		internal async Task<bool> InternalConnect(string hostname, bool throwOnError = true)
		{
			if (LogAdapter.ExtendedLogEnabled)
				LogAdapter.LogDebug("Connection", "Trying to connect to " + hostname);

			var result = await _io.InternalDoConnectSocket(hostname, _connectionInfo.port, throwOnError).ConfigureAwait(false);

			if (!result) return false;

			result = await _io.Handshake(_connectionInfo.vhost, 
				_connectionInfo.username, 
				_connectionInfo.password, 
				_connectionInfo.connectionName, throwOnError).ConfigureAwait(false);

			if (result && this.Recovery != null)
			{
				this.Recovery.NotifyConnected(hostname);
			}

			return result;
		}

		public bool IsClosed { get { return _io.IsClosed; } }

		public Task<IChannel> CreateChannel(ChannelOptions options)
		{
			return InternalCreateChannel(options, null, withPubConfirm: false);
		}

		public Task<IChannel> CreateChannelWithPublishConfirmation(ChannelOptions options, int maxunconfirmedMessages = 100)
		{
			return InternalCreateChannel(options, null, maxunconfirmedMessages, withPubConfirm: true);
		}

		public void Dispose()
		{
			if (this.Recovery != null)
				this.Recovery.Dispose();

			this._io.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal ChannelIO ResolveChannel(ushort channel)
		{
			if (channel > MaxChannels)
			{
				LogAdapter.LogError("Connection", "ResolveChannel for invalid channel " + channel);
				throw new Exception("Unexpected channel number " + channel);
			}

			var channelInst = _channels[channel];
			if (channelInst == null)
			{
				LogAdapter.LogError("Connection", "ResolveChannel for non-initialized channel " + channel);
				throw new Exception("Channel not initialized " + channel);
			}

			return channelInst._io;
		}

		internal void CloseAllChannels(Exception reason)
		{
			foreach (var channel in _channels)
			{
				if (channel == null) continue;

				channel._io.InitiateAbruptClose(reason).IntentionallyNotAwaited();
			}
		}

		internal void CloseAllChannels(bool initiatedByServer, AmqpError error)
		{
			LogAdapter.LogDebug("Connection", "Closing all channels");

			foreach (var channel in _channels)
			{
				if (channel == null) continue;

#pragma warning disable 4014
				channel._io.InitiateCleanClose(initiatedByServer, error);
#pragma warning restore 4014
			}
		}

		internal void Reset()
		{
			for (int i = 0; i < MaxChannels; i++)
			{
				Interlocked.Exchange(ref _channels[i], null);
			}
		}

		internal async Task<IChannel> InternalCreateChannel(ChannelOptions options, int? desiredChannelNum, int maxunconfirmedMessages = 0, bool withPubConfirm = false)
		{
			var channelNum = desiredChannelNum.HasValue ?
				(ushort) desiredChannelNum.Value : 
				(ushort) Interlocked.Increment(ref _channelNumbers);

			if (channelNum > MaxChannels)
				throw new Exception("Exceeded channel limits");

			var channel = new Channel(options, channelNum, this._io, _channelCancellationTokenSource.Token);

			try
			{
				_channels[channelNum] = channel;
				await channel.Open().ConfigureAwait(false);
				if (withPubConfirm)
				{
					await channel.EnableConfirmation(maxunconfirmedMessages).ConfigureAwait(false);
				}
				return channel;
			}
			catch
			{
				// TODO: release channel number that wasnt used
				_channels[channelNum] = null;
				throw;
			}
		}

		internal RecoveryAction NotifyAbruptClose(Exception reason)
		{
			if (this.Recovery != null)
				return this.Recovery.NotifyAbruptClose(reason);

			return RecoveryAction.NoAction;
		}

		internal RecoveryAction NotifyClosedByServer()
		{
			if (this.Recovery != null)
				return this.Recovery.NotifyCloseByServer();

			return RecoveryAction.NoAction;
		}

		internal void NotifyClosedByUser()
		{
			if (this.Recovery != null)
				this.Recovery.NotifyCloseByUser();
		}

		internal class ConnectionInfo
		{
			internal string hostname;
			internal string vhost;
			internal string username;
			internal string password;
			internal string connectionName;
			internal int port;
		}
	}
}
