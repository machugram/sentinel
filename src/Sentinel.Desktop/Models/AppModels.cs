namespace Sentinel.Desktop.Models;

/// <summary>
/// Desktop app configuration loaded from appsettings or environment.
/// </summary>
public class AppConfiguration
{
    public string ApiBaseUrl { get; set; } = "https://localhost:5001";
    public string SignalRHubUrl { get; set; } = "https://localhost:5001/hubs/monitoring";
    public int DashboardRefreshIntervalSeconds { get; set; } = 30;
    public string Theme { get; set; } = "Dark";
    public bool EnableTelemetry { get; set; } = false;
}

/// <summary>
/// Navigation item model for sidebar menu.
/// </summary>
public class NavigationItem
{
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ViewKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int BadgeCount { get; set; }
}
