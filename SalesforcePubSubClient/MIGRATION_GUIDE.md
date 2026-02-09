# Migration Guide: Console Application to ASP.NET Core

This guide helps you migrate from the console application version to the ASP.NET Core version with `AddGrpcClient`.

## Overview of Changes

### Project File Changes

**Before (Console App):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.Net.Client" Version="2.63.0" />
  </ItemGroup>
</Project>
```

**After (ASP.NET Core):**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.Net.Client" Version="2.63.0" />
    <PackageReference Include="Grpc.Net.ClientFactory" Version="2.63.0" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
  </ItemGroup>
</Project>
```

### New Components

1. **SalesforcePubSubAuthInterceptor.cs**
   - Automatically injects authentication headers
   - Implements gRPC interceptor pattern
   - Centralizes authentication logic

2. **SalesforcePubSubHostedService.cs** (in Program.cs)
   - ASP.NET Core BackgroundService
   - Manages subscription lifecycle
   - Handles graceful shutdown

3. **Updated Program.cs**
   - Configures ASP.NET Core host
   - Sets up dependency injection
   - Registers gRPC client factory

## Step-by-Step Migration

### Step 1: Update Project File

Change the SDK and add new packages:

```bash
# Edit SalesforcePubSubClient.csproj
# Change: <Project Sdk="Microsoft.NET.Sdk">
# To: <Project Sdk="Microsoft.NET.Sdk.Web">

# Add packages:
dotnet add package Grpc.Net.ClientFactory
dotnet add package Microsoft.Extensions.Options.ConfigurationExtensions
```

### Step 2: Create Authentication Interceptor

Create `SalesforcePubSubAuthInterceptor.cs`:

```csharp
public class SalesforcePubSubAuthInterceptor : Interceptor
{
    private readonly SalesforceConfig _config;
    private readonly ILogger<SalesforcePubSubAuthInterceptor> _logger;

    public SalesforcePubSubAuthInterceptor(
        SalesforceConfig config,
        ILogger<SalesforcePubSubAuthInterceptor> logger)
    {
        _config = config;
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var options = context.Options.WithHeaders(CreateAuthHeaders());
        context = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options);
        return base.AsyncUnaryCall(request, context, continuation);
    }

    private Metadata CreateAuthHeaders()
    {
        return new Metadata
        {
            { "accesstoken", _config.AccessToken },
            { "instanceurl", _config.InstanceUrl },
            { "tenantid", _config.TenantId }
        };
    }
}
```

### Step 3: Simplify PubSubSubscriber

Remove manual channel management:

**Removed methods:**
- `ConnectAsync()`
- `DisconnectAsync()`
- `DisposeAsync()`

**Constructor change:**

**Before:**
```csharp
public PubSubSubscriber(SalesforceConfig config, ILogger<PubSubSubscriber> logger)
{
    _config = config;
    _logger = logger;
}
```

