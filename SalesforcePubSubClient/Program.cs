using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesforcePubSub.Protos;
using SalesforcePubSubClient.Configuration;
using SalesforcePubSubClient.Services;

namespace SalesforcePubSubClient;

/// <summary>
/// ASP.NET Core Hosted Service for Salesforce Pub/Sub subscription
/// </summary>
public class SalesforcePubSubHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SalesforcePubSubHostedService> _logger;

    public SalesforcePubSubHostedService(
        IServiceProvider serviceProvider,
        ILogger<SalesforcePubSubHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Salesforce Pub/Sub Hosted Service Starting...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var subscriber = scope.ServiceProvider.GetRequiredService<PubSubSubscriber>();

            await subscriber.SubscribeAsync(async (consumerEvent) =>
            {
                await ProcessEvent(consumerEvent, scope);
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Subscription cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in hosted service");
            throw;
        }
    }

    private static async Task ProcessEvent(ConsumerEvent consumerEvent, IServiceScope scope)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SalesforcePubSubHostedService>>();
        var config = scope.ServiceProvider.GetRequiredService<SalesforceConfig>();

        try
        {
            logger.LogInformation("===========================================");
            logger.LogInformation("New Event Received");

            // Extract event attributes
            var attributes = AvroEventDecoder.FormatEventAttributes(consumerEvent.EventAttributes);
            foreach (var attr in attributes)
            {
                logger.LogInformation($"Attribute - {attr.Key}: {attr.Value}");
            }

            // Decode payload
            Dictionary<string, object>? decodedPayload = null;
            try
            {
                decodedPayload = AvroEventDecoder.DecodeAvroPayload(
                    consumerEvent.Payload.ToByteArray()
                );

                logger.LogInformation("Payload Data:");
                foreach (var kvp in decodedPayload)
                {
                    logger.LogInformation($"  {kvp.Key}: {FormatValue(kvp.Value)}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decode event payload");
                logger.LogInformation($"Raw payload length: {consumerEvent.Payload.Length} bytes");
                logger.LogInformation($"Raw payload (base64): {Convert.ToBase64String(consumerEvent.Payload.ToByteArray())}");
            }

            logger.LogInformation("===========================================");

            // Add your custom event processing logic here
            await ProcessCustomEvent(decodedPayload, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing event");
        }
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
            return "null";

        return value switch
        {
            string s => $"&quot;{s}&quot;",
            System.Collections.IEnumerable enumerable and not string => $"[{string.Join(", ", enumerable)}]",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static Task ProcessCustomEvent(Dictionary<string, object>? decodedPayload, ILogger logger)
    {
        // Add your custom business logic here
        // Examples:
        // - Save to database
        // - Call external APIs
        // - Send notifications
        // - Update caches
        
        logger.LogInformation("Processing custom event logic...");
        
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Salesforce Pub/Sub Hosted Service Stopping...");
        await base.StopAsync(cancellationToken);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                // Add command line arguments support
                if (args.Length > 0)
                {
                    config.AddCommandLine(args);
                }

                // Load from appsettings.json
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Bind configuration
                var config = context.Configuration.Get<SalesforceConfig>() ?? new SalesforceConfig();
                
                // Override with environment variables if present
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SF_INSTANCE_URL")))
                {
                    var envConfig = SalesforceConfig.FromEnvironment();
                    config = envConfig;
                }
                else if (args.Length > 0 && System.IO.File.Exists(args[0]))
                {
                    var fileConfig = SalesforceConfig.FromFile(args[0]);
                    config = fileConfig;
                }

                config.Validate();

                // Register configuration
                services.AddSingleton(config);

                // Add gRPC client for Salesforce Pub/Sub API
                var endpoint = $"https://{config.PubSubEndpoint}";
                services.AddGrpcClient<PubSub.PubSubClient>(o =>
                {
                    o.Address = new Uri(endpoint);
                    o.ChannelOptionsActions.Add(options =>
                    {
                        options.MaxReceiveMessageSize = 100 * 1024 * 1024; // 100 MB
                        options.HttpHandler = new System.Net.Http.HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = 
                                System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                    });
                })
                .AddInterceptor<SalesforcePubSubAuthInterceptor>();

                // Register services
                services.AddSingleton<PubSubSubscriber>();
                services.AddSingleton<SalesforcePubSubAuthInterceptor>();

                // Register hosted service
                services.AddHostedService<SalesforcePubSubHostedService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Salesforce Pub/Sub API Subscriber Starting...");
        logger.LogInformation($"Endpoint: {host.Services.GetRequiredService<SalesforceConfig>().PubSubEndpoint}");
        logger.LogInformation($"Topic: {host.Services.GetRequiredService<SalesforceConfig>().TopicName}");

        // Handle Ctrl+C gracefully
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received. Stopping...");
            cancellationTokenSource.Cancel();
        };

        try
        {
            await host.RunAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Application shutdown requested");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in application");
            throw;
        }
    }
}