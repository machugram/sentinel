using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sentinel.Core.Models;
using System.Globalization;

namespace Sentinel.Desktop.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BrushFor(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static IBrush BrushFor(object? value) => value switch
    {
        RunStatus.Success or WorkflowStatus.Active or "Success" or "Active" => Solid("#2E7D32"),
        RunStatus.Running or "Running" => Solid("#1976D2"),
        RunStatus.Failed or WorkflowStatus.Failed or "Failed" => Solid("#C62828"),
        RunStatus.Pending or WorkflowStatus.Draft or "Pending" or "Draft" => Solid("#F9A825"),
        RunStatus.Cancelled or WorkflowStatus.Archived or "Cancelled" or "Archived" => Solid("#757575"),
        RunStatus.TimedOut or "TimedOut" => Solid("#E65100"),
        WorkflowStatus.Paused or "Paused" => Solid("#E65100"),
        _ => Solid("#9E9E9E")
    };

    internal static IBrush BackgroundFor(object? value) => value switch
    {
        RunStatus.Success or WorkflowStatus.Active or "Success" or "Active" => Solid("#332E7D32"),
        RunStatus.Running or "Running" => Solid("#331976D2"),
        RunStatus.Failed or WorkflowStatus.Failed or "Failed" => Solid("#33C62828"),
        RunStatus.Pending or WorkflowStatus.Draft or "Pending" or "Draft" => Solid("#33F9A825"),
        RunStatus.Cancelled or WorkflowStatus.Archived or "Cancelled" or "Archived" => Solid("#33757575"),
        RunStatus.TimedOut or "TimedOut" => Solid("#33E65100"),
        WorkflowStatus.Paused or "Paused" => Solid("#33E65100"),
        _ => Solid("#339E9E9E")
    };

    private static SolidColorBrush Solid(string hex) => new(Color.Parse(hex));
}

public class StatusToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => StatusToBrushConverter.BackgroundFor(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class SeverityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BrushFor(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static IBrush BrushFor(object? value) => value switch
    {
        AlertSeverity.Critical or "Critical" => new SolidColorBrush(Color.Parse("#C62828")),
        AlertSeverity.Warning or "Warning" => new SolidColorBrush(Color.Parse("#E65100")),
        AlertSeverity.Info or "Info" => new SolidColorBrush(Color.Parse("#1976D2")),
        _ => new SolidColorBrush(Color.Parse("#9E9E9E"))
    };

    internal static IBrush BackgroundFor(object? value) => value switch
    {
        AlertSeverity.Critical or "Critical" => new SolidColorBrush(Color.Parse("#33C62828")),
        AlertSeverity.Warning or "Warning" => new SolidColorBrush(Color.Parse("#33E65100")),
        AlertSeverity.Info or "Info" => new SolidColorBrush(Color.Parse("#331976D2")),
        _ => new SolidColorBrush(Color.Parse("#339E9E9E"))
    };
}

public class SeverityToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => SeverityToBrushConverter.BackgroundFor(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

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
            return dt.ToLocalTime().ToString("MMM dd, HH:mm");
        }
        return "—";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PercentageToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value switch
        {
            double d => d,
            int i => i,
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

public class IconKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || Application.Current is null)
            return null;

        if (Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource))
            return resource;

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DurationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan span)
        {
            if (span.TotalSeconds < 60) return $"{span.Seconds}s";
            if (span.TotalMinutes < 60) return $"{span.Minutes}m {span.Seconds}s";
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }
        return "In progress";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NegateConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is false;
}