**After:**
```csharp
public PubSubSubscriber(
    PubSub.PubSubClient client,
    SalesforceConfig config,
    ILogger<PubSubSubscriber> logger)
{
    _client = client ?? throw new ArgumentNullException(nameof(client));
    _config = config ?? throw new ArgumentNullException(nameof(config));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

**Method changes:**

**Before:**
```csharp
public async Task SubscribeAsync(...)
{
    if (_client == null)
        throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
    
    var metadata = CreateAuthMetadata();
    using var streamingCall = _client.Subscribe(request, metadata, cancellationToken);
    // ...
}
```

**After:**
```csharp
public async Task SubscribeAsync(...)
{
    using var streamingCall = _client.Subscribe(request, cancellationToken);
    // ...
}
```

### Step 4: Create BackgroundService

Create a new hosted service:

```csharp
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
        using var scope = _serviceProvider.CreateScope();
        var subscriber = scope.ServiceProvider.GetRequiredService<PubSubSubscriber>();

        await subscriber.SubscribeAsync(async (consumerEvent) =>
        {
            await ProcessEvent(consumerEvent, scope);
        }, stoppingToken);
    }
}
```

### Step 5: Update Program.cs

Replace the console Main with ASP.NET Core host:

**Before:**
```csharp
static async Task Main(string[] args)
{
    // Load config
    var config = SalesforceConfig.FromEnvironment();
    config.Validate();

    // Create logger
    using var loggerFactory = LoggerFactory.Create(...);

    // Create subscriber
    var subscriber = new PubSubSubscriber(config, logger);

    // Connect
    await subscriber.ConnectAsync();

    // Subscribe
    await subscriber.SubscribeAsync(...);
}
```

**After:**
```csharp
static async Task Main(string[] args)
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddCommandLine(args);
        })
        .ConfigureServices((context, services) =>
        {
            var config = GetConfiguration(context, args);
            services.AddSingleton(config);

            services.AddGrpcClient<PubSub.PubSubClient>(o =>
            {
                o.Address = new Uri($"https://{config.PubSubEndpoint}");
                o.ChannelOptionsActions.Add(options =>
                {
                    options.MaxReceiveMessageSize = 100 * 1024 * 1024;
                });
            })
            .AddInterceptor<SalesforcePubSubAuthInterceptor>();

            services.AddSingleton<PubSubSubscriber>();
            services.AddSingleton<SalesforcePubSubAuthInterceptor>();
            services.AddHostedService<SalesforcePubSubHostedService>();
        })
        .Build();

    await host.RunAsync();
}
```

## Key Benefits of Migration

### 1. Automatic Dependency Injection

**Before:**
```csharp
var subscriber = new PubSubSubscriber(config, logger);
```

**After:**
```csharp
var subscriber = scope.ServiceProvider.GetRequiredService<PubSubSubscriber>();
```

### 2. Automatic Authentication

**Before:**
```csharp
var metadata = CreateAuthMetadata();
_client.Subscribe(request, metadata);
```

**After:**
```csharp
_client.Subscribe(request); // Headers added automatically
```

### 3. Lifecycle Management

**Before:**
```csharp
await subscriber.ConnectAsync();
try
{
    await subscriber.SubscribeAsync(...);
}
finally
{
    await subscriber.DisconnectAsync();
}
```

**After:**
```csharp
// BackgroundService handles everything automatically
services.AddHostedService<SalesforcePubSubHostedService>();
```

### 4. Better Testing

**Before:**
```csharp
// Difficult to mock
var subscriber = new PubSubSubscriber(config, logger);
```

**After:**
```csharp
// Easy to test with DI
services.AddSingleton<IPubSubSubscriber, MockPubSubSubscriber>();
```

## Compatibility

### Configuration Compatibility

All configuration options remain the same:

- ✅ Environment variables work
- ✅ JSON configuration files work
- ✅ Command-line arguments work

### Event Processing Compatibility

Event processing logic remains the same:

```csharp
await subscriber.SubscribeAsync(async (consumerEvent) =>
{
    // Your event processing logic here
}, cancellationToken);
```

### API Compatibility

Most methods remain the same:

- ✅ `SubscribeAsync()` - Same signature
- ✅ `GetTopicInfoAsync()` - Same signature
- ✅ `GetSchemaInfoAsync()` - Same signature
- ❌ `ConnectAsync()` - Removed
- ❌ `DisconnectAsync()` - Removed
- ❌ `DisposeAsync()` - Removed

## Testing the Migration

### 1. Build Test

```bash
dotnet build
```

### 2. Run Test

```bash
dotnet run
```

### 3. Verify Event Reception

Modify a record in Salesforce and verify the event is received.

## Common Migration Issues

### Issue 1: "Client not set"

**Error:**
```
NullReferenceException: Object reference not set to an instance of an object
```

**Solution:**
Ensure you're using dependency injection:

```csharp
services.AddSingleton<PubSubSubscriber>();
```

### Issue 2: Authentication headers not sent

**Error:**
```
Unauthorized
```

**Solution:**
Ensure the interceptor is registered:

```csharp
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>();
```

### Issue 3: BackgroundService not starting

**Error:**
```
No events received
```

**Solution:**
Ensure the hosted service is registered:

```csharp
services.AddHostedService<SalesforcePubSubHostedService>();
```

## Performance Improvements

| Metric | Console App | ASP.NET Core |
|--------|-------------|--------------|
| Memory Usage | Higher | Lower ✅ |
| Connection Pooling | Manual | Automatic ✅ |
| Resource Cleanup | Manual | Automatic ✅ |
| Startup Time | Faster | Slightly Slower |
| Throughput | Same | Same ✅ |

## Deployment Considerations

### Console App Deployment

```bash
dotnet publish -c Release -o ./publish
./publish/SalesforcePubSubClient
```

### ASP.NET Core Deployment

```bash
dotnet publish -c Release -o ./publish
./publish/SalesforcePubSubClient
```

No changes needed - deployment process is the same!

## Additional Features Enabled

### 1. Health Checks

```csharp
services.AddHealthChecks()
    .AddCheck<SalesforcePubSubHealthCheck>("pubsub");
```

### 2. Multiple Subscriptions

```csharp
services.AddHostedService<AccountEventSubscriber>();
services.AddHostedService<ContactEventSubscriber>();
```

### 3. Integration with Web API

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Pub/Sub
builder.Services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>();
builder.Services.AddHostedService<SalesforcePubSubHostedService>();

// Add Web API
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

### 4. Retry Policies

```csharp
services.AddGrpcClient<PubSub.PubSubClient>()
    .AddInterceptor<SalesforcePubSubAuthInterceptor>()
    .AddPolicyHandler(GetRetryPolicy());
```

## Rollback Plan

If you need to rollback to the console version:

1. Restore the original `Program.cs`
2. Restore the original `PubSubSubscriber.cs`
3. Revert the project file changes
4. Remove `SalesforcePubSubAuthInterceptor.cs`

All configuration and event processing logic remains compatible!

## Support

For issues during migration:

1. Check the error messages carefully
2. Verify all services are registered
3. Ensure the interceptor is added to the gRPC client
4. Review the sample code in `README_ASPNET_CORE.md`

## Conclusion

The ASP.NET Core version provides:

- ✅ Better architecture with dependency injection
- ✅ Automatic resource management
- ✅ Easier testing
- ✅ More extensibility
- ✅ Better observability
- ✅ Production-ready patterns

The migration is straightforward and all your existing configuration and event processing logic will work without changes!

---

**Estimated Migration Time:** 1-2 hours
**Difficulty:** Medium
**Risk:** Low (configuration and logic remain compatible)