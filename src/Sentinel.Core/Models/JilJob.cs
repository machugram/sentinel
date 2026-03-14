namespace Sentinel.Core.Models;

/// <summary>
/// Represents a JIL (Job Information Language) job imported from AutoSys.
/// Based on PRD §5.5 - JIL Migration Subsystem.
/// </summary>
public class JilJob
{
    public string JobName { get; set; } = string.Empty;
    public JilJobType JobType { get; set; }
    public string? Command { get; set; }
    public string? Machine { get; set; }
    public string? Owner { get; set; }
    public string? StartTimes { get; set; }
    public string? Condition { get; set; }
    public string? BoxName { get; set; }
    public string? Description { get; set; }
    public int? MaxRunAlarm { get; set; }
    public int? MinRunAlarm { get; set; }
    public string? StdOutFile { get; set; }
    public string? StdErrFile { get; set; }
    public Dictionary<string, string> RawAttributes { get; set; } = new();
}

/// <summary>
/// Represents the result of converting a JIL job to a Sentinel workflow.
/// </summary>
public class JilConversionResult
{
    public JilJob SourceJob { get; set; } = null!;
    public Workflow? ConvertedWorkflow { get; set; }
    public ConversionConfidence Confidence { get; set; }
    public List<ConversionIssue> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool RequiresManualReview => Confidence != ConversionConfidence.High || Issues.Any();
}

/// <summary>
/// Represents an issue found during JIL conversion.
/// </summary>
public class ConversionIssue
{
    public IssueSeverity Severity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Attribute { get; set; }
    public string? SuggestedFix { get; set; }
}

public enum JilJobType
{
    Command,
    Box,
    FileWatcher
}

public enum ConversionConfidence
{
    High,
    Medium,
    Low,
    Failed
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error
}
