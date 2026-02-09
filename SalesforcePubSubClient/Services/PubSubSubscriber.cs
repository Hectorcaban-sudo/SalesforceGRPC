using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using SalesforcePubSub.Protos;
using SalesforcePubSubClient.Configuration;

namespace SalesforcePubSubClient.Services;

/// <summary>
/// Service for subscribing to Salesforce Pub/Sub API events using gRPC
/// </summary>
public class PubSubSubscriber
{
    private readonly SalesforceConfig _config;
    private readonly ILogger<PubSubSubscriber> _logger;
    private GrpcChannel? _channel;
    private PubSub.PubSubClient? _client;

    public PubSubSubscriber(SalesforceConfig config, ILogger<PubSubSubscriber> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Connects to the Salesforce Pub/Sub API
    /// </summary>
    public async Task ConnectAsync()
    {
        _logger.LogInformation($"Connecting to Salesforce Pub/Sub API at {_config.PubSubEndpoint}");

        _channel = GrpcChannel.ForAddress($"https://{_config.PubSubEndpoint}", new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 100 * 1024 * 1024, // 100 MB
            HttpHandler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }
        });

        _client = new PubSub.PubSubClient(_channel);
        _logger.LogInformation("Successfully connected to Salesforce Pub/Sub API");
    }

    /// <summary>
    /// Creates metadata headers for authentication
    /// </summary>
    private Metadata CreateAuthMetadata()
    {
        var metadata = new Metadata
        {
            { "accesstoken", _config.AccessToken },
            { "instanceurl", _config.InstanceUrl },
            { "tenantid", _config.TenantId }
        };
        return metadata;
    }

    /// <summary>
    /// Subscribes to events and processes them using the provided handler
    /// </summary>
    public async Task SubscribeAsync(Func<ConsumerEvent, Task> eventHandler, CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        _logger.LogInformation($"Subscribing to topic: {_config.TopicName}");

        var request = new FetchRequest
        {
            TopicName = _config.TopicName,
            ReplayPreset = _config.ReplayPreset,
            ReplayId = _config.ReplayId ?? "",
            NumRequested = _config.NumRequested
        };

        var metadata = CreateAuthMetadata();

        try
        {
            using var streamingCall = _client.Subscribe(request, metadata, cancellationToken: cancellationToken);

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
        if (_client == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        var request = new TopicRequest
        {
            TopicName = _config.TopicName
        };

        var metadata = CreateAuthMetadata();

        _logger.LogInformation($"Getting topic info for: {_config.TopicName}");

        var response = await _client.GetTopicAsync(request, metadata);

        _logger.LogInformation($"Topic info retrieved - Schema ID: {response.SchemaId}, Type: {response.TopicType}");

        return response;
    }

    /// <summary>
    /// Gets schema information
    /// </summary>
    public async Task<SchemaInfo> GetSchemaInfoAsync(string schemaId)
    {
        if (_client == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        var request = new SchemaRequest
        {
            SchemaId = schemaId
        };

        var metadata = CreateAuthMetadata();

        _logger.LogInformation($"Getting schema info for: {schemaId}");

        var response = await _client.GetSchemaAsync(request, metadata);

        _logger.LogInformation($"Schema info retrieved - Type: {response.Type}");

        return response;
    }

    /// <summary>
    /// Disconnects from the Salesforce Pub/Sub API
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_channel != null)
        {
            await _channel.ShutdownAsync();
            _logger.LogInformation("Disconnected from Salesforce Pub/Sub API");
        }
    }

    /// <summary>
    /// Disposes resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}