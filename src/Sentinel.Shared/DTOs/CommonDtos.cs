namespace Sentinel.Shared.DTOs;

/// <summary>
/// Standard API response wrapper for consistent error handling.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public Dictionary<string, string[]>? ValidationErrors { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string message, string? code = null) =>
        new() { Success = false, ErrorMessage = message, ErrorCode = code };
}

/// <summary>
/// Paginated response for list endpoints.
/// </summary>
public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => Page * PageSize < TotalCount;
}

/// <summary>
/// Paged query parameters.
/// </summary>
public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public string? Filter { get; set; }
}

/// <summary>
/// Dashboard summary statistics DTO.
/// </summary>
public class DashboardSummaryDto
{
    public int ActiveWorkflows { get; set; }
    public int RunningJobs { get; set; }
    public double SuccessRate { get; set; }
    public int PendingAlerts { get; set; }
    public int TotalRunsToday { get; set; }
    public int FailedRunsToday { get; set; }
    public TimeSpan? AverageRunDuration { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Migration progress DTO for real-time updates.
/// </summary>
public class MigrationProgressDto
{
    public int TotalJobs { get; set; }
    public int ProcessedJobs { get; set; }
    public int SuccessfulJobs { get; set; }
    public int FailedJobs { get; set; }
    public double ProgressPercent => TotalJobs > 0 ? (double)ProcessedJobs / TotalJobs * 100 : 0;
    public string CurrentJobName { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
}
