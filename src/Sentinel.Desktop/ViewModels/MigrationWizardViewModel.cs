using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;
using Sentinel.Desktop.Models;
using Sentinel.Desktop.Services;
using Sentinel.Infrastructure.Mock;

namespace Sentinel.Desktop.ViewModels;

public partial class MigrationWizardViewModel : ViewModelBase
{
    private readonly IJilMigrationService? _migrationService;
    private readonly IWorkflowService? _workflowService;
    private readonly IFilePickerService? _filePicker;

    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private string _jilFilePath = string.Empty;
    [ObservableProperty] private string _jilContent = string.Empty;
    [ObservableProperty] private ObservableCollection<JilJobPreview> _importedJobs = new();
    [ObservableProperty] private ObservableCollection<ConversionResult> _conversionResults = new();
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private double _overallConfidence;
    [ObservableProperty] private int _highRiskCount;
    [ObservableProperty] private int _mediumRiskCount;
    [ObservableProperty] private int _lowRiskCount;
    [ObservableProperty] private bool _enableParallelMigration = true;
    [ObservableProperty] private bool _skipValidationForLowRisk = true;
    [ObservableProperty] private string _migrationMode = "accelerated";
    [ObservableProperty] private int _importedWorkflowCount;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public IReadOnlyList<string> MigrationModes { get; } = new[] { "accelerated", "standard" };

    public MigrationWizardViewModel()
    {
        JilContent = MockJilMigrationService.SampleJil;
    }

    public MigrationWizardViewModel(
        IJilMigrationService migrationService,
        IWorkflowService workflowService,
        IFilePickerService filePicker)
    {
        _migrationService = migrationService;
        _workflowService = workflowService;
        _filePicker = filePicker;
        JilContent = MockJilMigrationService.SampleJil;
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
    }

    [RelayCommand]
    private void LoadSample()
    {
        JilContent = MockJilMigrationService.SampleJil;
        JilFilePath = "sample.jil";
        WeakReferenceMessenger.Default.Send(new StatusMessage("Loaded sample AutoSys JIL"));
    }

    [RelayCommand]
    private async Task SelectJilFileAsync()
    {
        if (_filePicker is null)
        {
            LoadSample();
            return;
        }

        var content = await _filePicker.PickTextFileAsync("Select AutoSys JIL File", new[] { "*.jil", "*.txt" });
        if (string.IsNullOrWhiteSpace(content))
            return;

        JilContent = content;
        JilFilePath = "imported.jil";
    }

