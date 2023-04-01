using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GloboTicket.Shared
{
    public class AzureServiceBusHealthCheck : IHealthCheck
    {
        private readonly string connectionString;
        private readonly string topicName;

        public AzureServiceBusHealthCheck(string connectionString, string topicName)
        {
            this.connectionString = connectionString;
            this.topicName = topicName;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var serviceBusAdministrationClient = new ServiceBusAdministrationClient(connectionString);
                _ = await serviceBusAdministrationClient.GetTopicRuntimePropertiesAsync(topicName, cancellationToken).ConfigureAwait(false);
                return HealthCheckResult.Healthy();
            }
            catch (Exception e)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, exception: e);
            }
        }
    }
}

