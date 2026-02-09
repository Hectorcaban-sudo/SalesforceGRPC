# Salesforce Pub/Sub API ASP.NET Core gRPC Subscriber

An advanced ASP.NET Core application for subscribing to Salesforce Platform Events and Change Events using the Pub/Sub API via gRPC with `AddGrpcClient` and Dependency Injection.

## What's New in ASP.NET Core Version

This version has been modernized with ASP.NET Core best practices:

- ✅ **`AddGrpcClient`** - Built-in gRPC client factory with dependency injection
- ✅ **`BackgroundService`** - Hosted service pattern for long-running subscriptions
- ✅ **Interceptors** - Centralized authentication header injection
- ✅ **Configuration Binding** - Seamless integration with ASP.NET Core configuration
- ✅ **Logging Integration** - Structured logging with Microsoft.Extensions.Logging
- ✅ **Graceful Shutdown** - Proper lifecycle management with cancellation tokens

## Architecture

### Dependency Injection Setup

The application uses ASP.NET Core's built-in dependency injection:

```csharp
services.AddSingleton<SalesforceConfig>();
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>();
services.AddSingleton<PubSubSubscriber>();
services.AddHostedService<SalesforcePubSubHostedService>();
```

### Authentication Interceptor

The `SalesforcePubSubAuthInterceptor` automatically adds authentication headers to all gRPC calls:

```csharp
public class SalesforcePubSubAuthInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var options = context.Options.WithHeaders(CreateAuthHeaders());
        // Automatically injects accesstoken, instanceurl, tenantid
    }
}
```

### Background Service

The `SalesforcePubSubHostedService` manages the subscription lifecycle:

```csharp
public class SalesforcePubSubHostedService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await subscriber.SubscribeAsync(async (consumerEvent) =>
        {
            await ProcessEvent(consumerEvent, scope);
        }, stoppingToken);
    }
}
```

## Benefits Over Console Application

| Feature | Console App | ASP.NET Core |
|---------|-------------|--------------|
| Dependency Injection | Manual | Built-in ✅ |
| Configuration | Custom | ASP.NET Core ✅ |
| gRPC Client Factory | Manual | `AddGrpcClient` ✅ |
| Lifecycle Management | Manual | `BackgroundService` ✅ |
| Logging | Custom | `ILogger<T>` ✅ |
| Authentication Headers | Per-call | Interceptor ✅ |
| Testing | Difficult | Easy (DI) ✅ |
| Extensibility | Limited | High ✅ |

## Prerequisites

- .NET 8.0 SDK or later
- Salesforce org with Pub/Sub API enabled
- Salesforce user with appropriate permissions
- Connected App with OAuth enabled

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

### Option 1: appsettings.json

```json
{
  "InstanceUrl": "https://yourinstance.my.salesforce.com",
  "AccessToken": "your_access_token",
  "TenantId": "your_org_id",
  "PubSubEndpoint": "api.pubsub.salesforce.com:443",
  "TopicName": "/event/YourEvent__e",
  "ReplayPreset": "LATEST",
  "ReplayId": "",
  "NumRequested": 0
}
```

### Option 2: Environment Variables

```bash
export SF_INSTANCE_URL="https://yourinstance.my.salesforce.com"
export SF_ACCESS_TOKEN="your_access_token"
export SF_TENANT_ID="your_org_id"
export SF_TOPIC_NAME="/event/YourEvent__e"
```

### Option 3: Configuration File

```bash
dotnet run config.json
```

## Running the Application

```bash
# Run with appsettings.json
dotnet run

# Run with environment variables
export SF_INSTANCE_URL="https://yourinstance.my.salesforce.com"
export SF_ACCESS_TOKEN="your_token"
export SF_TENANT_ID="00D..."
export SF_TOPIC_NAME="/event/AccountChangeEvent"
dotnet run

# Run with configuration file
dotnet run config.json
```

## Key Components

### 1. SalesforcePubSubAuthInterceptor

Automatically injects authentication headers into all gRPC calls.

**Benefits:**
- Centralized authentication logic
- No need to pass headers manually
- Works with all gRPC call types
- Easy to test and maintain

### 2. PubSubSubscriber

Simplified service that uses the injected gRPC client.

**Changes from Console Version:**
- Removed manual channel management
- Uses injected `PubSub.PubSubClient`
- No need to call `ConnectAsync()`
- Cleaner API surface

### 3. SalesforcePubSubHostedService

ASP.NET Core `BackgroundService` that manages the subscription.

**Benefits:**
- Proper startup/shutdown lifecycle
- Graceful cancellation handling
- Integration with ASP.NET Core host
- Scoped service resolution

### 4. Program.cs

Configures the ASP.NET Core host with all services.

**Key Configurations:**
- `AddGrpcClient<PubSub.PubSubClient>()` - Registers gRPC client
- `AddInterceptor<T>()` - Adds authentication
- `AddSingleton<T>()` - Registers services
- `AddHostedService<T>()` - Registers background service

