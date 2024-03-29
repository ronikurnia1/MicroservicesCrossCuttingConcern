﻿using GloboTicket.Integration.Messages;
using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace GloboTicket.Integration.MessagingBus
{
    public class AzServiceBusMessageBus : IMessageBus
    {
        public async Task PublishMessage(IntegrationBaseMessage message, string topicName, string connectionString)
        {
            await using ServiceBusClient client = new ServiceBusClient(connectionString);
            var sender = client.CreateSender(topicName);

            var jsonMessage = JsonConvert.SerializeObject(message);
            var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(jsonMessage))
            {
                CorrelationId = Guid.NewGuid().ToString()
            };

            await sender.SendMessageAsync(serviceBusMessage);
            Console.WriteLine($"Sent message to {sender.EntityPath}");
            await sender.CloseAsync();

        }
    }
}
