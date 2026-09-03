using System.Globalization;
using System.Text.RegularExpressions;

namespace Sentinel.Desktop.Services;

/// <summary>
/// Converts between cron expressions and short English schedules for operators.
/// </summary>
public static class ScheduleText
{
    public static string ToHuman(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron) || cron == "—")
            return "Manual";

        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            return cron;

        var minute = parts[0];
        var hour = parts[1];
        var day = parts[2];
        var month = parts[3];
        var dow = parts[4];

        if (minute.StartsWith("*/", StringComparison.Ordinal) && hour == "*" && day == "*" && month == "*" && IsEveryDay(dow)
            && int.TryParse(minute[2..], out var everyMin))
            return everyMin == 1 ? "Every minute" : $"Every {everyMin} minutes";

        if (hour.StartsWith("*/", StringComparison.Ordinal) && minute == "0" && day == "*" && month == "*" && IsEveryDay(dow)
            && int.TryParse(hour[2..], out var everyHour))
            return everyHour == 1 ? "Every hour" : $"Every {everyHour} hours";

        if (minute == "0" && hour == "*" && day == "*" && month == "*" && IsEveryDay(dow))
            return "Every hour";

        if (int.TryParse(minute, out var m) && int.TryParse(hour, out var h) && month == "*")
        {
            var time = FormatTime(h, m);
            if (day == "*" && IsWeekdays(dow))
                return $"Weekdays at {time}";
            if (day == "*" && IsEveryDay(dow))
                return h == 0 && m == 0 ? "Daily at midnight" : $"Daily at {time}";
            if (int.TryParse(day, out var d) && IsEveryDay(dow))
                return $"Monthly on day {d} at {time}";
        }

        return cron;
    }

    public static bool TryParse(string? input, out string cron, out string human)
    {
        cron = string.Empty;
        human = "Manual";
        if (string.IsNullOrWhiteSpace(input))
        {
            cron = string.Empty;
            return true;
        }

        var text = Regex.Replace(input.Trim(), @"\s+", " ");

        var cronParts = text.Split(' ');
        if (cronParts.Length is 5 or 6 && LooksLikeCron(cronParts))
        {
            cron = string.Join(' ', cronParts.Take(5));
            human = ToHuman(cron);
            return true;
        }

        var lower = text.ToLowerInvariant();

        if (lower is "manual" or "none" or "on demand" or "on-demand")
        {
            cron = string.Empty;
            human = "Manual";
            return true;
        }

        if (lower is "hourly" or "every hour")
        {
            cron = "0 * * * *";
            human = ToHuman(cron);
            return true;
        }

        if (lower is "midnight" or "daily at midnight" or "every day at midnight")
        {
            cron = "0 0 * * *";
            human = ToHuman(cron);
            return true;
        }

        var everyMinutes = Regex.Match(lower, @"^every (\d+) minutes?$");
        if (everyMinutes.Success && int.TryParse(everyMinutes.Groups[1].Value, out var mins) && mins is > 0 and <= 59)
        {
            cron = $"*/{mins} * * * *";
            human = ToHuman(cron);
            return true;
        }

        var everyHours = Regex.Match(lower, @"^every (\d+) hours?$");
        if (everyHours.Success && int.TryParse(everyHours.Groups[1].Value, out var hours) && hours is > 0 and <= 23)
        {
            cron = $"0 */{hours} * * *";
            human = ToHuman(cron);
            return true;
        }

        var weekdays = Regex.Match(lower, @"^weekdays? at (.+)$");
        if (weekdays.Success && TryParseTime(weekdays.Groups[1].Value, out var wh, out var wm))
        {
            cron = $"{wm} {wh} * * 1-5";
            human = ToHuman(cron);
            return true;
        }

        var daily = Regex.Match(lower, @"^(?:daily|every day) at (.+)$");
        if (daily.Success && TryParseTime(daily.Groups[1].Value, out var dh, out var dm))
        {
            cron = $"{dm} {dh} * * *";
            human = ToHuman(cron);
            return true;
        }

        var monthly = Regex.Match(lower, @"^(?:first of(?: the)? month|monthly on day 1) at (.+)$");
        if (monthly.Success && TryParseTime(monthly.Groups[1].Value, out var mh, out var mm))
        {
            cron = $"{mm} {mh} 1 * *";
            human = ToHuman(cron);
            return true;
        }

        var monthlyDay = Regex.Match(lower, @"^monthly on day (\d+) at (.+)$");
        if (monthlyDay.Success
            && int.TryParse(monthlyDay.Groups[1].Value, out var dayNum)
            && dayNum is >= 1 and <= 28
            && TryParseTime(monthlyDay.Groups[2].Value, out mh, out mm))
        {
            cron = $"{mm} {mh} {dayNum} * *";
            human = ToHuman(cron);
            return true;
        }

        return false;
    }

    public static bool TryParseTime(string text, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        var value = text.Trim().ToLowerInvariant().Replace(".", "");
        value = value.Replace(" ", "");

        var am = value.EndsWith("am", StringComparison.Ordinal);
        var pm = value.EndsWith("pm", StringComparison.Ordinal);
        if (am || pm)
            value = value[..^2];

        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var time))
        {
            hour = time.Hour;
            minute = time.Minute;
        }
        else if (int.TryParse(value, out var hourOnly) && hourOnly is >= 0 and <= 23)
        {
            hour = hourOnly;
            minute = 0;
        }
        else
        {
            return false;
        }

        if (am || pm)
        {
            if (hour is < 1 or > 12)
                return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
            if (hour == 12)
                hour = 0;
            if (pm)
                hour += 12;
        }

        return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
    }

    private static string FormatTime(int hour, int minute)
    {
        var period = hour >= 12 ? "PM" : "AM";
        var h12 = hour % 12;
        if (h12 == 0) h12 = 12;
        return minute == 0 ? $"{h12}:00 {period}" : $"{h12}:{minute:D2} {period}";
    }

    private static bool IsEveryDay(string dow) => dow is "*" or "0-6" or "1-7";
    private static bool IsWeekdays(string dow) => dow is "1-5" or "MON-FRI" or "mon-fri";

    private static bool LooksLikeCron(string[] parts)
    {
        static bool Token(string value) =>
            value.Contains('*') || value.Contains('/') || value.Contains('-') || value.Contains(',')
            || int.TryParse(value, out _);

        return parts.Take(5).All(Token);
    }
}
