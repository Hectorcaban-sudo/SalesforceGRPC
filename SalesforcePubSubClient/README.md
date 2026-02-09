# Salesforce Pub/Sub API C# gRPC Subscriber

A robust C# application for subscribing to Salesforce Platform Events and Change Events using the Pub/Sub API via gRPC.

## Features

- ✅ Subscribe to Salesforce Platform Events and Change Events
- ✅ Support for all replay presets (LATEST, EARLIEST, CUSTOM)
- ✅ Automatic Avro payload decoding
- ✅ Schema-aware event decoding
- ✅ Comprehensive error handling and logging
- ✅ Graceful shutdown handling
- ✅ Configurable via environment variables or JSON file
- ✅ .NET 8.0 / .NET Core compatible

## Prerequisites

- .NET 8.0 SDK or later
- Salesforce org with Pub/Sub API enabled
- Salesforce user with appropriate permissions
- Connected App with OAuth enabled (or use session ID authentication)

## Installation

1. Clone or download this project
2. Navigate to the project directory
3. Restore NuGet packages:

```bash
dotnet restore
```

4. Build the project:

```bash
dotnet build
```

## Configuration

### Option 1: Environment Variables

Set the following environment variables:

```bash
export SF_INSTANCE_URL="https://yourinstance.my.salesforce.com"
export SF_ACCESS_TOKEN="your_access_token"
export SF_TENANT_ID="your_org_id"
export SF_TOPIC_NAME="/event/YourEvent__e"
export SF_REPLAY_PRESET="LATEST"
export SF_PUBSUB_ENDPOINT="api.pubsub.salesforce.com:443"
export SF_NUM_REQUESTED="0"
```

### Option 2: Configuration File

Create a JSON configuration file (e.g., `config.json`):

```json
{
  "InstanceUrl": "https://yourinstance.my.salesforce.com",
  "AccessToken": "your_access_token_or_session_id",
  "TenantId": "your_org_id",
  "PubSubEndpoint": "api.pubsub.salesforce.com:443",
  "TopicName": "/event/YourEvent__e",
  "ReplayPreset": "LATEST",
  "ReplayId": "",
  "NumRequested": 0
}
```

## Configuration Parameters

| Parameter | Required | Description | Default |
|-----------|----------|-------------|---------|
| `InstanceUrl` | Yes | Salesforce instance URL | - |
| `AccessToken` | Yes | OAuth access token or session ID | - |
| `TenantId` | Yes | Organization ID (00D...) | - |
| `PubSubEndpoint` | No | Pub/Sub API endpoint | `api.pubsub.salesforce.com:443` |
| `TopicName` | Yes | Platform event topic name (e.g., `/event/AccountChangeEvent`) | - |
| `ReplayPreset` | No | Replay preset: `LATEST`, `EARLIEST`, or `CUSTOM` | `LATEST` |
| `ReplayId` | No | Custom replay ID (required if ReplayPreset is `CUSTOM`) | - |
| `NumRequested` | No | Number of events to request (0 for infinite) | `0` |

## Getting Salesforce Credentials

### 1. Get Access Token via OAuth

```bash
# Example using curl for OAuth password flow
curl -X POST https://yourinstance.my.salesforce.com/services/oauth2/token \
  -d "grant_type=password" \
  -d "client_id=YOUR_CONNECTED_APP_CLIENT_ID" \
  -d "client_secret=YOUR_CONNECTED_APP_CLIENT_SECRET" \
  -d "username=YOUR_SF_USERNAME" \
  -d "password=YOUR_SF_PASSWORD+YOUR_SECURITY_TOKEN"
```

The response will contain:
- `access_token` → Set as `SF_ACCESS_TOKEN`
- `instance_url` → Set as `SF_INSTANCE_URL`

### 2. Get Tenant ID

The tenant ID is your Salesforce organization ID (starts with `00D`). You can find it in Salesforce Setup → Company Information → Salesforce Organization ID.

### 3. Find Topic Name

Platform event topic names follow the pattern: `/event/EventName__e`

For change events: `/data/ObjectNameChangeEvent`

## Running the Application

### Using Environment Variables

```bash
# Set environment variables (Linux/Mac)
export SF_INSTANCE_URL="https://yourinstance.my.salesforce.com"
export SF_ACCESS_TOKEN="your_token"
export SF_TENANT_ID="00D..."
export SF_TOPIC_NAME="/event/YourEvent__e"

# Run the application
dotnet run
```

### Using Configuration File

```bash
dotnet run config.json
```

## Usage Examples

### Subscribe to Latest Events

```bash
export SF_REPLAY_PRESET="LATEST"
dotnet run
```

### Subscribe from Earliest Events

```bash
export SF_REPLAY_PRESET="EARLIEST"
dotnet run
```

### Subscribe from Custom Replay Point

```bash
export SF_REPLAY_PRESET="CUSTOM"
export SF_REPLAY_ID="base64_encoded_replay_id"
dotnet run
```

### Subscribe to Change Events

```bash
export SF_TOPIC_NAME="/data/AccountChangeEvent"
export SF_REPLAY_PRESET="LATEST"
dotnet run
```

