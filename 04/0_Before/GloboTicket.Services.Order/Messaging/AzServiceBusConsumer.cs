using Azure.Messaging.ServiceBus;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Ordering.Entities;
using GloboTicket.Services.Ordering.Messages;
using GloboTicket.Services.Ordering.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace GloboTicket.Services.Ordering.Messaging
{
    public class AzServiceBusConsumer : IAzServiceBusConsumer
    {
        private readonly string subscriptionName = "globoticketorder";
        private readonly IMessageBus messageBus;

        private readonly IConfiguration _configuration;

        private readonly OrderRepository _orderRepository;
        private readonly ServiceBusProcessor _serviceBusProcessor;

        private readonly string checkoutMessageTopic;
        private readonly string orderPaymentRequestMessageTopic;
        private readonly string serviceBusConnectionString;

        private readonly ILogger<AzServiceBusConsumer> logger;

        public AzServiceBusConsumer(IConfiguration configuration, IMessageBus messageBus, OrderRepository orderRepository, ILogger<AzServiceBusConsumer> logger)
        {
            this.messageBus = messageBus;
            _configuration = configuration;
            _orderRepository = orderRepository;
            this.logger = logger;

            serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            orderPaymentRequestMessageTopic = _configuration.GetValue<string>("OrderPaymentRequestMessageTopic");

            var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);

            checkoutMessageTopic = _configuration.GetValue<string>("CheckoutMessageTopic");

            _serviceBusProcessor = serviceBusClient.CreateProcessor(checkoutMessageTopic, subscriptionName);
        }

        public void Start()
        {
            _serviceBusProcessor.ProcessMessageAsync += MessageHandler;
            _serviceBusProcessor.ProcessErrorAsync += ErrorHandler;

            _serviceBusProcessor.StartProcessingAsync();
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            using var scope = logger.BeginScope("Processing message for trace {TraceId}", args.Message.CorrelationId);

            var body = Encoding.UTF8.GetString(args.Message.Body);//json from service bus

            // Save order with status "not paid"
            BasketCheckoutMessage basketCheckoutMessage = JsonConvert.DeserializeObject<BasketCheckoutMessage>(body);

            var orderId = Guid.NewGuid();

            var order = new Order
            {
                UserId = basketCheckoutMessage.UserId,
                Id = orderId,
                OrderPaid = false,
                OrderPlaced = DateTime.Now,
                OrderTotal = basketCheckoutMessage.BasketTotal
            };

            await _orderRepository.AddOrder(order);

            // Trigger payment service by sending a new message.  
            // Functionality not included in demo on purpose.  
            var orderPaymentRequestMessage = new OrderPaymentRequestMessage
            {
                OrderId = orderId,
                CardExpiration = basketCheckoutMessage.CardExpiration,
                CardName = basketCheckoutMessage.CardName,
                CardNumber = basketCheckoutMessage.CardNumber,
                CreationDateTime = DateTime.Now,
                Id = Guid.NewGuid(),
                Total = basketCheckoutMessage.BasketTotal
            };

            await messageBus.PublishMessage(orderPaymentRequestMessage, orderPaymentRequestMessageTopic, serviceBusConnectionString, args.Message.CorrelationId);
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());

            return Task.CompletedTask;
        }

        public void Stop()
        {
            _serviceBusProcessor.ProcessMessageAsync -= MessageHandler;
            _serviceBusProcessor.ProcessErrorAsync -= ErrorHandler;
            _serviceBusProcessor.StopProcessingAsync();
        }
    }
}
