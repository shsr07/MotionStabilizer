using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the pure gamepad math in <see cref="XInputInterop"/>:
/// thumb normalization, circular deadzone, stick→velocity / stick→mouse-delta
/// mapping, and the per-axis keyboard &gt; gamepad priority merge.
/// No XInput DLL is loaded — the P/Invoke surface is never touched.
/// </summary>
public class XInputInteropTests
{
    private const float Dz = XInputInterop.DefaultDeadzone; // 0.15

    // ── NormalizeThumb ──

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(32767, 1.0)]
    [InlineData(-32768, -1.0)]
    public void NormalizeThumb_MapsExtremes(short value, double expected)
        => Assert.Equal(expected, (double)XInputInterop.NormalizeThumb(value), 5);

    [Fact]
    public void NormalizeThumb_HalfDeflection()
    {
        Assert.Equal(0.5, (double)XInputInterop.NormalizeThumb(16384), 4);
        Assert.Equal(-0.5, (double)XInputInterop.NormalizeThumb(-16384), 4);
    }

    // ── ApplyDeadzone ──

    [Fact]
    public void ApplyDeadzone_ZeroesBelowDeadzone()
    {
        Assert.Equal((0f, 0f), XInputInterop.ApplyDeadzone(0.1f, 0f, Dz));
        // Magnitude √(0.1² + 0.1²) ≈ 0.1414 < 0.15
        Assert.Equal((0f, 0f), XInputInterop.ApplyDeadzone(0.1f, 0.1f, Dz));
    }

    [Fact]
    public void ApplyDeadzone_ZeroesExactlyOnRim()
    {
        Assert.Equal((0f, 0f), XInputInterop.ApplyDeadzone(Dz, 0f, Dz));
    }

    [Fact]
    public void ApplyDeadzone_JustOutsideRim_StartsNearZero()
    {
        var (x, y) = XInputInterop.ApplyDeadzone(0.16f, 0f, Dz);
        Assert.Equal(0.0118, (double)x, 3);
        Assert.Equal(0f, y);
    }

    [Fact]
    public void ApplyDeadzone_FullPush_PreservesUnitMagnitude()
    {
        Assert.Equal((1f, 0f), XInputInterop.ApplyDeadzone(1f, 0f, Dz));
        Assert.Equal((0f, 1f), XInputInterop.ApplyDeadzone(0f, 1f, Dz));
    }

    [Fact]
    public void ApplyDeadzone_DiagonalUnitMagnitude_PreservesDirection()
    {
        var (x, y) = XInputInterop.ApplyDeadzone(0.7071068f, 0.7071068f, Dz);
        Assert.Equal(0.7071068, (double)x, 5);
        Assert.Equal(0.7071068, (double)y, 5);
    }

    [Fact]
    public void ApplyDeadzone_Overshoot_ClampsToRim()
    {
        // Hypothetical square-range hardware: both axes at 1 → magnitude √2
        var (x, y) = XInputInterop.ApplyDeadzone(1f, 1f, Dz);
        double mag = Math.Sqrt((double)x * x + (double)y * y);
        Assert.Equal(1.0, mag, 5);
        Assert.Equal(0.7071068, (double)x, 5);
        Assert.Equal(0.7071068, (double)y, 5);
    }

    [Fact]
    public void ApplyDeadzone_DeadzoneOfOne_ZeroesEverything()
    {
        Assert.Equal((0f, 0f), XInputInterop.ApplyDeadzone(1f, 1f, 1f));
    }

    // ── StickToVelocity (left stick = WASD role) ──

    [Fact]
    public void StickToVelocity_RightPush_MovesRight()
    {
        var (vx, vy) = XInputInterop.StickToVelocity(1f, 0f, 1.0, inverted: false, baseSpeed: 210f);
        Assert.Equal(210.0, (double)vx, 4);
        Assert.Equal(0.0, (double)vy, 4);
    }

    [Fact]
    public void StickToVelocity_UpPush_MovesUp_OnScreen()
    {
        // Stick +Y is up; screen Y is positive-down → dots move up (negative Y)
        var (vx, vy) = XInputInterop.StickToVelocity(0f, 1f, 1.0, inverted: false, baseSpeed: 210f);
        Assert.Equal(0.0, (double)vx, 4);
        Assert.Equal(-210.0, (double)vy, 4);
    }

    [Fact]
    public void StickToVelocity_Inverted_ReversesBothAxes()
    {
        var (vx, vy) = XInputInterop.StickToVelocity(1f, 1f, 1.0, inverted: true, baseSpeed: 210f);
        Assert.Equal(-210.0, (double)vx, 4);
        Assert.Equal(210.0, (double)vy, 4);
    }

    [Fact]
    public void StickToVelocity_Sensitivity_ClampedLikeKeyboard()
    {
        var (vxHi, _) = XInputInterop.StickToVelocity(1f, 0f, 5.0, inverted: false, baseSpeed: 210f);
        Assert.Equal(630.0, (double)vxHi, 4); // clamped to 3.0

        var (vxLo, _) = XInputInterop.StickToVelocity(1f, 0f, 0.01, inverted: false, baseSpeed: 210f);
        Assert.Equal(10.5, (double)vxLo, 4); // clamped to 0.05
    }

    [Fact]
    public void StickToVelocity_AnalogMagnitude_ScalesSpeed()
    {
        var (vx, _) = XInputInterop.StickToVelocity(0.5f, 0f, 1.0, inverted: false, baseSpeed: 210f);
        Assert.Equal(105.0, (double)vx, 4);
    }

    // ── StickToMouseDelta (right stick = mouse role) ──

    [Fact]
    public void StickToMouseDelta_UpPush_FlipsToScreenCoordinates()
    {
        // Stick up (+Y) must produce a negative raw deltaY like a real mouse moving up
        var (dx, dy) = XInputInterop.StickToMouseDelta(0f, 1f, dt: 1f / 120f, fullDeflectionSpeed: 8000f);
        Assert.Equal(0.0, (double)dx, 5);
        Assert.Equal(-8000.0 / 120.0, (double)dy, 3);
    }

    [Fact]
    public void StickToMouseDelta_ScalesLinearlyWithDt()
    {
        var (dx1, dy1) = XInputInterop.StickToMouseDelta(1f, -1f, dt: 1f / 120f, fullDeflectionSpeed: 8000f);
        var (dx2, dy2) = XInputInterop.StickToMouseDelta(1f, -1f, dt: 1f / 30f, fullDeflectionSpeed: 8000f);
        // dt-compensation guarantee: delta ∝ dt, so velocity (delta/(dt·60)) is cadence-independent
        Assert.Equal((double)dx1 * 4.0, (double)dx2, 5);
        Assert.Equal((double)dy1 * 4.0, (double)dy2, 5);
    }

    // ── MergeAxis (keyboard > gamepad > existing) ──

    [Fact]
    public void MergeAxis_KeyboardWins_OverGamepadAndExisting()
    {
        Assert.Equal(42f, XInputInterop.MergeAxis(10f, 42f, 7f));
    }

    [Fact]
    public void MergeAxis_GamepadWins_WhenKeyboardInactive()
    {
        Assert.Equal(7f, XInputInterop.MergeAxis(10f, 0f, 7f));
    }

    [Fact]
    public void MergeAxis_ExistingPassedThrough_WhenBothInactive()
    {
        Assert.Equal(10f, XInputInterop.MergeAxis(10f, 0f, 0f));
    }
    // ── Stick-drift scenario: deadzone must be raisable past the drift ──

    [Fact]
    public void ApplyDeadzone_RaisedDeadzoneSuppressesDrift()
    {
        // Worn-stick drift of 0.2 magnitude: passes at the 0.15 default
        // (the user complaint), vanishes once the deadzone is raised to 0.3
        Assert.NotEqual((0f, 0f), XInputInterop.ApplyDeadzone(0.2f, 0f, Dz));
        Assert.Equal((0f, 0f), XInputInterop.ApplyDeadzone(0.2f, 0f, 0.3f));
        // Rim rescale still works above the raised deadzone
        Assert.Equal((0f, 0f), XInputInterop.ApplyDeadzone(0.3f, 0f, 0.3f));
        var (x, _) = XInputInterop.ApplyDeadzone(0.6f, 0f, 0.3f);
        Assert.True(x > 0f && x <= 1f);
    }

    // ── Zombie virtual pad: slot selection & probe classification ──
    // Conversion tools (Steam Input / BetterJoy leftovers) report "connected"
    // forever with all-zero sticks and packet 0 — they must not shadow a real
    // controller on a higher slot, and the probe must tell the user.

    [Fact]
    public void PickSlot_LiveSlotWinsOverZombie()
    {
        bool[] connected = { true, true, false, false };
        bool[] alive = { false, true, false, false };
        Assert.Equal(1, XInputInterop.PickSlot(connected, alive));
    }

    [Fact]
    public void PickSlot_FallsBackToIdleConnectedSlot()
    {
        bool[] connected = { true, false, false, false };
        bool[] alive = { false, false, false, false };
        Assert.Equal(0, XInputInterop.PickSlot(connected, alive));
    }

    [Fact]
    public void PickSlot_FirstConnectedWins_WhenAllIdle()
    {
        bool[] connected = { false, true, true, false };
        bool[] alive = { false, false, false, false };
        Assert.Equal(1, XInputInterop.PickSlot(connected, alive));
    }

    [Fact]
    public void PickSlot_NoneConnected_ReturnsMinusOne()
    {
        Assert.Equal(-1, XInputInterop.PickSlot(new bool[4], new bool[4]));
    }

    [Theory]
    [InlineData(true, true, false, GamepadProbeResult.Connected)]
    [InlineData(true, false, false, GamepadProbeResult.ConnectedNoData)]
    [InlineData(false, false, false, GamepadProbeResult.NotConnected)]
    [InlineData(false, false, true, GamepadProbeResult.XInputUnavailable)]
    internal void ClassifyProbe_MapsAllStates(bool anyConnected, bool anyLive, bool unavailable,
        GamepadProbeResult expected)
        => Assert.Equal(expected, XInputInterop.ClassifyProbe(anyConnected, anyLive, unavailable));

    [Fact]
    public void HasStickSignal_DetectsAnyAxis()
    {
        Assert.False(XInputInterop.HasStickSignal(0f, 0f, 0f, 0f));
        Assert.True(XInputInterop.HasStickSignal(0f, 0f, 0.2f, 0f));
    }
}