## Regional Endpoints

For data residency requirements, use regional endpoints:

| Region | Endpoint |
|--------|----------|
| Global | `api.pubsub.salesforce.com:443` |
| EU (Frankfurt) | `api.pubsub.eu01.salesforce.com:443` |
| EU (Ireland) | `api.pubsub.eu02.salesforce.com:443` |
| US West | `api.pubsub.usw2.salesforce.com:443` |

## Event Processing

The application includes a `ProcessCustomEvent` method in `Program.cs` where you can add your custom business logic:

```csharp
private static Task ProcessCustomEvent(Dictionary<string, object>? decodedPayload, ILogger logger)
{
    // Add your custom logic here
    // Examples:
    // - Save to database
    // - Call external APIs
    // - Send notifications
    
    return Task.CompletedTask;
}
```

## Avro Payload Decoding

The application automatically attempts to decode Avro-encoded event payloads. There are two modes:

### 1. Schema-Aware Decoding (Recommended)

Retrieves the schema from Salesforce and uses it to decode payloads accurately.

### 2. Schema-Less Decoding

Attempts to decode without schema information (limited functionality, may not work for all event types).

## Error Handling

The application includes comprehensive error handling:

- Connection errors are logged with detailed information
- Invalid configuration is validated before connection
- Event processing errors don't stop the subscription
- Graceful shutdown on Ctrl+C

## Troubleshooting

### Connection Refused
- Verify your `PubSubEndpoint` is correct
- Check network firewall settings
- Ensure port 443 or 7443 is open

### Authentication Failed
- Verify your access token is valid and not expired
- Check that your user has Pub/Sub API permissions
- Ensure your Connected App is properly configured

### No Events Received
- Verify the topic name is correct
- Check that events are being published to the topic
- Try using `EARLIEST` replay preset to see historical events
- Check Salesforce debug logs for subscription errors

### Avro Decoding Errors
- Ensure you have the correct schema
- Check that the event payload is not corrupted
- Verify the event type matches your expectations

## Project Structure

```
SalesforcePubSubClient/
├── Configuration/
│   └── SalesforceConfig.cs       # Configuration management
├── Protos/
│   └── pubsub_api.proto          # gRPC protocol definition
├── Services/
│   ├── PubSubSubscriber.cs       # Subscription service
│   └── AvroEventDecoder.cs       # Avro payload decoder
├── Program.cs                    # Main application entry point
├── appsettings.json              # Example configuration
├── README.md                     # This file
└── SalesforcePubSubClient.csproj # Project file
```

## Dependencies

- `Google.Protobuf` - Protocol buffers support
- `Grpc.Net.Client` - gRPC client
- `Grpc.Tools` - gRPC code generation
- `Apache.Avro` - Avro serialization
- `Newtonsoft.Json` - JSON processing

## API Reference

### PubSubSubscriber Methods

- `ConnectAsync()` - Establishes connection to Salesforce Pub/Sub API
- `SubscribeAsync()` - Subscribes to events with event handler
- `GetTopicInfoAsync()` - Retrieves topic information including schema ID
- `GetSchemaInfoAsync()` - Retrieves schema information for decoding
- `DisconnectAsync()` - Closes the connection

### SalesforceConfig Properties

- `InstanceUrl` - Salesforce instance URL
- `AccessToken` - OAuth token or session ID
- `TenantId` - Organization ID
- `PubSubEndpoint` - Pub/Sub API endpoint
- `TopicName` - Event topic name
- `ReplayPreset` - Replay strategy
- `ReplayId` - Custom replay ID
- `NumRequested` - Number of events to request

## Best Practices

1. **Use Persistent Connections**: The Subscribe RPC method maintains a long-lived connection with automatic keepalive
2. **Handle Reconnection**: Implement reconnection logic for network interruptions
3. **Monitor Replay IDs**: Store replay IDs to resume from specific points
4. **Process Events Efficiently**: Avoid blocking operations in event handlers
5. **Use Appropriate Replay Presets**: Choose based on your use case:
   - `LATEST` - For real-time processing of new events
   - `EARLIEST` - For processing all available events
   - `CUSTOM` - For resuming from specific replay points

## Security Considerations

- Store access tokens securely (use Azure Key Vault, AWS Secrets Manager, etc.)
- Use OAuth with short-lived tokens when possible
- Implement token refresh logic for long-running applications
- Validate all incoming event data
- Use TLS/SSL for all communications

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License

This project is provided as-is for educational and commercial use.

## Support

For Salesforce Pub/Sub API documentation:
- [Salesforce Pub/Sub API Developer Guide](https://developer.salesforce.com/docs/platform/pub-sub-api/guide/intro.html)
- [Salesforce Pub/Sub API GitHub Repository](https://github.com/forcedotcom/pub-sub-api)

For issues with this C# implementation, please open a GitHub issue.

## Changelog

### Version 1.0.0
- Initial release
- Support for Platform Events and Change Events
- Avro payload decoding
- Schema-aware decoding
- Comprehensive error handling
- Configuration via environment variables or JSON file