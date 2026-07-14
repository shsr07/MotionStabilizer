using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MotionStabilizer.Models;

namespace MotionStabilizer.Services;

/// <summary>
/// Manages saving and loading of configuration profiles.
/// Profiles include Overlay, Crosshair, and Clock settings (NOT hotkeys).
/// Also manages the global AppConfig.
/// </summary>
public class ConfigManager
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MotionStabilizer");

    private static readonly string ProfilesDir = Path.Combine(AppDataDir, "Profiles");
    private static readonly string AppConfigPath = Path.Combine(AppDataDir, "appconfig.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static ConfigManager()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(ProfilesDir);
    }

    /// <summary>Save a profile to disk.</summary>
    public static void SaveProfile(ProfileData profile)
    {
        string path = Path.Combine(ProfilesDir, $"{SanitizeName(profile.ProfileName)}.json");
        string json = JsonSerializer.Serialize(profile, JsonOpts);
        File.WriteAllText(path, json);
    }

    /// <summary>Load a profile by name. Returns null if not found.</summary>
    public static ProfileData? LoadProfile(string profileName)
    {
        string path = Path.Combine(ProfilesDir, $"{SanitizeName(profileName)}.json");
        if (!File.Exists(path)) return null;
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProfileData>(json, JsonOpts);
    }

    /// <summary>List all saved profile names.</summary>
    public static List<string> ListProfiles()
    {
        var files = Directory.GetFiles(ProfilesDir, "*.json");
        var names = new List<string>();
        foreach (var f in files)
            names.Add(Path.GetFileNameWithoutExtension(f));
        return names;
    }

    /// <summary>Delete a profile by name.</summary>
    public static void DeleteProfile(string profileName)
    {
        string path = Path.Combine(ProfilesDir, $"{SanitizeName(profileName)}.json");
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Save the global app config (options + hotkeys are NOT saved to profile).</summary>
    public static void SaveAppConfig(AppConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(AppConfigPath, json);
    }

    /// <summary>Load the global app config.</summary>
    public static AppConfig LoadAppConfig()
    {
        if (!File.Exists(AppConfigPath)) return new AppConfig();
        try
        {
            string json = File.ReadAllText(AppConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <summary>Save hotkeys separately (they persist between sessions but are NOT part of profiles).</summary>
    private static readonly string HotkeyPath = Path.Combine(AppDataDir, "hotkeys.json");

    public static void SaveHotkeys(HotkeyConfig hotkeys)
    {
        string json = JsonSerializer.Serialize(hotkeys, JsonOpts);
        File.WriteAllText(HotkeyPath, json);
    }

    public static HotkeyConfig LoadHotkeys()
    {
        if (!File.Exists(HotkeyPath)) return new HotkeyConfig();
        try
        {
            string json = File.ReadAllText(HotkeyPath);
            return JsonSerializer.Deserialize<HotkeyConfig>(json, JsonOpts) ?? new HotkeyConfig();
        }
        catch
        {
            return new HotkeyConfig();
        }
    }

    private static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
