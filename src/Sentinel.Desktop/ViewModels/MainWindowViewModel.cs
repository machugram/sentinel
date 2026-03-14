using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Sentinel.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;
    
    [ObservableProperty]
    private string _userName = "Ops User";
    
    [ObservableProperty]
    private string _userRole = "Operator";
    
    [ObservableProperty]
    private string _userInitials = "OU";
    
    [ObservableProperty]
    private int _activeAlertCount = 3;
    
    [ObservableProperty]
    private bool _hasActiveAlerts = true;
    
    public MainWindowViewModel()
    {
        // Start with the dashboard view
        _currentView = new DashboardViewModel();
    }
    
    [RelayCommand]
    private void NavigateToDashboard()
    {
        CurrentView = new DashboardViewModel();
    }
    
    [RelayCommand]
    private void NavigateToWorkflows()
    {
        CurrentView = new WorkflowListViewModel();
    }
    
    [RelayCommand]
    private void NavigateToRuns()
    {
        CurrentView = new RunsViewModel();
    }
    
    [RelayCommand]
    private void NavigateToAlerts()
    {
        CurrentView = new AlertsViewModel();
    }
    
    [RelayCommand]
    private void NavigateToMigration()
    {
        CurrentView = new MigrationWizardViewModel();
    }
    
    [RelayCommand]
    private void NavigateToCalendars()
    {
        CurrentView = new CalendarsViewModel();
    }
    
    [RelayCommand]
    private void NavigateToAudit()
    {
        CurrentView = new AuditViewModel();
    }
    
    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = new SettingsViewModel();
    }
}
