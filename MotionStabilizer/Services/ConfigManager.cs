using System.Diagnostics;
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

    /// <summary>Data directory for config + log files (%LocalAppData%\MotionStabilizer).</summary>
    internal static string DataDirectory => AppDataDir;

    /// <summary>
    /// Name of the always-on working profile. This is NOT a user-facing preset:
    /// it is the live "current configuration" that auto-save writes on every
    /// change. Named profiles are read-only snapshots by comparison. It is kept
    /// out of the profile list so users never mistake it for a preset they can
    /// load or delete.
    /// </summary>
    public const string WorkingProfileName = "Default";

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

    /// <summary>Load a profile by name. Returns null if not found or unreadable.</summary>
    public static ProfileData? LoadProfile(string profileName) => LoadProfile(profileName, out _);

    /// <summary>
    /// Load a profile by name. Returns null both when the file is missing (first
    /// run — perfectly normal) and when it cannot be parsed. A corrupt file must
    /// never surface as an exception: at startup that would abort the whole app,
    /// and this used to be the one failure path that never reached error.log.
    /// <paramref name="quarantinedPath"/> receives the backup path when an
    /// unreadable file was moved aside, or null otherwise — callers use it to
    /// tell the user what happened instead of silently resetting their settings.
    /// </summary>
    public static ProfileData? LoadProfile(string profileName, out string? quarantinedPath)
    {
        quarantinedPath = null;
        string path = Path.Combine(ProfilesDir, $"{SanitizeName(profileName)}.json");
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProfileData>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Profile '{profileName}' is unreadable: {ex.Message}");
            quarantinedPath = TryQuarantineCorruptProfile(path);
            return null;
        }
    }

    /// <summary>
    /// Move an unreadable profile out of the way so the next launch starts clean,
    /// keeping the original bytes around for manual recovery. Best-effort: a
    /// failure here must never become a second reason to fail startup.
    /// Returns the backup path, or null when the move failed.
    /// </summary>
    private static string? TryQuarantineCorruptProfile(string path)
    {
        try
        {
            string backup = Path.ChangeExtension(path, $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(path, backup);
            return backup;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not quarantine corrupt profile '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// List user-saved profile names. The working profile is excluded on purpose:
    /// it is the live current configuration, not a preset — loading it is a no-op
    /// and deleting it would throw away the user's current settings. Quarantined
    /// corrupt files (.corrupt-*.json) are excluded as well.
    /// </summary>
    public static List<string> ListProfiles()
    {
        var files = Directory.GetFiles(ProfilesDir, "*.json");
        var names = new List<string>();
        foreach (var f in files)
        {
            string name = Path.GetFileNameWithoutExtension(f);
            if (string.Equals(name, WorkingProfileName, StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains(".corrupt-", StringComparison.OrdinalIgnoreCase)) continue;
            names.Add(name);
        }
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

    /// <summary>Replace characters that are invalid in file names with '_'.</summary>
    public static string SanitizeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
