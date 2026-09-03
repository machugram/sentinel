using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Infrastructure.Mock;

public sealed class MockJilMigrationService : IJilMigrationService
{
    public const string SampleJil = """
        insert_job: TRADE_CAPTURE_DAILY   job_type: c
        command: /opt/scripts/trade_capture.sh
        machine: prod-batch-01
        owner: trading
        start_times: "00,15,30,45"
        description: Ingest exchange fills

        insert_job: RISK_EOD_CALC   job_type: c
        command: /opt/risk/eod_var.py --book ALL
        machine: risk-grid-02
        owner: risk
        condition: s(TRADE_CAPTURE_DAILY)
        start_times: "17:00"
        description: End of day VaR

        insert_job: DTCC_REPORT_GEN   job_type: c
        command: /opt/reg/dtcc_submit.sh
        machine: reg-rpt-01
        owner: compliance
        condition: s(TRADE_CAPTURE_DAILY) & s(RISK_EOD_CALC)
        start_times: "06:00"
        description: DTCC daily pack

        insert_job: NAV_CALCULATION   job_type: c
        command: /opt/funds/nav.py
        machine: funds-01
        owner: funds
        start_times: "18:00"
        description: Fund NAV

        insert_job: FILE_WATCH_PRICES   job_type: f
        machine: mkt-data-01
        owner: marketdata
        watch_file: /data/vendor/prices/*.csv
        description: Vendor price arrival
        """;

    public Task<IEnumerable<JilJob>> ParseJilFileAsync(string content, CancellationToken cancellationToken = default)
    {
        var source = string.IsNullOrWhiteSpace(content) ? SampleJil : content;
        return Task.FromResult(Parse(source).AsEnumerable());
    }

