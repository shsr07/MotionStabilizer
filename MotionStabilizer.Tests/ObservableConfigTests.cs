using System.ComponentModel;
using MotionStabilizer.Models;
using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for <see cref="ObservableObject"/> and <see cref="ConfigStore"/>.
/// Verifies that property changes fire events, ConfigStore aggregates them,
/// and profile application/reset works correctly.
/// </summary>
public class ObservableConfigTests
{
    // ── ObservableObject ──

    [Fact]
    public void SetProperty_FiresPropertyChanged_WhenValueChanged()
    {
        var cfg = new OverlayConfig();
        string? changedProperty = null;
        cfg.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        cfg.IsVisible = true;

        Assert.Equal(nameof(OverlayConfig.IsVisible), changedProperty);
    }

    [Fact]
    public void SetProperty_DoesNotFire_WhenValueUnchanged()
    {
        var cfg = new OverlayConfig { IsVisible = true };
        int fireCount = 0;
        cfg.PropertyChanged += (_, _) => fireCount++;

        cfg.IsVisible = true; // Same value

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void SetProperty_FiresForCorrectProperty()
    {
        var cfg = new OverlayConfig();
        var changedProps = new List<string?>();
        cfg.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        cfg.Opacity = 50;
        cfg.Shape = OverlayShape.Dome;
        cfg.ColorPreset = ColorPreset.Red;

        Assert.Equal(3, changedProps.Count);
        Assert.Contains(nameof(OverlayConfig.Opacity), changedProps);
        Assert.Contains(nameof(OverlayConfig.Shape), changedProps);
        Assert.Contains(nameof(OverlayConfig.ColorPreset), changedProps);
    }

    // ── ConfigStore ──

    [Fact]
    public void ConfigStore_FiresChanged_WhenOverlayPropertyChanges()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        store.Overlay.IsVisible = true;

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ConfigStore_FiresChanged_WhenCrosshairPropertyChanges()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        store.Crosshair.Size = SizePreset.XL;

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ConfigStore_FiresChanged_WhenClockPropertyChanges()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        store.Clock.FontSize = 32;

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ConfigStore_FiresChanged_WhenAppPropertyChanges()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        store.App.Language = Language.English;

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ConfigStore_FiresChanged_WhenConfigObjectReplaced()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        store.Overlay = new OverlayConfig();

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ConfigStore_ReplacedObject_FiresSubsequentChanges()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        // Replace the overlay config
        store.Overlay = new OverlayConfig { IsVisible = true };
        Assert.Equal(1, changeCount); // One event from the replacement

        // Change a property on the new object
        store.Overlay.Opacity = 50;
        Assert.Equal(2, changeCount); // Second event from the property change
    }

    [Fact]
    public void ConfigStore_OldObjectStopsFiring_AfterReplacement()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        var oldOverlay = store.Overlay;
        store.Overlay = new OverlayConfig();
        changeCount = 0; // Reset after replacement

        // Change a property on the OLD object — should NOT fire
        oldOverlay.Opacity = 99;