    [RelayCommand]
    private async Task ParseJilAsync()
    {
        if (_migrationService is null)
        {
            ImportedJobs = new ObservableCollection<JilJobPreview>
            {
                new("TRADE_CAPTURE_DAILY", "Command | no deps", 92, "low"),
                new("RISK_EOD_CALC", "Command | 1 dep", 88, "low"),
                new("DTCC_REPORT_GEN", "Command | 2 deps", 78, "medium"),
                new("FILE_WATCH_PRICES", "FileWatcher", 70, "medium")
            };
            RecalculateRisk();
            CurrentStep = 2;
            return;
        }

        IsProcessing = true;
        try
        {
            var jobs = (await _migrationService.ParseJilFileAsync(JilContent)).ToList();
            ImportedJobs.Clear();
            foreach (var job in jobs)
            {
                ImportedJobs.Add(new JilJobPreview(
                    job.JobName,
                    $"{job.JobType} | {job.Condition ?? "no deps"}",
                    CalculateConfidence(job),
                    ClassifyRisk(job)));
            }
            RecalculateRisk();
            CurrentStep = 2;
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new StatusMessage($"JIL parsing failed: {ex.Message}"));
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
            if (_migrationService is null)
            {
                foreach (var job in ImportedJobs)
                    ConversionResults.Add(new ConversionResult(job.JobName, "success", job.Confidence, Guid.NewGuid().ToString("N")[..8], job.RiskLevel == "low" ? "Validation skipped (low risk)" : "Needs validation"));
                CurrentStep = 3;
                return;
            }

            foreach (var preview in ImportedJobs.ToList())
            {
                var result = await ConvertSingleJobAsync(preview);
                ConversionResults.Add(result);
            }
            CurrentStep = 3;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task StartMigrationAsync()
    {
        IsProcessing = true;
        try
        {
            ImportedWorkflowCount = 0;
            if (_workflowService is not null && _migrationService is not null)
            {
                foreach (var result in ConversionResults.Where(r => r.Status == "success"))
                {
                    var job = new JilJob { JobName = result.JobName };
                    var converted = await _migrationService.ConvertJobAsync(job);
                    if (converted.ConvertedWorkflow is not null)
                    {
                        converted.ConvertedWorkflow.Status = WorkflowStatus.Draft;
                        await _workflowService.CreateWorkflowAsync(converted.ConvertedWorkflow);
                        ImportedWorkflowCount++;
                    }
                }
            }
            else
            {
                ImportedWorkflowCount = ConversionResults.Count(r => r.Status == "success");
            }

            CurrentStep = 4;
            WeakReferenceMessenger.Default.Send(new StatusMessage($"Imported {ImportedWorkflowCount} workflows as drafts"));
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 1)
            CurrentStep--;
    }

    [RelayCommand]
    private void Reset()
    {
        CurrentStep = 1;
        ConversionResults.Clear();
        ImportedJobs.Clear();
        ImportedWorkflowCount = 0;
    }

    [RelayCommand]
    private void OpenDrafts()
    {
        WeakReferenceMessenger.Default.Send(new NavigateRequest("Workflows", Filter: "Draft"));
    }

    private async Task<ConversionResult> ConvertSingleJobAsync(JilJobPreview job)
    {
        try
        {
            var jilJob = new JilJob { JobName = job.JobName, Command = "migrated", JobType = JilJobType.Command };
            var conversionResult = await _migrationService!.ConvertJobAsync(jilJob);
            return new ConversionResult(
                job.JobName,
                "success",
                job.Confidence,
                conversionResult.ConvertedWorkflow?.Id.ToString("N")[..8] ?? "",
                job.RiskLevel == "low" && SkipValidationForLowRisk ? "Validation skipped (low risk)" : "Needs validation");
        }
        catch (Exception ex)
        {
            return new ConversionResult(job.JobName, "failed", 0, "", ex.Message);
        }
    }

    private void RecalculateRisk()
    {
        HighRiskCount = ImportedJobs.Count(j => j.RiskLevel == "high");
        MediumRiskCount = ImportedJobs.Count(j => j.RiskLevel == "medium");
        LowRiskCount = ImportedJobs.Count(j => j.RiskLevel == "low");
        OverallConfidence = ImportedJobs.Count == 0 ? 0 : ImportedJobs.Average(j => j.Confidence);
    }

    private static string ClassifyRisk(JilJob job)
    {
        var riskScore = 0;
        var depCount = job.Condition?.Split('&', '|').Length ?? 0;
        if (depCount > 5) riskScore += 2;
        else if (depCount > 2) riskScore += 1;
        if (job.Command?.Contains("custom", StringComparison.OrdinalIgnoreCase) == true ||
            job.Command?.Contains("script", StringComparison.OrdinalIgnoreCase) == true)
            riskScore += 2;
        if (job.JobType == JilJobType.FileWatcher) riskScore += 1;
        if (job.JobType == JilJobType.Command && depCount <= 2) riskScore -= 1;
        return riskScore >= 3 ? "high" : riskScore >= 1 ? "medium" : "low";
    }

    private static double CalculateConfidence(JilJob job)
    {
        var baseConfidence = 100.0;
        if (job.JobType == JilJobType.FileWatcher) baseConfidence -= 10;
        else if (job.JobType != JilJobType.Command) baseConfidence -= 20;
        var depCount = job.Condition?.Split('&', '|').Length ?? 0;
        if (depCount > 5) baseConfidence -= 15;
        if (string.IsNullOrEmpty(job.Command) && job.JobType != JilJobType.FileWatcher) baseConfidence -= 25;
        return Math.Clamp(baseConfidence, 0, 100);
    }
}

public record JilJobPreview(string JobName, string JobType, double Confidence, string RiskLevel)
{
    public string ConfidenceText => $"{Confidence:F0}%";
    public string RiskText => RiskLevel.ToUpperInvariant();
}

public record ConversionResult(string JobName, string Status, double Confidence, string WorkflowId, string Notes);
