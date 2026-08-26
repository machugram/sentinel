using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Desktop.Models;
using Sentinel.Desktop.Services;
using Sentinel.Desktop.ViewModels;
using Sentinel.Desktop.Views;
using Sentinel.Infrastructure;

namespace Sentinel.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            var config = Services.GetRequiredService<AppConfiguration>();
            ThemeManager.Apply(config.Theme);

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new AppConfiguration
        {
            EnableRealtime = false,
            Theme = "Dark",
            DashboardRefreshIntervalSeconds = 30,
            EnableNotifications = true
        });
        services.AddSentinelInfrastructureMock();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<WorkflowListViewModel>();
        services.AddTransient<RunsViewModel>();
        services.AddTransient<AlertsViewModel>();
        services.AddTransient<MigrationWizardViewModel>();
        services.AddTransient<CalendarsViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
