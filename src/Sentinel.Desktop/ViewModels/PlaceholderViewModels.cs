using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Avalonia.Threading;

namespace Sentinel.Desktop.ViewModels;

// Placeholder ViewModels for navigation (to be enhanced)
public partial class RunsViewModel : ViewModelBase { }
public partial class AlertsViewModel : ViewModelBase { }
public partial class CalendarsViewModel : ViewModelBase { }
public partial class AuditViewModel : ViewModelBase { }
public partial class SettingsViewModel : ViewModelBase { }

/// <summary>
/// Enhanced Migration Wizard for AutoSys to Sentinel migration
/// Supports accelerated migration with AI-powered risk classification
/// </summary>
public partial class MigrationWizardViewModel : ViewModelBase
{
    private readonly IJilMigrationService _migrationService;
    private readonly IWorkflowService _workflowService;
    
    [ObservableProperty]
    private int _currentStep = 1;
    
    [ObservableProperty]
    private string _jilFilePath = string.Empty;
    
    [ObservableProperty]
    private string _jilContent = string.Empty;
    
    [ObservableProperty]
    private ObservableCollection<JilJobPreview> _importedJobs = new();
    
    [ObservableProperty]
    private ObservableCollection<ConversionResult> _conversionResults = new();
    
    [ObservableProperty]
    private bool _isProcessing = false;
    
    [ObservableProperty]
    private double _overallConfidence = 0;
    
    [ObservableProperty]
    private int _highRiskCount = 0;
    
    [ObservableProperty]
    private int _mediumRiskCount = 0;
    
    [ObservableProperty]
    private int _lowRiskCount = 0;
    
    [ObservableProperty]
    private bool _enableParallelMigration = true;
    
    [ObservableProperty]
    private bool _skipValidationForLowRisk = true;
    
    [ObservableProperty]
    private string _migrationMode = "accelerated"; // "standard" or "accelerated"
    
    public MigrationWizardViewModel(
        IJilMigrationService migrationService,
        IWorkflowService workflowService)
    {
        _migrationService = migrationService;
        _workflowService = workflowService;
    }
    
    // Design-time constructor
    public MigrationWizardViewModel()
    {
        ImportedJobs = new ObservableCollection<JilJobPreview>
        {
            new("TRADE_CAPTURE_DAILY", "box_type: c", 85, "low"),
            new("RISK_EOD_CALC", "box_type: c", 92, "low"),
            new("DTCC_REPORT_GEN", "box_type: c", 78, "medium"),
            new("NAV_CALCULATION", "box_type: c", 95, "low")
        };
    }
    
