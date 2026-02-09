# Quick Start Guide - Salesforce Pub/Sub API C# Subscriber

This guide will help you get up and running with the Salesforce Pub/Sub API subscriber in under 5 minutes.

## Prerequisites Checklist

Before you begin, make sure you have:

- [ ] .NET 8.0 SDK installed (check with `dotnet --version`)
- [ ] Access to a Salesforce org with Pub/Sub API enabled
- [ ] Salesforce user with appropriate permissions
- [ ] Connected App created (for OAuth access)

## Step 1: Setup Salesforce

### 1.1 Create a Connected App (if you don't have one)

1. Go to **Setup** → **App Manager** → **New Connected App**
2. Fill in the required information:
   - **Connected App Name**: "PubSub API Client"
   - **API Name**: "PubSub_APIClient"
3. In **API (Enable OAuth Settings)**:
   - Check **Enable OAuth Settings**
   - **Callback URL**: `https://login.salesforce.com/services/oauth2/callback`
   - **Selected OAuth Scopes**: Check "Full Access (full)"
4. Save and note down:
   - **Consumer Key** (Client ID)
   - **Consumer Secret** (Client Secret)

### 1.2 Get Your Access Token

Use OAuth password flow to get an access token:

```bash
curl -X POST https://YOUR_INSTANCE.my.salesforce.com/services/oauth2/token \
  -d "grant_type=password" \
  -d "client_id=YOUR_CONSUMER_KEY" \
  -d "client_secret=YOUR_CONSUMER_SECRET" \
  -d "username=YOUR_USERNAME" \
  -d "password=YOUR_PASSWORD+YOUR_SECURITY_TOKEN"
```

**Important**: Your security token can be found in **Settings** → **My Personal Information** → **Reset My Security Token**

Save the `access_token`, `instance_url`, and note your org ID (starts with `00D`).

## Step 2: Configure the Application

### 2.1 Using Environment Variables (Recommended)

**Linux/Mac:**
```bash
export SF_INSTANCE_URL="https://yourinstance.my.salesforce.com"
export SF_ACCESS_TOKEN="00D...access_token_here"
export SF_TENANT_ID="00Dxxxxxxxxxxxx"
export SF_TOPIC_NAME="/event/AccountChangeEvent"
```

**Windows PowerShell:**
```powershell
$env:SF_INSTANCE_URL="https://yourinstance.my.salesforce.com"
$env:SF_ACCESS_TOKEN="00D...access_token_here"
$env:SF_TENANT_ID="00Dxxxxxxxxxxxx"
$env:SF_TOPIC_NAME="/event/AccountChangeEvent"
```

### 2.2 Using .env File

1. Copy the example file:
```bash
cp .env.example .env
```

2. Edit `.env` with your values:
```bash
SF_INSTANCE_URL=https://yourinstance.my.salesforce.com
SF_ACCESS_TOKEN=00D...your_access_token_here
SF_TENANT_ID=00Dxxxxxxxxxxxx
SF_TOPIC_NAME=/event/AccountChangeEvent
```

### 2.3 Using Configuration File

1. Copy `appsettings.json` and edit with your values:
```json
{
  "InstanceUrl": "https://yourinstance.my.salesforce.com",
  "AccessToken": "00D...your_access_token_here",
  "TenantId": "00Dxxxxxxxxxxxx",
  "TopicName": "/event/AccountChangeEvent",
  "ReplayPreset": "LATEST"
}
```

## Step 3: Choose Your Topic

### Platform Events
Format: `/event/EventName__e`

Examples:
- `/event/AccountChangeEvent` - Account change events
- `/event/ContactChangeEvent` - Contact change events
- `/event/YourCustomEvent__e` - Custom platform events

### Change Events
Format: `/data/ObjectNameChangeEvent`

Examples:
- `/data/AccountChangeEvent` - Account changes
- `/data/ContactChangeEvent` - Contact changes

**Note**: Make sure Change Data Capture is enabled for the object in Setup → Change Data Capture.

## Step 4: Run the Application

### Option 1: Using Start Script (Recommended)

**Linux/Mac:**
```bash
./start.sh
```

**Windows:**
```cmd
start.bat
```

### Option 2: Manual Build and Run

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

Or with a config file:
```bash
dotnet run config.json
```

## Step 5: Test Your Subscription

### 5.1 Subscribe to Change Events

