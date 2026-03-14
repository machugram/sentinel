using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Sentinel.Desktop.ViewModels;

public partial class WorkflowListViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _searchQuery = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<WorkflowItem> _workflows;
    
    [ObservableProperty]
    private WorkflowItem? _selectedWorkflow;
    
    public WorkflowListViewModel()
    {
        _workflows = new ObservableCollection<WorkflowItem>
        {
            new(Guid.NewGuid(), "Trade Capture Pipeline", "Active", "Americas", 156, 98.5, "*/15 * * * *"),
            new(Guid.NewGuid(), "EOD Risk Calculation", "Active", "Global", 89, 99.1, "0 17 * * 1-5"),
            new(Guid.NewGuid(), "DTCC Regulatory Report", "Active", "Americas", 234, 97.8, "0 6 * * 1-5"),
            new(Guid.NewGuid(), "Market Data Reconciliation", "Active", "APAC", 112, 95.2, "0 8 * * *"),
            new(Guid.NewGuid(), "NAV Calculation", "Active", "EMEA", 67, 99.5, "0 18 * * 1-5"),
            new(Guid.NewGuid(), "Surveillance Daily", "Active", "Global", 45, 100.0, "0 0 * * *"),
            new(Guid.NewGuid(), "Client Reporting", "Paused", "Americas", 23, 98.0, "0 9 1 * *"),
            new(Guid.NewGuid(), "Margin Calculation", "Active", "EMEA", 178, 97.2, "0 16 * * 1-5"),
        };
    }
    
    [RelayCommand]
    private void CreateWorkflow()
    {
        // TODO: Open workflow designer
    }
    
    [RelayCommand]
    private void TriggerWorkflow(WorkflowItem workflow)
    {
        // TODO: Trigger workflow execution
    }
    
    [RelayCommand]
    private void EditWorkflow(WorkflowItem workflow)
    {
        // TODO: Open workflow editor
    }
}

public record WorkflowItem(
    Guid Id, 
    string Name, 
    string Status, 
    string Region, 
    int TotalRuns, 
    double SuccessRate,
    string Schedule)
{
    public string SuccessRateText => $"{SuccessRate:F1}%";
}
