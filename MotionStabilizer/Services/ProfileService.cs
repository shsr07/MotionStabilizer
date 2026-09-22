using System.Windows;
using MotionStabilizer.Models;
using MotionStabilizer.Views;

namespace MotionStabilizer.Services;

/// <summary>
/// Encapsulates all profile (配置文件) operations: save, load, delete.
/// Handles dialog UI, config persistence, and UI refresh in one place
/// so that MainWindow and OptionsPage don't duplicate the same logic.
/// </summary>
public static class ProfileService
{
    /// <summary>
    /// Show an input dialog and save current settings as a named profile.
    /// Rejects empty names and asks for confirmation before overwriting
    /// an existing profile.
    /// </summary>
    public static void SaveProfile()
    {
        var title = (string)Application.Current.Resources["Options_SaveProfile"];
        var label = (string)Application.Current.Resources["Options_ProfileName"];

        // No pre-filled name. "Default" used to be pre-filled, which made users
        // believe they were creating a preset when they were actually overwriting
        // their live current configuration.
        var dialog = new InputDialog(title, label, "");

        if (dialog.ShowDialog() == true)
        {
            string name = dialog.InputText?.Trim() ?? "";

            // Reject empty names
            if (string.IsNullOrWhiteSpace(name))
            {
                CustomMessageBox.Show(
                    (string)Application.Current.Resources["ProfileSave_EmptyTitle"],
                    (string)Application.Current.Resources["ProfileSave_EmptyMsg"],
                    (string)Application.Current.Resources["Common_OK"]);
                return;
            }

            // Reject the reserved working-profile name: it is the live current
            // configuration, not a preset. Letting someone save to it would make
            // their working settings and the profile list collide.
            if (string.Equals(name, ConfigManager.WorkingProfileName, StringComparison.OrdinalIgnoreCase))
            {
                CustomMessageBox.Show(
                    (string)Application.Current.Resources["ProfileSave_ReservedTitle"],
                    (string)Application.Current.Resources["ProfileSave_ReservedMsg"],
                    (string)Application.Current.Resources["Common_OK"]);
                return;
            }

            // Confirm before overwriting an existing profile
            string safeName = ConfigManager.SanitizeName(name);
            if (ConfigManager.ListProfiles().Contains(safeName))
            {
                var confirmTitle = (string)Application.Current.Resources["ProfileSave_OverwriteTitle"];
                var confirmMsg = string.Format(
                    (string)Application.Current.Resources["ProfileSave_OverwriteMsg"], name);
                var noText = (string)Application.Current.Resources["Options_ResetConfirmNo"];
                var yesText = (string)Application.Current.Resources["ProfileSave_OverwriteYes"];

                var result = CustomMessageBox.Show(confirmTitle, confirmMsg, noText, yesText);
                if (result != CustomMessageBox.Result.Option2)
                    return;
            }

            var profile = new ProfileData
            {
                ProfileName = name,
                Overlay = App.Config.Overlay,
                Crosshair = App.Config.Crosshair,
                Clock = App.Config.Clock
            };
            ConfigManager.SaveProfile(profile);
        }
    }