    public async Task<IEnumerable<JilJob>> ParseJilFileAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return await ParseJilFileAsync(content, cancellationToken);
    }

    public Task<JilConversionResult> ConvertJobAsync(JilJob job, CancellationToken cancellationToken = default)
    {
        var issues = new List<ConversionIssue>();
        if (string.IsNullOrWhiteSpace(job.Command) && job.JobType != JilJobType.FileWatcher)
        {
            issues.Add(new ConversionIssue
            {
                Severity = IssueSeverity.Warning,
                Code = "NO_COMMAND",
                Message = "Job has no command; mapped as a placeholder Shell task.",
                SuggestedFix = "Set a command or convert to FileWatcher."
            });
        }

        var confidence = job.JobType == JilJobType.Command && (job.Condition?.Split('&', '|').Length ?? 0) <= 2
            ? ConversionConfidence.High
            : job.JobType == JilJobType.FileWatcher
                ? ConversionConfidence.Medium
                : ConversionConfidence.Medium;

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            Name = job.JobName.Replace('_', ' '),
            Description = job.Description ?? $"Migrated from AutoSys job {job.JobName}",
            Status = WorkflowStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CronExpression = job.StartTimes,
            Metadata = new Dictionary<string, string>
            {
                ["Source"] = "JIL",
                ["Machine"] = job.Machine ?? "",
                ["Owner"] = job.Owner ?? "",
                ["Condition"] = job.Condition ?? ""
            },
            Tasks =
            {
                new WorkflowTask
                {
                    Id = Guid.NewGuid(),
                    Name = job.JobName,
                    Type = job.JobType == JilJobType.FileWatcher ? TaskType.Custom : TaskType.Shell,
                    Command = job.Command ?? (job.RawAttributes.TryGetValue("watch_file", out var watch) ? watch : job.JobName),
                    Status = Core.Models.TaskStatus.Pending
                }
            }
        };

        if (string.IsNullOrWhiteSpace(workflow.Name) || workflow.Name == job.JobName.ToLowerInvariant())
            workflow.Name = job.JobName;

        return Task.FromResult(new JilConversionResult
        {
            SourceJob = job,
            ConvertedWorkflow = workflow,
            Confidence = confidence,
            Issues = issues
        });
    }

    public async Task<IEnumerable<JilConversionResult>> ConvertJobsAsync(IEnumerable<JilJob> jobs, CancellationToken cancellationToken = default)
    {
        var results = new List<JilConversionResult>();
        foreach (var job in jobs)
            results.Add(await ConvertJobAsync(job, cancellationToken));
        return results;
    }

    public Task<ValidationResult> ValidateConversionAsync(JilJob original, Workflow converted, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        if (converted.Tasks.Count == 0)
            warnings.Add("Converted workflow has no tasks.");
        return Task.FromResult(new ValidationResult
        {
            IsValid = converted.Tasks.Count > 0,
            Warnings = warnings,
            EquivalenceScore = converted.Tasks.Count > 0 ? 0.86 : 0.2
        });
    }

    public Task<MigrationStatistics> GetMigrationStatisticsAsync(IEnumerable<JilConversionResult> results, CancellationToken cancellationToken = default)
    {
        var list = results.ToList();
        return Task.FromResult(new MigrationStatistics
        {
            TotalJobs = list.Count,
            SuccessfulConversions = list.Count(r => r.ConvertedWorkflow != null),
            HighConfidence = list.Count(r => r.Confidence == ConversionConfidence.High),
            MediumConfidence = list.Count(r => r.Confidence == ConversionConfidence.Medium),
            LowConfidence = list.Count(r => r.Confidence == ConversionConfidence.Low),
            Failed = list.Count(r => r.Confidence == ConversionConfidence.Failed),
            RequiringManualReview = list.Count(r => r.RequiresManualReview)
        });
    }

    private static List<JilJob> Parse(string content)
    {
        var jobs = new List<JilJob>();
        JilJob? current = null;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                if (string.IsNullOrWhiteSpace(line) && current != null)
                {
                    jobs.Add(current);
                    current = null;
                }
                continue;
            }

            if (line.StartsWith("insert_job:", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null)
                    jobs.Add(current);

                current = new JilJob
                {
                    JobName = ReadValue(line, "insert_job:") ?? "UNNAMED",
                    JobType = ParseType(line)
                };
                continue;
            }

            if (current is null)
                continue;

            if (line.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
                current.Command = AfterColon(line);
            else if (line.StartsWith("machine:", StringComparison.OrdinalIgnoreCase))
                current.Machine = AfterColon(line);
            else if (line.StartsWith("owner:", StringComparison.OrdinalIgnoreCase))
                current.Owner = AfterColon(line);
            else if (line.StartsWith("start_times:", StringComparison.OrdinalIgnoreCase))
                current.StartTimes = AfterColon(line).Trim('"');
            else if (line.StartsWith("condition:", StringComparison.OrdinalIgnoreCase))
                current.Condition = AfterColon(line);
            else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                current.Description = AfterColon(line);
            else if (line.StartsWith("box_name:", StringComparison.OrdinalIgnoreCase))
                current.BoxName = AfterColon(line);
            else
                current.RawAttributes[line.Split(':')[0]] = AfterColon(line);
        }

        if (current != null)
            jobs.Add(current);

        return jobs;
    }

    private static JilJobType ParseType(string line)
    {
        if (line.Contains("job_type: b", StringComparison.OrdinalIgnoreCase))
            return JilJobType.Box;
        if (line.Contains("job_type: f", StringComparison.OrdinalIgnoreCase))
            return JilJobType.FileWatcher;
        return JilJobType.Command;
    }

    private static string AfterColon(string line)
    {
        var idx = line.IndexOf(':');
        return idx < 0 ? line : line[(idx + 1)..].Trim();
    }

    private static string? ReadValue(string line, string key)
    {
        var start = line.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        var rest = line[(start + key.Length)..].Trim();
        var jobTypeAt = rest.IndexOf("job_type:", StringComparison.OrdinalIgnoreCase);
        if (jobTypeAt >= 0)
            rest = rest[..jobTypeAt].Trim();
        return rest;
    }
}
