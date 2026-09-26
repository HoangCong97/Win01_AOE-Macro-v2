using System.Text.Json;
using AOEKeyboardMacroPro.Models;

namespace AOEKeyboardMacroPro.Services;

public static class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object _fileLock = new();

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    if (settings.FarmTimerInterval <= 0)
                    {
                        settings.FarmTimerInterval = 200;
                    }
                    settings.ResourceCrop ??= new ResourceCropSettings();
                    settings.PopCrop ??= new PopCropSettings();
                    settings.TimerCrop ??= new TimerCropSettings();
                    settings.Hud ??= new HudSettings();
                    return settings;
                }
            }
        }
        catch
        {
            // If any error occurs reading file, fall back to default settings
        }

        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            lock (_fileLock)
            {
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(ConfigPath, json);
            }
        }
        catch
        {
            // Fail silently on write errors (e.g., read-only filesystem)
        }
    }
}
