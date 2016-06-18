﻿namespace RabbitMqNext
{
	using System;
	using System.Threading.Tasks;
	using Internals;

	public class ChannelOptions
	{
		public TaskScheduler Scheduler { get; set; }
	}

	public interface IConnection : IDisposable
	{
		// event Action<AmqpError> OnError;
		void AddErrorCallback(Func<AmqpError, Task> errorCallback);
		void RemoveErrorCallback(Func<AmqpError, Task> errorCallback);
		
		bool IsClosed { get; }

		Task<IChannel> CreateChannel(ChannelOptions options = null);

		Task<IChannel> CreateChannelWithPublishConfirmation(ChannelOptions options = null, int maxunconfirmedMessages = 100);
	}
}