using Azure.Messaging.ServiceBus;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Payment.Messages;
using GloboTicket.Services.Payment.Model;
using GloboTicket.Services.Payment.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GloboTicket.Services.Payment.Worker
{
    public class ServiceBusListener : IHostedService
    {
        private readonly ILogger logger;
        private readonly IExternalGatewayPaymentService externalGatewayPaymentService;
        private readonly IMessageBus messageBus;
        private readonly string orderPaymentUpdatedMessageTopic;
        private readonly string connectionString;
        private readonly ServiceBusProcessor serviceBusProcessor;


        public ServiceBusListener(IConfiguration configuration, ILoggerFactory loggerFactory, IExternalGatewayPaymentService externalGatewayPaymentService, IMessageBus messageBus)
        {
            logger = loggerFactory.CreateLogger<ServiceBusListener>();
            orderPaymentUpdatedMessageTopic = configuration.GetValue<string>("OrderPaymentUpdatedMessageTopic");
            var orderPaymentRequestMessageTopic = configuration.GetValue<string>("OrderPaymentRequestMessageTopic");
            connectionString = configuration.GetValue<string>("ServiceBusConnectionString");
            var subscriptionName = configuration.GetValue<string>("subscriptionName");
            this.externalGatewayPaymentService = externalGatewayPaymentService;
            this.messageBus = messageBus;

            var serviceBusClient = new ServiceBusClient(connectionString);

            serviceBusProcessor = serviceBusClient.CreateProcessor(orderPaymentRequestMessageTopic, subscriptionName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {

            serviceBusProcessor.ProcessMessageAsync += ProcessMessageAsync;
            serviceBusProcessor.ProcessErrorAsync += ProcessError;

            serviceBusProcessor.StartProcessingAsync(cancellationToken);
            return Task.CompletedTask;
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogDebug($"ServiceBusListener stopping.");
            serviceBusProcessor.ProcessMessageAsync -= ProcessMessageAsync;
            serviceBusProcessor.ProcessErrorAsync -= ProcessError;
            await serviceBusProcessor.StopProcessingAsync(cancellationToken);
        }

        protected async Task ProcessError(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception, "Error while processing queue item in ServiceBusListener.");
            await Task.CompletedTask;
        }

        protected async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            using var scope = logger.BeginScope("Processing message for trace {TraceId}", args.Message.CorrelationId);

            var messageBody = Encoding.UTF8.GetString(args.Message.Body);
            OrderPaymentRequestMessage orderPaymentRequestMessage = JsonConvert.DeserializeObject<OrderPaymentRequestMessage>(messageBody);

            PaymentInfo paymentInfo = new PaymentInfo
            {
                CardNumber = orderPaymentRequestMessage.CardNumber,
                CardName = orderPaymentRequestMessage.CardName,
                CardExpiration = orderPaymentRequestMessage.CardExpiration,
                Total = orderPaymentRequestMessage.Total
            };

            var result = await externalGatewayPaymentService.PerformPayment(paymentInfo);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);

            //send payment result to order service via service bus
            OrderPaymentUpdateMessage orderPaymentUpdateMessage = new OrderPaymentUpdateMessage
            {
                PaymentSuccess = result,
                OrderId = orderPaymentRequestMessage.OrderId
            };

            try
            {
                await messageBus.PublishMessage(orderPaymentUpdateMessage, orderPaymentUpdatedMessageTopic, connectionString, args.Message.CorrelationId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            logger.LogDebug($"{orderPaymentRequestMessage.OrderId}: ServiceBusListener received item.");
            await Task.Delay(2000);
            logger.LogDebug($"{orderPaymentRequestMessage.OrderId}:  ServiceBusListener processed item.");
        }
    }
}