Set your topic to a change event (e.g., `/data/AccountChangeEvent`):

```bash
export SF_TOPIC_NAME="/data/AccountChangeEvent"
```

Then run the application and modify an Account record in Salesforce. You should see the event in your console.

### 5.2 Subscribe to Platform Events

Set your topic to a platform event (e.g., `/event/YourCustomEvent__e`):

```bash
export SF_TOPIC_NAME="/event/YourCustomEvent__e"
```

Then publish an event using Salesforce Developer Console or Workbench.

## Understanding Replay Presets

### LATEST (Default)
Receives only new events published after subscription starts.

```bash
export SF_REPLAY_PRESET="LATEST"
```

### EARLIEST
Receives all available events from the event bus.

```bash
export SF_REPLAY_PRESET="EARLIEST"
```

### CUSTOM
Receives events starting from a specific replay point.

```bash
export SF_REPLAY_PRESET="CUSTOM"
export SF_REPLAY_ID="base64_encoded_replay_id"
```

## Common Use Cases

### 1. Real-Time Data Synchronization
```bash
export SF_TOPIC_NAME="/data/ContactChangeEvent"
export SF_REPLAY_PRESET="LATEST"
dotnet run
```

### 2. Historical Data Backfill
```bash
export SF_TOPIC_NAME="/data/AccountChangeEvent"
export SF_REPLAY_PRESET="EARLIEST"
export SF_NUM_REQUESTED="1000"  # Limit to 1000 events
dotnet run
```

### 3. Custom Event Processing
```bash
export SF_TOPIC_NAME="/event/OrderCreated__e"
export SF_REPLAY_PRESET="LATEST"
dotnet run
```

## Troubleshooting

### Connection Errors

**Problem**: "Connection refused" or timeout

**Solutions**:
1. Check your `SF_PUBSUB_ENDPOINT` is correct
2. Verify network connectivity to Salesforce
3. Check firewall settings (port 443 or 7443)

### Authentication Errors

**Problem**: "Unauthorized" or "Invalid token"

**Solutions**:
1. Verify your access token is not expired
2. Check your tenant ID is correct
3. Ensure your user has Pub/Sub API permissions

### No Events Received

**Problem**: Application connects but no events appear

**Solutions**:
1. Verify the topic name is correct
2. Check that events are being published
3. Try `SF_REPLAY_PRESET=EARLIEST` to see historical events
4. Check Salesforce debug logs for subscription errors

### Avro Decoding Errors

**Problem**: "Failed to decode event payload"

**Solutions**:
1. This is normal for some event types
2. The raw payload is still logged in base64
3. For custom events, you may need to implement custom decoding logic

## Next Steps

### Customize Event Processing

Edit `Program.cs` and modify the `ProcessCustomEvent` method:

```csharp
private static Task ProcessCustomEvent(Dictionary<string, object>? decodedPayload, ILogger logger)
{
    // Your custom logic here
    // Example: Save to database
    // await SaveToDatabase(decodedPayload);
    
    return Task.CompletedTask;
}
```

### Add Reconnection Logic

For production use, implement automatic reconnection in `PubSubSubscriber.cs`.

### Monitor Performance

Add metrics and monitoring to track:
- Event throughput
- Processing latency
- Error rates
- Connection health

## Resources

- [Salesforce Pub/Sub API Documentation](https://developer.salesforce.com/docs/platform/pub-sub-api/guide/intro.html)
- [Salesforce Pub/Sub API GitHub](https://github.com/forcedotcom/pub-sub-api)
- [gRPC Documentation](https://grpc.io/docs/)

## Getting Help

If you encounter issues:

1. Check the console output for detailed error messages
2. Review Salesforce debug logs
3. Verify all configuration values are correct
4. Ensure your Salesforce org has the required permissions
5. Check network connectivity and firewall settings

## Security Best Practices

1. **Never commit credentials** to version control
2. **Use environment variables** or secret managers for sensitive data
3. **Rotate access tokens** regularly
4. **Use short-lived tokens** when possible
5. **Implement token refresh** for long-running applications
6. **Validate all incoming data** before processing

## Performance Tips

1. Use `LATEST` replay preset for real-time processing
2. Implement batching for database writes
3. Use async/await for I/O operations
4. Consider using a message queue for event distribution
5. Monitor memory usage and implement backpressure if needed

Congratulations! You're now ready to receive Salesforce events in real-time using gRPC.