    /// <summary>
    /// Show a selection dialog and load the chosen profile into the app.
    /// </summary>
    public static void LoadProfile()
    {
        var profiles = ConfigManager.ListProfiles();
        if (profiles.Count == 0)
        {
            // With the working profile excluded from the list, "nothing saved yet"
            // is now reachable — say so instead of the button doing nothing.
            CustomMessageBox.Show(
                (string)Application.Current.Resources["ProfileList_EmptyTitle"],
                (string)Application.Current.Resources["ProfileList_EmptyMsg"],
                (string)Application.Current.Resources["Common_OK"]);
            return;
        }

        var dialog = new ProfileSelectDialog(
            profiles,
            hintText: (string)(Application.Current.TryFindResource("ProfileLoad_ReplacesCurrent") ?? ""));
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedProfile))
        {
            // Loading is destructive — it overwrites the live current
            // configuration — so it needs an explicit go-ahead every time until
            // the user ticks "don't show again".
            if (!ConfirmLoadReplaces(dialog.SelectedProfile))
                return;

            var profile = ConfigManager.LoadProfile(dialog.SelectedProfile, out string? corruptPath);
            if (profile != null)
            {
                // ConfigStore.ApplyProfile replaces the config objects and fires
                // Changed events, which automatically triggers RefreshOverlay + AutoSave.
                App.Config.ApplyProfile(profile);
                App.MainWin?.NotifyConfigChanged();
            }
            else if (corruptPath != null)
            {
                // Unreadable preset — it was quarantined, so tell the user where.
                string msg = string.Format(
                        (string)Application.Current.Resources["Error_ProfileCorrupt_Msg"], corruptPath)
                    .Replace("\\n", "\n");
                CustomMessageBox.Show(
                    (string)Application.Current.Resources["Error_ProfileCorrupt_Title"],
                    msg,
                    (string)Application.Current.Resources["Common_OK"]);
            }
        }
    }

    /// <summary>
    /// Confirm before a preset overwrites the live current configuration.
    /// Returns true when loading may proceed; cancelling AND dismissing both
    /// abort. "Don't show again" is persisted, and a factory reset re-arms it.
    /// </summary>
    private static bool ConfirmLoadReplaces(string profileName)
    {
        if (App.AppConfig.LoadProfileWarningAcknowledged) return true;

        var title = (string)Application.Current.Resources["ProfileLoad_ConfirmTitle"];
        var msg = string.Format(
            (string)Application.Current.Resources["ProfileLoad_ConfirmMsg"], profileName);
        var yesText = (string)Application.Current.Resources["ProfileLoad_ConfirmYes"];
        var noText = (string)Application.Current.Resources["Common_Cancel"];
        var dontShowText = (string)Application.Current.Resources["Common_DontShowAgain"];

        // Option2 is the confirm button (same ordering as the overwrite prompt).
        var result = CustomMessageBox.Show(title, msg, noText, yesText, dontShowText, out bool dontShowAgain);
        if (result != CustomMessageBox.Result.Option2) return false;

        if (dontShowAgain)
        {
            App.AppConfig.LoadProfileWarningAcknowledged = true;
            // Persist immediately — the debounced auto-save only writes profiles,
            // it never saves AppConfig.
            ConfigManager.SaveAppConfig(App.AppConfig);
        }
        return true;
    }

    /// <summary>
    /// Show a selection dialog, then a confirmation warning, and delete
    /// the chosen profile if the user confirms.
    /// </summary>
    public static void DeleteProfile()
    {
        var profiles = ConfigManager.ListProfiles();
        if (profiles.Count == 0)
        {
            CustomMessageBox.Show(
                (string)Application.Current.Resources["ProfileList_EmptyTitle"],
                (string)Application.Current.Resources["ProfileList_EmptyMsg"],
                (string)Application.Current.Resources["Common_OK"]);
            return;
        }

        var dialogTitle = (string)Application.Current.Resources["ProfileDelete_Title"];
        var confirmText = (string)Application.Current.Resources["ProfileDelete_Confirm"];
        var dialog = new ProfileSelectDialog(profiles, dialogTitle, confirmText);

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedProfile))
        {
            var title = (string)Application.Current.Resources["ProfileDelete_Title"];
            var msgTemplate = (string)Application.Current.Resources["ProfileDelete_Msg"];
            var msg = string.Format(msgTemplate, dialog.SelectedProfile);
            var noText = (string)Application.Current.Resources["Options_ResetConfirmNo"];
            var yesText = (string)Application.Current.Resources["ProfileDelete_ConfirmYes"];

            var result = CustomMessageBox.Show(title, msg, noText, yesText);
            if (result == CustomMessageBox.Result.Option2)
            {
                ConfigManager.DeleteProfile(dialog.SelectedProfile);
            }
        }
    }
}
