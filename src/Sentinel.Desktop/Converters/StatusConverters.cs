using Avalonia.Data.Converters;
using Avalonia.Media;
using Sentinel.Core.Models;
using System.Globalization;

namespace Sentinel.Desktop.Converters;

/// <summary>
/// Converts RunStatus enum to a display-friendly color brush.
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            RunStatus.Success => new SolidColorBrush(Color.Parse("#2E7D32")),
            RunStatus.Running => new SolidColorBrush(Color.Parse("#1976D2")),
            RunStatus.Failed => new SolidColorBrush(Color.Parse("#C62828")),
            RunStatus.Pending => new SolidColorBrush(Color.Parse("#F9A825")),
            RunStatus.Cancelled => new SolidColorBrush(Color.Parse("#757575")),
            RunStatus.TimedOut => new SolidColorBrush(Color.Parse("#E65100")),
            _ => new SolidColorBrush(Color.Parse("#9E9E9E"))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts AlertSeverity to a color brush.
/// </summary>
public class SeverityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            AlertSeverity.Critical => new SolidColorBrush(Color.Parse("#C62828")),
            AlertSeverity.Warning => new SolidColorBrush(Color.Parse("#E65100")),
            AlertSeverity.Info => new SolidColorBrush(Color.Parse("#1976D2")),
            _ => new SolidColorBrush(Color.Parse("#9E9E9E"))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a boolean to a visibility-like opacity (1.0 or 0.0).
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = parameter is string s && s == "invert";
        var isTrue = value is true;
        return (invert ? !isTrue : isTrue) ? 1.0 : 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Formats a nullable DateTime as a relative time string.
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            var span = DateTime.UtcNow - dt.ToUniversalTime();
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return dt.ToString("MMM dd, HH:mm");
        }
        return "—";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Formats a percentage value with appropriate color coding.
/// </summary>
public class PercentageToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value switch
        {
            double d => d,
            int i => (double)i,
            _ => 0.0
        };

        return percent switch
        {
            >= 95 => new SolidColorBrush(Color.Parse("#2E7D32")),
            >= 80 => new SolidColorBrush(Color.Parse("#F9A825")),
            _ => new SolidColorBrush(Color.Parse("#C62828"))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
