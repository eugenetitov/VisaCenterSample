﻿using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisaCenter.DomainEvents;
using VisaCenter.Interfaces.Handlers;

namespace VisaCenter.RabbitMqHandlers
{
    public class RabbitMqRPCHandler : IEventHandler<VisaStatusCheckEvent, string>
    {
        public async Task<string> HandleAsync(VisaStatusCheckEvent ev, IBus bus)
        {

            //var r =  await new Task<string>(() => {
            //});
            var rpcClient = new RpcClient();
            var response = rpcClient.Call(ev.Id.ToString());
            rpcClient.Close();
            return response;

        }

        public class RpcClient
        {
            private readonly IConnection connection;
            private readonly IModel channel;
            private readonly string replyQueueName;
            private readonly EventingBasicConsumer consumer;
            private readonly BlockingCollection<string> respQueue = new BlockingCollection<string>();
            private readonly IBasicProperties props;

            public RpcClient()
            {
                var factory = new ConnectionFactory() { HostName = "localhost" };

                connection = factory.CreateConnection();
                channel = connection.CreateModel();
                replyQueueName = channel.QueueDeclare().QueueName;
                consumer = new EventingBasicConsumer(channel);

                props = channel.CreateBasicProperties();
                var correlationId = Guid.NewGuid().ToString();
                props.CorrelationId = correlationId;
                props.ReplyTo = replyQueueName;

                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body;
                    var response = Encoding.UTF8.GetString(body);
                    if (ea.BasicProperties.CorrelationId == correlationId)
                    {
                        respQueue.Add(response);
                    }
                };
            }

            public string Call(string message)
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish(
                    exchange: "",
                    routingKey: "rpc_queue",
                    basicProperties: props,
                    body: messageBytes);

                channel.BasicConsume(
                    consumer: consumer,
                    queue: replyQueueName,
                    autoAck: true);

                return respQueue.Take();
            }

            public void Close()
            {
                connection.Close();
            }
        }
    }
}
