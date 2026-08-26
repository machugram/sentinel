using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Sentinel.Desktop.Services;

public interface IFilePickerService
{
    Task<string?> PickTextFileAsync(string title, IReadOnlyList<string> patterns);
}

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickTextFileAsync(string title, IReadOnlyList<string> patterns)
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
            return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Job files") { Patterns = patterns.ToList() }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return null;

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}

public static class ThemeManager
{
    public static void Apply(string theme)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? Avalonia.Styling.ThemeVariant.Light
            : Avalonia.Styling.ThemeVariant.Dark;
    }
}
