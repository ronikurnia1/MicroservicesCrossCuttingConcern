using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Diagnostics;

namespace GloboTicket.Common
{
    public static class Logging
    {
        public static Action<HostBuilderContext, IServiceProvider, LoggerConfiguration> ConfigureLogger =>
           (hostingContext, provider, loggerConfiguration) =>
           {
               var env = hostingContext.HostingEnvironment;

               loggerConfiguration.MinimumLevel.Information()
                   .Enrich.FromLogContext()
                   .Enrich.WithExceptionDetails()
                   .Enrich.WithProperty("ApplicationName", env.ApplicationName)
                   .Enrich.WithProperty("EnvironmentName", env.EnvironmentName)
                   .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                   .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                   .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                   .WriteTo.Console();

               loggerConfiguration.Enrich.With<LoggingEnricher>();  

               if (hostingContext.HostingEnvironment.IsDevelopment())
               {
                   loggerConfiguration.MinimumLevel.Override("GloboTicket", LogEventLevel.Debug);
               }

               var elasticUrl = hostingContext.Configuration.GetValue<string>("Logging:ElasticUrl");

               if (!string.IsNullOrEmpty(elasticUrl))
               {
                   loggerConfiguration.WriteTo.Elasticsearch(
                       new ElasticsearchSinkOptions(new Uri(elasticUrl))
                       {
                           AutoRegisterTemplate = true,
                           AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                           IndexFormat = "globoticket-logs-{0:yyyy.MM.dd}",
                           MinimumLogEventLevel = LogEventLevel.Debug,
                           ModifyConnectionSettings = c => c.BasicAuthentication("elastic", "changeme")
                       });
               }

               var appInsightsConnectionString = hostingContext.Configuration.GetValue<string>("AppInsightsConnectionString");

               if (!string.IsNullOrEmpty(appInsightsConnectionString))
               {
                   loggerConfiguration.WriteTo.ApplicationInsights(provider.GetRequiredService<TelemetryConfiguration>(), 
                       TelemetryConverter.Traces).CreateLogger();
               }
           };
    }


    public class LoggingEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                logEvent.AddPropertyIfAbsent(new LogEventProperty("TraceId", new ScalarValue(activity.TraceId)));
            }
        }
    }
}