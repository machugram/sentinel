using CommunityToolkit.Mvvm.ComponentModel;

namespace Sentinel.Desktop.Models;

public class AppConfiguration
{
    public string ApiBaseUrl { get; set; } = "https://localhost:5001";
    public string SignalRHubUrl { get; set; } = "https://localhost:5001/hubs/monitoring";
    public int DashboardRefreshIntervalSeconds { get; set; } = 30;
    public string Theme { get; set; } = "Dark";
    public bool EnableTelemetry { get; set; } = false;
    public bool EnableRealtime { get; set; } = false;
    public bool EnableNotifications { get; set; } = true;
}

public partial class NavigationItem : ObservableObject
{
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _iconKey = string.Empty;
    [ObservableProperty] private string _viewKey = string.Empty;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private int _badgeCount;

    public bool HasBadge => BadgeCount > 0;

    partial void OnBadgeCountChanged(int value) => OnPropertyChanged(nameof(HasBadge));
}

public record StatusMessage(string Text);

public record DataRefreshedMessage(DateTime When);

public record NavigateRequest(
    string ViewKey,
    Guid? EntityId = null,
    string? Filter = null,
    bool OpenCreate = false);

public sealed class CommandPaletteItem
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Kind { get; init; }
    public required Action Execute { get; init; }
}
