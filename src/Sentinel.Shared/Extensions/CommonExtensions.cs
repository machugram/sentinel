namespace Sentinel.Shared.Extensions;

/// <summary>
/// Extension methods for common operations across the Sentinel platform.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Returns a human-readable relative time string (e.g., "2 hours ago", "just now").
    /// </summary>
    public static string ToRelativeTime(this DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime.ToUniversalTime();

        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)}w ago";
        return dateTime.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Formats a TimeSpan as a human-readable duration.
    /// </summary>
    public static string ToReadableDuration(this TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds < 60) return $"{timeSpan.Seconds}s";
        if (timeSpan.TotalMinutes < 60) return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
    }
}

public static class StringExtensions
{
    /// <summary>
    /// Truncates a string to a maximum length with ellipsis.
    /// </summary>
    public static string Truncate(this string value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}

public static class EnumerableExtensions
{
    /// <summary>
    /// Returns a collection or empty if null (null-safe enumeration).
    /// </summary>
    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? source) => source ?? Enumerable.Empty<T>();
}
