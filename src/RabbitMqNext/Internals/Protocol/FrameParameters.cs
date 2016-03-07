namespace RabbitMqNext.Internals
{
	using System;

	internal interface IFrameContentWriter
	{
		void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg);
	}

	internal static class FrameParameters
	{
		internal class CloseParams
		{
			public ushort replyCode;
			public string replyText;

			public override string ToString()
			{
				return "CloseParams [" + replyCode + " | " + replyText + "]";
			}
		}

		internal class BasicAckArgs : IFrameContentWriter
		{
			public ulong deliveryTag;
			public bool multiple;

			public void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg)
			{
				AmqpChannelLevelFrameWriter.InternalBasicAck(amqpWriter, channel, classId, methodId, optionalArg);
			}

			public override string ToString()
			{
				return "BasicAckArgs [delivery " + deliveryTag + " | " + multiple + "]";
			}
		}

		internal class BasicNAckArgs : IFrameContentWriter
		{
			public ulong deliveryTag;
			public bool multiple;
			public bool requeue;

			public void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg)
			{
				AmqpChannelLevelFrameWriter.InternalBasicNAck(amqpWriter, channel, classId, methodId, optionalArg);
			}

			public override string ToString()
			{
				return "BasicNAckArgs [" + deliveryTag + " | " + multiple + " | " + requeue + "]";
			}
		}

		internal class BasicPublishArgs : IFrameContentWriter
		{
			private readonly Action<BasicPublishArgs> _recycler;

			public BasicPublishArgs(Action<BasicPublishArgs> recycler)
			{
				_recycler = recycler;
			}

			public override string ToString()
			{
				return "BasicPublishArgs [" + exchange + " | " + routingKey + " | " + mandatory + " | " + properties + "]";
			}

			public string exchange;
			public string routingKey;
			public bool mandatory;
			public BasicProperties properties;
			public ArraySegment<byte> buffer;

			internal void Done()
			{
				if (_recycler != null)
					_recycler(this);
			}

			public void Write(AmqpPrimitivesWriter amqpWriter, ushort channel, ushort classId, ushort methodId, object optionalArg)
			{
				// AmqpChannelLevelFrameWriter.InternalBasicPublish(amqpWriter, channel, classId, methodId, optionalArg);
				AmqpChannelLevelFrameWriter.InternalBufferedBasicPublish(amqpWriter, channel, classId, methodId, optionalArg);
			}
		}
	}
}