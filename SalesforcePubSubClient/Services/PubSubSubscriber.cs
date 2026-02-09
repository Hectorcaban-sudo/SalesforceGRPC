using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using SalesforcePubSub.Protos;
using SalesforcePubSubClient.Configuration;

namespace SalesforcePubSubClient.Services;

/// <summary>
/// Service for subscribing to Salesforce Pub/Sub API events using gRPC
/// </summary>
public class PubSubSubscriber
{
    private readonly PubSub.PubSubClient _client;
    private readonly SalesforceConfig _config;
    private readonly ILogger<PubSubSubscriber> _logger;

    public PubSubSubscriber(
        PubSub.PubSubClient client,
        SalesforceConfig config,
        ILogger<PubSubSubscriber> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribes to events and processes them using the provided handler
    /// </summary>
    public async Task SubscribeAsync(Func<ConsumerEvent, Task> eventHandler, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Subscribing to topic: {_config.TopicName}");

        var request = new FetchRequest
        {
            TopicName = _config.TopicName,
            ReplayPreset = _config.ReplayPreset,
            ReplayId = _config.ReplayId ?? "",
            NumRequested = _config.NumRequested
        };

        try
        {
            using var streamingCall = _client.Subscribe(request, cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully subscribed. Waiting for events...");

            await foreach (var response in streamingCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation($"Received batch of {response.Results.Count} events");

                foreach (var result in response.Results)
                {
                    try
                    {
                        await eventHandler(result.Event);
                        _logger.LogDebug($"Successfully processed event with replay ID: {result.ReplayId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing event with replay ID: {result.ReplayId}");
                    }
                }
            }
        }
        catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Cancelled)
        {
            _logger.LogInformation("Subscription cancelled");
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, $"gRPC error during subscription: {ex.Status}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during subscription");
            throw;
        }
    }

    /// <summary>
    /// Gets topic information
    /// </summary>
    public async Task<TopicInfo> GetTopicInfoAsync()
    {
        var request = new TopicRequest
        {
            TopicName = _config.TopicName
        };

        _logger.LogInformation($"Getting topic info for: {_config.TopicName}");

        var response = await _client.GetTopicAsync(request);

        _logger.LogInformation($"Topic info retrieved - Schema ID: {response.SchemaId}, Type: {response.TopicType}");

        return response;
    }

    /// <summary>
    /// Gets schema information
    /// </summary>
    public async Task<SchemaInfo> GetSchemaInfoAsync(string schemaId)
    {
        var request = new SchemaRequest
        {
            SchemaId = schemaId
        };

        _logger.LogInformation($"Getting schema info for: {schemaId}");

        var response = await _client.GetSchemaAsync(request);

        _logger.LogInformation($"Schema info retrieved - Type: {response.Type}");

        return response;
    }
}