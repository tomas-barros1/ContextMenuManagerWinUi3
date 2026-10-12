using System.Text.Json;

namespace ContextMenuManager.Services;

/// <summary>
/// Simple file-backed key-value app settings (stored in %LOCALAPPDATA%\ContextMenuManager\settings.json).
/// Used instead of Windows.Storage.ApplicationData, which requires package identity.
/// </summary>
public static class AppConfig
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ContextMenuManager");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly Lazy<Dictionary<string, string>> Values = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch
        {
            // Corrupt settings fall back to defaults.
        }
        return new Dictionary<string, string>();
    }

    public static string? Get(string key) => Values.Value.TryGetValue(key, out string? value) ? value : null;

    public static void Set(string key, string? value)
    {
        if (value == null)
        {
            Values.Value.Remove(key);
        }
        else
        {
            Values.Value[key] = value;
        }

        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Values.Value));
        }
        catch
        {
            // Persisting settings is best-effort.
        }
    }
}
