using System;
using System.IO;

namespace SalesforcePubSubClient.Configuration;

/// <summary>
/// Configuration class for Salesforce Pub/Sub API connection
/// </summary>
public class SalesforceConfig
{
    /// <summary>
    /// Salesforce instance URL (e.g., https://yourinstance.my.salesforce.com)
    /// </summary>
    public string InstanceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Access token or session ID for authentication
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Organization ID (tenant ID)
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Pub/Sub API endpoint (global: api.pubsub.salesforce.com:443 or regional)
    /// </summary>
    public string PubSubEndpoint { get; set; } = "api.pubsub.salesforce.com:443";

    /// <summary>
    /// Topic name to subscribe to (e.g., /event/YourEvent__e)
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Replay preset: LATEST, EARLIEST, or CUSTOM
    /// </summary>
    public string ReplayPreset { get; set; } = "LATEST";

    /// <summary>
    /// Custom replay ID (required if ReplayPreset is CUSTOM)
    /// </summary>
    public string? ReplayId { get; set; }

    /// <summary>
    /// Number of events to request (0 for infinite stream)
    /// </summary>
    public int NumRequested { get; set; } = 0;

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InstanceUrl))
            throw new InvalidOperationException("InstanceUrl is required");

        if (string.IsNullOrWhiteSpace(AccessToken))
            throw new InvalidOperationException("AccessToken is required");

        if (string.IsNullOrWhiteSpace(TenantId))
            throw new InvalidOperationException("TenantId is required");

        if (string.IsNullOrWhiteSpace(TopicName))
            throw new InvalidOperationException("TopicName is required");

        if (ReplayPreset == "CUSTOM" && string.IsNullOrWhiteSpace(ReplayId))
            throw new InvalidOperationException("ReplayId is required when ReplayPreset is CUSTOM");
    }

    /// <summary>
    /// Loads configuration from environment variables
    /// </summary>
    public static SalesforceConfig FromEnvironment()
    {
        return new SalesforceConfig
        {
            InstanceUrl = Environment.GetEnvironmentVariable("SF_INSTANCE_URL") ?? string.Empty,
            AccessToken = Environment.GetEnvironmentVariable("SF_ACCESS_TOKEN") ?? string.Empty,
            TenantId = Environment.GetEnvironmentVariable("SF_TENANT_ID") ?? string.Empty,
            PubSubEndpoint = Environment.GetEnvironmentVariable("SF_PUBSUB_ENDPOINT") ?? "api.pubsub.salesforce.com:443",
            TopicName = Environment.GetEnvironmentVariable("SF_TOPIC_NAME") ?? string.Empty,
            ReplayPreset = Environment.GetEnvironmentVariable("SF_REPLAY_PRESET") ?? "LATEST",
            ReplayId = Environment.GetEnvironmentVariable("SF_REPLAY_ID"),
            NumRequested = int.TryParse(Environment.GetEnvironmentVariable("SF_NUM_REQUESTED"), out var num) ? num : 0
        };
    }

    /// <summary>
    /// Loads configuration from a JSON file
    /// </summary>
    public static SalesforceConfig FromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Configuration file not found: {filePath}");

        var json = File.ReadAllText(filePath);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<SalesforceConfig>(json)
               ?? throw new InvalidOperationException("Failed to deserialize configuration");
    }
}