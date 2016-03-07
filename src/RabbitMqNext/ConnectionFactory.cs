﻿namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;


	public class ConnectionFactory
	{
		public async Task<IConnection> Connect(IEnumerable<string> hostnames,
			string vhost = "/", string username = "guest",
			string password = "guest", int port = 5672, 
			bool autoRecovery = true)
		{
			var conn = new Connection();

			try
			{
				foreach (var hostname in hostnames)
				{
					var successful = 
						await conn.Connect(hostname, vhost, 
										   username, password, port, 
										   throwOnError: false).ConfigureAwait(false);
					if (successful)
					{
						LogAdapter.LogWarn("ConnectionFactory", "Selected " + hostname);

						return autoRecovery ? 
							(IConnection) new AutoRecoveryEnabledConnection(hostnames, conn) : 
							conn;
					}
				}

				throw new Exception("Could not connect to any of the provided hosts");
			}
			catch (Exception e)
			{
				LogAdapter.LogError("ConnectionFactory", "Connection error", e);

				conn.Dispose();
				throw;
			}
		}

		public async Task<IConnection> Connect(string hostname, 
			string vhost = "/", string username = "guest",
			string password = "guest", int port = 5672, 
			bool autoRecovery = true)
		{
			var conn = new Connection();

			try
			{
				await conn.Connect(hostname, vhost, username, password, port, throwOnError: true).ConfigureAwait(false);

				if (LogAdapter.ExtendedLogEnabled)
					LogAdapter.LogDebug("ConnectionFactory", "Connected to " + hostname + ":" + port);

				return autoRecovery ? (IConnection)new AutoRecoveryEnabledConnection(hostname, conn) : conn;
			}
			catch (Exception e)
			{
				LogAdapter.LogError("ConnectionFactory", "Connection error: " + hostname + ":" + port, e);

				conn.Dispose();
				throw;
			}
		}
	}
}