using System.Text.Json;
using Sentinel.Desktop.Models;

namespace Sentinel.Desktop.Services;

public static class AppConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "app-settings.json");

    public static AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppConfiguration();
            return JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(FilePath), JsonOptions)
                   ?? new AppConfiguration();
        }
        catch
        {
            return new AppConfiguration();
        }
    }

    public static void Save(AppConfiguration config)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(config, JsonOptions));
    }
}
