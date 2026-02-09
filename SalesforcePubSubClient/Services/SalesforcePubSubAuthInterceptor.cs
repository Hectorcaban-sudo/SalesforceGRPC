using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using SalesforcePubSubClient.Configuration;

namespace SalesforcePubSubClient.Services;

/// <summary>
/// gRPC interceptor for adding Salesforce authentication headers to all requests
/// </summary>
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
            context.Method,
            context.Host,
            options);

        return base.AsyncUnaryCall(request, context, continuation);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var options = context.Options.WithHeaders(CreateAuthHeaders());
        context = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            options);

        return base.AsyncClientStreamingCall(context, continuation);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var options = context.Options.WithHeaders(CreateAuthHeaders());
        context = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            options);

        return base.AsyncDuplexStreamingCall(context, continuation);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var options = context.Options.WithHeaders(CreateAuthHeaders());
        context = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            options);

        return base.AsyncServerStreamingCall(request, context, continuation);
    }

    private Metadata CreateAuthHeaders()
    {
        var metadata = new Metadata
        {
            { "accesstoken", _config.AccessToken },
            { "instanceurl", _config.InstanceUrl },
            { "tenantid", _config.TenantId }
        };

        _logger.LogDebug("Created authentication headers");
        return metadata;
    }
}