        Assert.Equal(0, changeCount);
    }

    [Fact]
    public void ConfigStore_ApplyProfile_ReplacesAllThreeConfigs()
    {
        var store = new ConfigStore();
        var profile = new ProfileData
        {
            ProfileName = "Test",
            Overlay = new OverlayConfig { IsVisible = true, Opacity = 42 },
            Crosshair = new CrosshairConfig { IsVisible = true, Size = SizePreset.XXL },
            Clock = new ClockConfig { IsVisible = true, FontSize = 48 }
        };

        store.ApplyProfile(profile);

        Assert.True(store.Overlay.IsVisible);
        Assert.Equal(42, store.Overlay.Opacity);
        Assert.True(store.Crosshair.IsVisible);
        Assert.Equal(SizePreset.XXL, store.Crosshair.Size);
        Assert.True(store.Clock.IsVisible);
        Assert.Equal(48, store.Clock.FontSize);
    }

    [Fact]
    public void ConfigStore_ResetToDefaults_CreatesFreshConfigs()
    {
        var store = new ConfigStore();
        // Modify current configs
        store.Overlay.IsVisible = true;
        store.Overlay.Opacity = 99;
        store.Crosshair.IsVisible = true;
        store.App.Language = Language.English;

        store.ResetToDefaults();

        // Verify defaults are restored
        Assert.False(store.Overlay.IsVisible);
        Assert.Equal(60, store.Overlay.Opacity);
        Assert.False(store.Crosshair.IsVisible);
        Assert.Equal(Language.Chinese, store.App.Language);
    }

    [Fact]
    public void ConfigStore_ResetToDefaults_FiresChangedEvents()
    {
        var store = new ConfigStore();
        int changeCount = 0;
        store.Changed += () => changeCount++;

        store.ResetToDefaults();

        // Should fire at least once for each replaced config (Overlay, Crosshair, Clock, App)
        Assert.True(changeCount >= 4, $"Expected at least 4 change events, got {changeCount}");
    }

    // ── Additional edge cases ──

    [Fact]
    public void ConfigStore_MultipleSubscribers_AllInvoked()
    {
        var store = new ConfigStore();
        int count1 = 0, count2 = 0;
        store.Changed += () => count1++;
        store.Changed += () => count2++;

        store.Overlay.Opacity = 50;

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void ConfigStore_UnsubscribedHandler_StopsReceivingEvents()
    {
        var store = new ConfigStore();
        int count = 0;
        Action handler = () => count++;
        store.Changed += handler;

        store.Overlay.Opacity = 50;
        Assert.Equal(1, count);

        store.Changed -= handler;
        store.Overlay.Opacity = 60;
        Assert.Equal(1, count); // Still 1 — handler was removed
    }

    [Fact]
    public void ConfigStore_SettingSameValue_DoesNotFire()
    {
        var store = new ConfigStore();
        store.Overlay.Opacity = 50; // Set initial value
        int count = 0;
        store.Changed += () => count++;

        store.Overlay.Opacity = 50; // Same value

        Assert.Equal(0, count);
    }

    [Fact]
    public void ConfigStore_ReplacingWithSameReference_DoesNotFire()
    {
        var store = new ConfigStore();
        int count = 0;
        store.Changed += () => count++;

        var current = store.Overlay;
        store.Overlay = current; // Same reference

        Assert.Equal(0, count);
    }

    [Fact]
    public void ConfigStore_RapidChanges_FireMultipleEvents()
    {
        var store = new ConfigStore();
        int count = 0;
        store.Changed += () => count++;

        store.Overlay.Opacity = 10;
        store.Overlay.Opacity = 20;
        store.Overlay.Opacity = 30;

        Assert.Equal(3, count);
    }

    [Fact]
    public void ConfigStore_Hotkeys_NotObservable()
    {
        var store = new ConfigStore();
        int count = 0;
        store.Changed += () => count++;

        // Changing Hotkeys property should not fire Changed (it's not observable)
        store.Hotkeys = new HotkeyConfig();

        Assert.Equal(0, count);
    }

    [Fact]
    public void ConfigStore_ApplyProfile_NullProfile_Throws()
    {
        var store = new ConfigStore();
        Assert.Throws<NullReferenceException>(() => store.ApplyProfile(null!));
    }

    [Fact]
    public void ConfigStore_AllConfigs_StartWithDefaults()
    {
        var store = new ConfigStore();

        Assert.False(store.Overlay.IsVisible);
        Assert.Equal(OverlayShape.Box, store.Overlay.Shape);
        Assert.False(store.Crosshair.IsVisible);
        Assert.False(store.Clock.IsVisible);
        Assert.True(store.App.AutoSaveOnClose);
        Assert.Equal(UIScale.Auto, store.App.Scale);
    }

    [Fact]
    public void ConfigStore_ApplyProfile_PreservesHotkeys()
    {
        var store = new ConfigStore();
        var originalHotkeys = store.Hotkeys;

        store.ApplyProfile(new ProfileData());

        // Hotkeys should not change when applying a profile
        Assert.Same(originalHotkeys, store.Hotkeys);
    }

    [Fact]
    public void ConfigStore_ResetToDefaults_ReplacesHotkeys()
    {
        var store = new ConfigStore();
        var originalHotkeys = store.Hotkeys;

        store.ResetToDefaults();

        // Hotkeys should be a new instance after reset
        Assert.NotSame(originalHotkeys, store.Hotkeys);
    }

    [Fact]
    public void ConfigStore_CrosshairReplaced_SubscribesToNewObject()
    {
        var store = new ConfigStore();
        int count = 0;
        store.Changed += () => count++;

        var newCrosshair = new CrosshairConfig { Size = SizePreset.XXL };
        store.Crosshair = newCrosshair;
        count = 0; // Reset after replacement

        newCrosshair.Opacity = 50;

        Assert.Equal(1, count);
    }

    [Fact]
    public void ConfigStore_ClockReplaced_SubscribesToNewObject()
    {
        var store = new ConfigStore();
        int count = 0;
        store.Changed += () => count++;

        var newClock = new ClockConfig { FontSize = 48 };
        store.Clock = newClock;
        count = 0;

        newClock.Opacity = 50;

        Assert.Equal(1, count);
    }

    [Fact]
    public void ConfigStore_AppReplaced_SubscribesToNewObject()
    {
        var store = new ConfigStore();
        int count = 0;
        store.Changed += () => count++;

        var newApp = new AppConfig { Language = Language.English };
        store.App = newApp;
        count = 0;

        newApp.Scale = UIScale.Percent125;

        Assert.Equal(1, count);
    }
}
