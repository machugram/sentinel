using Sentinel.Core.Models;

namespace Sentinel.Core.Interfaces;

/// <summary>
/// Service for JIL (AutoSys) migration operations.
/// Based on PRD §5.5 - JIL Migration Subsystem.
/// </summary>
public interface IJilMigrationService
{
    /// <summary>
    /// Parses a JIL file and extracts job definitions.
    /// </summary>
    Task<IEnumerable<JilJob>> ParseJilFileAsync(string content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Parses a JIL file from a stream.
    /// </summary>
    Task<IEnumerable<JilJob>> ParseJilFileAsync(Stream stream, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Converts a single JIL job to a Sentinel workflow.
    /// </summary>
    Task<JilConversionResult> ConvertJobAsync(JilJob job, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Batch converts multiple JIL jobs to Sentinel workflows.
    /// </summary>
    Task<IEnumerable<JilConversionResult>> ConvertJobsAsync(IEnumerable<JilJob> jobs, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a converted workflow against the original JIL job.
    /// </summary>
    Task<ValidationResult> ValidateConversionAsync(JilJob original, Workflow converted, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the overall migration statistics.
    /// </summary>
    Task<MigrationStatistics> GetMigrationStatisticsAsync(IEnumerable<JilConversionResult> results, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of validation between JIL job and converted workflow.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double EquivalenceScore { get; set; }
}

/// <summary>
/// Statistics about a JIL migration batch.
/// </summary>
public class MigrationStatistics
{
    public int TotalJobs { get; set; }
    public int SuccessfulConversions { get; set; }
    public int HighConfidence { get; set; }
    public int MediumConfidence { get; set; }
    public int LowConfidence { get; set; }
    public int Failed { get; set; }
    public int RequiringManualReview { get; set; }
    public double AutoConversionRate => TotalJobs > 0 
        ? (double)(HighConfidence + MediumConfidence) / TotalJobs * 100 
        : 0;
    public Dictionary<string, int> IssuesByType { get; set; } = new();
}