    [RelayCommand]
    private async Task SelectJilFileAsync()
    {
        // Open file picker
        var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select AutoSys JIL File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JIL Files")
                {
                    Patterns = new[] { "*.jil", "*.txt" }
                }
            }
        };
        
        // TODO: Implement file picker logic
        // For now, placeholder
    }
    
    [RelayCommand]
    private async Task ParseJilAsync()
    {
        if (string.IsNullOrEmpty(JilContent)) return;
        
        IsProcessing = true;
        try
        {
            // Parse JIL file
            var jobs = await _migrationService.ParseJilFileAsync(JilContent);
            
            ImportedJobs.Clear();
            foreach (var job in jobs)
            {
                // AI-powered risk classification
                var riskLevel = ClassifyRisk(job);
                var confidence = CalculateConfidence(job);
                
                ImportedJobs.Add(new JilJobPreview(
                    job.JobName,
                    $"{job.JobType} | {job.Condition ?? "no deps"}",
                    confidence,
                    riskLevel
                ));
            }
            
            // Update statistics
            HighRiskCount = ImportedJobs.Count(j => j.RiskLevel == "high");
            MediumRiskCount = ImportedJobs.Count(j => j.RiskLevel == "medium");
            LowRiskCount = ImportedJobs.Count(j => j.RiskLevel == "low");
            OverallConfidence = ImportedJobs.Average(j => j.Confidence);
            
            CurrentStep = 2;
        }
        catch (Exception ex)
        {
            // Show error
            Console.WriteLine($"JIL parsing failed: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    [RelayCommand]
    private async Task ConvertJobsAsync()
    {
        IsProcessing = true;
        ConversionResults.Clear();
        
        try
        {
            if (MigrationMode == "accelerated" && EnableParallelMigration)
            {
                // Parallel conversion for speed
                var tasks = ImportedJobs.Select(async job =>
                {
                    var result = await ConvertSingleJobAsync(job);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ConversionResults.Add(result));
                });
                
                await Task.WhenAll(tasks);
            }
            else
            {
                // Sequential conversion
                foreach (var job in ImportedJobs)
                {
                    var result = await ConvertSingleJobAsync(job);
                    ConversionResults.Add(result);
                }
            }
            
            CurrentStep = 3;
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    private async Task<ConversionResult> ConvertSingleJobAsync(JilJobPreview job)
    {
        try
        {
            // Convert job using migration service
            var jilJob = new JilJob { JobName = job.JobName };
            var conversionResult = await _migrationService.ConvertJobAsync(jilJob);
            
            return new ConversionResult(
                job.JobName,
                "success",
                job.Confidence,
                conversionResult.ConvertedWorkflow?.Id.ToString() ?? "",
                job.RiskLevel == "low" && SkipValidationForLowRisk ? "Validation skipped (low risk)" : "Needs validation"
            );
        }
        catch (Exception ex)
        {
            return new ConversionResult(
                job.JobName,
                "failed",
                0,
                "",
                ex.Message
            );
        }
    }
    
    [RelayCommand]
    private async Task StartMigrationAsync()
    {
        IsProcessing = true;
        try
        {
            // Import converted workflows
            var successfulConversions = ConversionResults.Where(r => r.Status == "success");
            
            foreach (var result in successfulConversions)
            {
                // Workflow already created in conversion step
                // Update status or perform additional actions
            }
            
            CurrentStep = 4;
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    /// <summary>
    /// AI-powered risk classification based on job characteristics
    /// </summary>
    private string ClassifyRisk(JilJob job)
    {
        var riskScore = 0;
        
        // Complex dependencies increase risk (parsed from Condition string)
        var depCount = job.Condition?.Split('&', '|').Length ?? 0;
        if (depCount > 5) riskScore += 2;
        else if (depCount > 2) riskScore += 1;
        
        // Custom scripts are higher risk
        if (job.Command?.Contains("custom") == true || job.Command?.Contains("script") == true)
            riskScore += 2;
        
        // File watchers are medium risk
        if (job.JobType == JilJobType.FileWatcher) riskScore += 1;
        
        // Command jobs are typically low risk
        if (job.JobType == JilJobType.Command && depCount <= 2) riskScore -= 1;
        
        return riskScore >= 3 ? "high" : riskScore >= 1 ? "medium" : "low";
    }
    
    private double CalculateConfidence(JilJob job)
    {
        // Simple confidence calculation
        // In production, this would use ML model
        var baseConfidence = 100.0;
        
        if (job.JobType == JilJobType.Command) baseConfidence -= 0; // Standard command, high confidence
        else if (job.JobType == JilJobType.FileWatcher) baseConfidence -= 10; // File watcher
        else baseConfidence -= 20; // Unknown type
        
        var depCount = job.Condition?.Split('&', '|').Length ?? 0;
        if (depCount > 5) baseConfidence -= 15;
        if (string.IsNullOrEmpty(job.Command)) baseConfidence -= 25;
        
        return Math.Max(0, Math.Min(100, baseConfidence));
    }
}

public record JilJobPreview(string JobName, string JobType, double Confidence, string RiskLevel);
public record ConversionResult(string JobName, string Status, double Confidence, string WorkflowId, string Notes);
