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

        var dialog = new InputDialog(title, label, "Default");

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
        if (profiles.Count == 0) return;

        var dialog = new ProfileSelectDialog(profiles);
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedProfile))
        {
            var profile = ConfigManager.LoadProfile(dialog.SelectedProfile);
            if (profile != null)
            {
                // ConfigStore.ApplyProfile replaces the config objects and fires
                // Changed events, which automatically triggers RefreshOverlay + AutoSave.
                App.Config.ApplyProfile(profile);
                App.MainWin?.NotifyConfigChanged();
            }
        }
    }

    /// <summary>
    /// Show a selection dialog, then a confirmation warning, and delete
    /// the chosen profile if the user confirms.
    /// </summary>
    public static void DeleteProfile()
    {
        var profiles = ConfigManager.ListProfiles();
        if (profiles.Count == 0) return;

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
