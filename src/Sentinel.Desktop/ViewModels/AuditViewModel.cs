using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.ViewModels;

public partial class AuditViewModel : ViewModelBase
{
    private readonly IAuditService? _auditService;
    private List<AuditLogEntry> _all = new();

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _exportStatus = string.Empty;
    [ObservableProperty] private ObservableCollection<AuditLogEntry> _entries = new();
    [ObservableProperty] private bool _isLoading;

    public AuditViewModel()
    {
        _all =
        [
            new AuditLogEntry { Timestamp = DateTime.UtcNow.AddMinutes(-8), Action = "workflow.trigger", EntityType = "Workflow", UserName = "Alex Chen", NewValue = "manual trigger" },
            new AuditLogEntry { Timestamp = DateTime.UtcNow.AddHours(-1), Action = "run.fail", EntityType = "WorkflowRun", UserName = "scheduler", NewValue = "Failed" }
        ];
        ApplyFilters();
    }

    public AuditViewModel(IAuditService auditService)
    {
        _auditService = auditService;
        _ = LoadAsync();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_auditService is null)
            return;
        IsLoading = true;
        try
        {
            _all = (await _auditService.GetAuditLogsAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddMinutes(1))).ToList();
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync(string? format)
    {
        if (_auditService is null)
        {
            ExportStatus = $"Exported {Entries.Count} records (mock)";
            WeakReferenceMessenger.Default.Send(new StatusMessage(ExportStatus));
            return;
        }

        var parsed = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Csv : ExportFormat.Ndjson;
        await using var stream = await _auditService.ExportAuditLogsAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, parsed);
        ExportStatus = $"Exported {Entries.Count} records as {parsed}";
        WeakReferenceMessenger.Default.Send(new StatusMessage(ExportStatus));
    }

    private void ApplyFilters()
    {
        IEnumerable<AuditLogEntry> query = _all;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(e =>
                e.Action.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.UserName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                e.EntityType.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                (e.NewValue?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        Entries = new ObservableCollection<AuditLogEntry>(query);
    }
}
