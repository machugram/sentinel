using Microsoft.Extensions.DependencyInjection;
using Sentinel.Core.Interfaces;

namespace Sentinel.Core.Services;

/// <summary>
/// Dependency injection registration for Sentinel.Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSentinelCore(this IServiceCollection services)
    {
        // Service implementations will be registered here as they are built.
        // Example:
        // services.AddSingleton<IWorkflowService, WorkflowService>();
        // services.AddTransient<IJilMigrationService, JilMigrationService>();
        // services.AddSingleton<ICalendarService, CalendarService>();
        // services.AddSingleton<IAlertService, AlertService>();
        // services.AddSingleton<IAuditService, AuditService>();

        return services;
    }
}
