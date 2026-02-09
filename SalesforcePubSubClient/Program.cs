using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SalesforcePubSub.Protos;
using SalesforcePubSubClient.Configuration;
using SalesforcePubSubClient.Services;

namespace SalesforcePubSubClient;

class Program
{
    static async Task Main(string[] args)
    {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        try
        {
            logger.LogInformation("Salesforce Pub/Sub API Subscriber Starting...");

            // Load configuration (try environment variables first, then file)
            SalesforceConfig config;
            if (args.Length > 0)
            {
                logger.LogInformation($"Loading configuration from file: {args[0]}");
                config = SalesforceConfig.FromFile(args[0]);
            }
            else
            {
                logger.LogInformation("Loading configuration from environment variables");
                config = SalesforceConfig.FromEnvironment();
            }

            // Validate configuration
            config.Validate();
            logger.LogInformation($"Configuration validated successfully");
            logger.LogInformation($"Topic: {config.TopicName}");
            logger.LogInformation($"Endpoint: {config.PubSubEndpoint}");
            logger.LogInformation($"Replay Preset: {config.ReplayPreset}");

            // Create subscriber
            var subscriber = new PubSubSubscriber(config, loggerFactory.CreateLogger<PubSubSubscriber>());

            // Setup cancellation token
            using var cts = new CancellationTokenSource();
            
            // Handle Ctrl+C to gracefully shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Shutdown signal received. Canceling subscription...");
                cts.Cancel();
            };

            try
            {
                // Connect to Salesforce
                await subscriber.ConnectAsync();

                // Get topic information
                var topicInfo = await subscriber.GetTopicInfoAsync();
                logger.LogInformation($"Topic Schema ID: {topicInfo.SchemaId}");

                // Get schema information (optional, for decoding)
                SchemaInfo? schemaInfo = null;
                try
                {
                    schemaInfo = await subscriber.GetSchemaInfoAsync(topicInfo.SchemaId);
                    logger.LogInformation($"Schema Type: {schemaInfo.Type}");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not retrieve schema info. Event decoding may be limited.");
                }

                // Subscribe to events
                await subscriber.SubscribeAsync(async (consumerEvent) =>
                {
                    await ProcessEvent(consumerEvent, schemaInfo, logger);
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Subscription cancelled by user");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in subscription loop");
                throw;
            }
            finally
            {
                await subscriber.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in application");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Processes a received event
    /// </summary>
    private static async Task ProcessEvent(ConsumerEvent consumerEvent, SchemaInfo? schemaInfo, ILogger logger)
    {
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
                if (schemaInfo != null && !string.IsNullOrWhiteSpace(schemaInfo.SchemaJson))
                {
                    // Decode with schema
                    decodedPayload = AvroEventDecoder.DecodeAvroPayloadWithSchema(
                        consumerEvent.Payload.ToByteArray(),
                        schemaInfo.SchemaJson
                    );
                }
                else
                {
                    // Decode without schema (limited)
                    decodedPayload = AvroEventDecoder.DecodeAvroPayload(
                        consumerEvent.Payload.ToByteArray()
                    );
                }

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

    /// <summary>
    /// Formats values for display
    /// </summary>
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

    /// <summary>
    /// Custom event processing logic
    /// </summary>
    private static Task ProcessCustomEvent(Dictionary<string, object>? decodedPayload, ILogger logger)
    {
        // Add your custom business logic here
        // For example:
        // - Save to database
        // - Call external APIs
        // - Send notifications
        // - Update caches
        
        // Example: Log the event to a file or external system
        // await File.AppendAllTextAsync("events.log", DateTime.UtcNow + " - Event received\n");
        
        return Task.CompletedTask;
    }
}