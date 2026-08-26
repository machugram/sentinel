using Microsoft.Extensions.DependencyInjection;
using Sentinel.Core.Interfaces;
using Sentinel.Infrastructure.Auth;
using Sentinel.Infrastructure.Mock;

namespace Sentinel.Infrastructure;

/// <summary>
/// Dependency injection registration for Sentinel.Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSentinelInfrastructure(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddSingleton<IAuthService, MockAuthService>();

        services.AddRefitClient<Api.ISentinelApiClient>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

        return services;
    }

    /// <summary>
    /// Adds mock/design-time services for desktop preview and local development without a backend.
    /// </summary>
    public static IServiceCollection AddSentinelInfrastructureMock(this IServiceCollection services)
    {
        services.AddSingleton<IAuthService, MockAuthService>();
        services.AddSingleton<MockDataStore>();
        services.AddSingleton<IWorkflowService, MockWorkflowService>();
        services.AddSingleton<IWorkflowRunService, MockWorkflowRunService>();
        services.AddSingleton<IAlertService, MockAlertService>();
        services.AddSingleton<ICalendarService, MockCalendarService>();
        services.AddSingleton<IAuditService, MockAuditService>();
        services.AddSingleton<IJilMigrationService, MockJilMigrationService>();
        return services;
    }
}

internal static class RefitExtensions
{
    public static IHttpClientBuilder AddRefitClient<T>(this IServiceCollection services) where T : class
    {
        return services.AddHttpClient(typeof(T).Name)
            .ConfigureHttpClient(_ => { });
    }
}
