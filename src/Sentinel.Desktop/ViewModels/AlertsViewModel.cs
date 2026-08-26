using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService? _alertService;
    private List<Alert> _all = new();

    [ObservableProperty] private string _severityFilter = "All";
    [ObservableProperty] private ObservableCollection<Alert> _alerts = new();
    [ObservableProperty] private Alert? _selectedAlert;
    [ObservableProperty] private bool _isLoading;

    public IReadOnlyList<string> SeverityOptions { get; } = new[] { "All", "Critical", "Warning", "Info" };

    public int CriticalCount => _all.Count(a => a.Severity == AlertSeverity.Critical && a.ResolvedAt is null);
    public int WarningCount => _all.Count(a => a.Severity == AlertSeverity.Warning && a.ResolvedAt is null);
    public int InfoCount => _all.Count(a => a.Severity == AlertSeverity.Info && a.ResolvedAt is null);

    public AlertsViewModel()
    {
        _all =
        [
            new Alert { Id = Guid.NewGuid(), Title = "SLA Breach Warning", Message = "Trade Capture Pipeline approaching SLA threshold", Severity = AlertSeverity.Warning, Type = AlertType.SlaBreach, CreatedAt = DateTime.UtcNow.AddMinutes(-10), AiSuggestion = "Check booking API latency." },
            new Alert { Id = Guid.NewGuid(), Title = "Task Failure", Message = "Market Data Reconciliation failed after 3 retries", Severity = AlertSeverity.Critical, Type = AlertType.TaskFailure, CreatedAt = DateTime.UtcNow.AddHours(-1), AiSuggestion = "Re-pull the 16:00 snapshot." }
        ];
        ApplyFilters();
    }

    public AlertsViewModel(IAlertService alertService)
    {
        _alertService = alertService;
        _ = LoadAsync();
    }

    partial void OnSeverityFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_alertService is null)
            return;
        IsLoading = true;
        try
        {
            _all = (await _alertService.GetActiveAlertsAsync()).ToList();
            ApplyFilters();
            OnPropertyChanged(nameof(CriticalCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(InfoCount));
            WeakReferenceMessenger.Default.Send(new DataRefreshedMessage(DateTime.UtcNow));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AcknowledgeAsync(Alert? alert)
    {
        if (alert is null || _alertService is null)
            return;
        await _alertService.AcknowledgeAlertAsync(alert.Id, "Alex Chen");
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Acknowledged {alert.Title}"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ResolveAsync(Alert? alert)
    {
        if (alert is null || _alertService is null)
            return;
        await _alertService.ResolveAlertAsync(alert.Id, "Resolved from desktop");
        WeakReferenceMessenger.Default.Send(new StatusMessage($"Resolved {alert.Title}"));
        await LoadAsync();
    }

    private void ApplyFilters()
    {
        IEnumerable<Alert> query = _all.Where(a => a.ResolvedAt is null);
        if (!string.Equals(SeverityFilter, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(a => a.Severity.ToString().Equals(SeverityFilter, StringComparison.OrdinalIgnoreCase));
        Alerts = new ObservableCollection<Alert>(query.OrderByDescending(a => a.CreatedAt));
        SelectedAlert ??= Alerts.FirstOrDefault();
    }
}
