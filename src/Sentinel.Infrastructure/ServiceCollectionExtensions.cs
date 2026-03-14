using Microsoft.Extensions.DependencyInjection;
using Sentinel.Infrastructure.Auth;

namespace Sentinel.Infrastructure;

/// <summary>
/// Dependency injection registration for Sentinel.Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSentinelInfrastructure(this IServiceCollection services, string apiBaseUrl)
    {
        // Auth
        services.AddSingleton<IAuthService, MockAuthService>();

        // API Client (Refit)
        services.AddRefitClient<Api.ISentinelApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

        // Observability will be registered here:
        // services.AddSingleton<IMetricsService, PrometheusMetricsService>();
        // services.AddSingleton<ITracingService, OpenTelemetryTracingService>();

        return services;
    }

    /// <summary>
    /// Adds mock/design-time services for desktop preview and testing.
    /// </summary>
    public static IServiceCollection AddSentinelInfrastructureMock(this IServiceCollection services)
    {
        services.AddSingleton<IAuthService, MockAuthService>();
        return services;
    }
}

// Refit registration helper (requires Refit.HttpClientFactory)
internal static class RefitExtensions
{
    public static IHttpClientBuilder AddRefitClient<T>(this IServiceCollection services) where T : class
    {
        return services.AddHttpClient(typeof(T).Name)
            .ConfigureHttpClient(_ => { });
    }
}