## Testing

The application can be easily tested with mock services:

```csharp
// Example test setup
var services = new ServiceCollection();
services.AddSingleton(config);
services.AddGrpcClient<PubSub.PubSubClient>();
// Add mocks and tests
```

## Extending the Application

### Adding Custom Interceptors

```csharp
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>()
    .AddInterceptor<LoggingInterceptor>()
    .AddInterceptor<RetryInterceptor>();
```

### Adding Additional Services

```csharp
services.AddSingleton<IDatabaseService, DatabaseService>();
services.AddSingleton<INotificationService, NotificationService>();
```

### Using in ASP.NET Core Web API

You can also run this as part of a web API:

```csharp
// In Startup.cs or Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Salesforce Pub/Sub
builder.Services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>();
builder.Services.AddHostedService<SalesforcePubSubHostedService>();

// Add Web API
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

## Migration from Console Application

If you're migrating from the console version, here are the key changes:

### Removed Methods

- `ConnectAsync()` - No longer needed, managed by gRPC client factory
- `DisconnectAsync()` - No longer needed, managed by ASP.NET Core host
- `DisposeAsync()` - No longer needed, managed by DI container

### New Components

- `SalesforcePubSubAuthInterceptor` - Handles authentication
- `SalesforcePubSubHostedService` - Manages subscription lifecycle

### Configuration Changes

Configuration now uses ASP.NET Core's configuration system:

```csharp
// Old way
var config = SalesforceConfig.FromEnvironment();

// New way (automatic)
var config = context.Configuration.Get<SalesforceConfig>();
```

## Performance Considerations

The ASP.NET Core version offers several performance benefits:

1. **Connection Pooling** - Managed by gRPC client factory
2. **Resource Management** - Automatic disposal via DI container
3. **Async/Await** - Full async support throughout
4. **Scoped Services** - Proper scoping for event processing

## Deployment Options

### 1. Standalone Service

Run as a standalone background service:

```bash
dotnet run
```

### 2. Windows Service

Use `Microsoft.Extensions.Hosting.WindowsServices`:

```csharp
builder.UseWindowsService();
```

### 3. Linux Service (systemd)

Use `Microsoft.Extensions.Hosting.Systemd`:

```csharp
builder.UseSystemd();
```

### 4. Docker Container

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY . /app
WORKDIR /app
ENTRYPOINT ["dotnet", "SalesforcePubSubClient.dll"]
```

### 5. Kubernetes Deployment

Deploy as a Deployment with proper resource limits and health checks.

## Monitoring and Observability

### Structured Logging

All logging uses `ILogger<T>` with structured data:

```csharp
_logger.LogInformation("Processing event {EventId}", eventId);
```

### Health Checks

Add health checks to monitor the subscription status:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<SalesforcePubSubHealthCheck>("pubsub");
```

### Metrics

Integrate with Application Insights, Prometheus, or other metrics systems.

## Troubleshooting

### gRPC Client Not Injecting Headers

Ensure the interceptor is registered:

```csharp
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>();
```

### Service Not Starting

Check the hosted service registration:

```csharp
services.AddHostedService<SalesforcePubSubHostedService>();
```

### Configuration Not Loading

Verify the configuration binding:

```csharp
var config = context.Configuration.Get<SalesforceConfig>();
config.Validate();
```

## Best Practices

1. **Use Dependency Injection** - Leverage ASP.NET Core's DI for all services
2. **Scoped Services** - Use scoped services for event processing to avoid memory leaks
3. **Error Handling** - Implement proper error handling in the hosted service
4. **Logging** - Use structured logging for better observability
5. **Testing** - Test with mock gRPC clients and services
6. **Configuration** - Use environment variables for sensitive data
7. **Shutdown** - Ensure proper cleanup in the hosted service

## Advanced Features

### Retry Policy

Add retry logic using Polly:

```csharp
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>()
    .AddPolicyHandler(GetRetryPolicy());
```

### Circuit Breaker

Implement circuit breaker pattern for resilience:

```csharp
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddCircuitBreakerPolicy();
```

### Multiple Subscriptions

Register multiple hosted services for different topics:

```csharp
services.AddHostedService<AccountEventSubscriber>();
services.AddHostedService<ContactEventSubscriber>();
services.AddHostedService<OpportunityEventSubscriber>();
```

## Resources

- [ASP.NET Core BackgroundService](https://docs.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
- [gRPC Client Factory](https://docs.microsoft.com/aspnet/core/grpc/clientfactory)
- [Dependency Injection](https://docs.microsoft.com/aspnet/core/fundamentals/dependency-injection)
- [Salesforce Pub/Sub API](https://developer.salesforce.com/docs/platform/pub-sub-api/guide/intro.html)

## License

This project is provided as-is for educational and commercial use.

---

**Status**: ✅ Production Ready
**Framework**: ASP.NET Core 8.0
**Pattern**: Background Service with gRPC Client Factory