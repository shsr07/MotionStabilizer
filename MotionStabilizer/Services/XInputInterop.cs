using System.Runtime.InteropServices;

namespace MotionStabilizer.Services;

/// <summary>Result of the enable-time gamepad connectivity probe.</summary>
internal enum GamepadProbeResult
{
    Connected,
    NotConnected,
    XInputUnavailable
}

/// <summary>
/// Minimal XInput interop for gamepad stick input (手柄摇杆控制动态圆点).
/// Only the two sticks are read — buttons and triggers are unused.
/// Prefers xinput1_4.dll (Win8+) with a legacy fallback to xinput9_1_0.dll;
/// when neither can be loaded the gamepad is silently unavailable.
/// The polling model mirrors the renderer's GetAsyncKeyState keyboard poll:
/// passive per-tick state reads — no input is ever synthesized into the OS.
/// Pure math helpers are separated from the P/Invoke surface for unit testing.
/// </summary>
internal static class XInputInterop
{
    /// <summary>Circular radial deadzone applied to both sticks (fixed, not user-facing).</summary>
    public const float DefaultDeadzone = 0.15f;

    private const uint ErrorSuccess = 0;
    private const uint ErrorDeviceNotConnected = 1167;

    // ── P/Invoke ──
    // XINPUT_STATE / XINPUT_GAMEPAD are naturally aligned; default packing matches.

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState", ExactSpelling = true)]
    private static extern uint XInputGetState1_4(uint userIndex, out XInputState state);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState", ExactSpelling = true)]
    private static extern uint XInputGetState9_1_0(uint userIndex, out XInputState state);

    private enum Backend { Untested, XInput1_4, XInput9_1_0, Unavailable }

    private static Backend _backend = Backend.Untested;

    /// <summary>
    /// Polls controller slots 0–3 and reads the sticks of the first connected one.
    /// XInput convention: +Y is stick-up. Returns false when no controller is
    /// connected or XInput is unavailable.
    /// </summary>
    public static bool TryGetSticks(float deadzone,
        out float leftX, out float leftY, out float rightX, out float rightY)
    {
        leftX = leftY = rightX = rightY = 0f;
        for (uint slot = 0; slot < 4; slot++)
        {
            if (InvokeGetState(slot, out XInputState state) != ErrorSuccess)
                continue;

            (leftX, leftY) = ApplyDeadzone(
                NormalizeThumb(state.Gamepad.ThumbLX), NormalizeThumb(state.Gamepad.ThumbLY), deadzone);
            (rightX, rightY) = ApplyDeadzone(
                NormalizeThumb(state.Gamepad.ThumbRX), NormalizeThumb(state.Gamepad.ThumbRY), deadzone);
            return true;
        }
        return false;
    }

    /// <summary>
    /// One-shot connectivity probe for the enable-time feedback in the settings
    /// page. Unlike <see cref="TryGetSticks"/>, it distinguishes "no controller
    /// plugged in" from "XInput DLLs unavailable" — the latter being impossible
    /// to tell apart there, and the top reason users report the feature as
    /// silently broken.
    /// </summary>
    public static GamepadProbeResult ProbeConnection()
    {
        for (uint slot = 0; slot < 4; slot++)
        {
            if (InvokeGetState(slot, out _) == ErrorSuccess)
                return GamepadProbeResult.Connected;
        }
        return _backend == Backend.Unavailable
            ? GamepadProbeResult.XInputUnavailable
            : GamepadProbeResult.NotConnected;
    }

    private static uint InvokeGetState(uint userIndex, out XInputState state)
    {
        state = default;
        if (_backend == Backend.Untested)
            _backend = Backend.XInput1_4;

        while (_backend is Backend.XInput1_4 or Backend.XInput9_1_0)
        {
            try
            {
                return _backend == Backend.XInput1_4
                    ? XInputGetState1_4(userIndex, out state)
                    : XInputGetState9_1_0(userIndex, out state);
            }
            catch (DllNotFoundException) when (_backend == Backend.XInput1_4)
            {
                _backend = Backend.XInput9_1_0; // legacy DLL exports the same XInputGetState
            }
            catch (EntryPointNotFoundException) when (_backend == Backend.XInput1_4)
            {
                _backend = Backend.XInput9_1_0;
            }
            catch (DllNotFoundException)
            {
                _backend = Backend.Unavailable;
            }
            catch (EntryPointNotFoundException)
            {
                _backend = Backend.Unavailable;
            }
        }
        return ErrorDeviceNotConnected; // Unavailable — indistinguishable from "no controller"
    }

    // ── Pure math (no P/Invoke — unit-tested directly) ──

    /// <summary>Normalizes a thumb axis value (−32768…32767) to [−1, 1].</summary>
    public static float NormalizeThumb(short value) => value / (value < 0 ? 32768f : 32767f);

    /// <summary>
    /// Circular radial deadzone with rim rescale: magnitudes at or below
    /// <paramref name="deadzone"/> map to zero, the rim maps to magnitude 1,
    /// direction is preserved. Per-axis normalization overshoot (magnitude > 1)
    /// is clamped back to the rim.
    /// </summary>
    public static (float X, float Y) ApplyDeadzone(float x, float y, float deadzone)
    {
        float magSq = x * x + y * y;
        if (deadzone >= 1f || magSq <= deadzone * deadzone)
            return (0f, 0f);

        float mag = MathF.Sqrt(magSq);
        float scaled = (mag - deadzone) / (1f - deadzone);
        if (scaled > 1f)
            scaled = 1f;
        float scale = scaled / mag;
        return (x * scale, y * scale);
    }

    /// <summary>
    /// Left stick (WASD role) → dot velocity: base speed × sensitivity, screen-space
    /// Y flipped (stick up = negative Y), inversion applied to both axes — mirrors
    /// the WASD math in DirectCompositionMotionRenderer.Timer_Tick.
    /// </summary>
    public static (float Vx, float Vy) StickToVelocity(
        float x, float y, double sensitivity, bool inverted, float baseSpeed)
    {
        float clamped = (float)Math.Clamp(sensitivity, 0.05, 3.0);
        float ksign = inverted ? -1f : 1f;
        return (ksign * x * baseSpeed * clamped, ksign * -y * baseSpeed * clamped);
    }

    /// <summary>
    /// Right stick (mouse role) → synthetic raw mouse delta for one tick: a virtual
    /// mouse gliding at <paramref name="fullDeflectionSpeed"/> px/s, Y flipped into
    /// screen coordinates. The delta scales with dt, so after the renderer's
    /// pending/(dt·60) velocity formula the synthesized velocity is independent of
    /// the tick cadence (identical at the 8 ms and 33 ms tick periods).
    /// </summary>
    public static (float Dx, float Dy) StickToMouseDelta(float x, float y, float dt, float fullDeflectionSpeed)
    {
        float scale = dt * fullDeflectionSpeed;
        return (x * scale, -y * scale);
    }

    /// <summary>Per-axis override priority: keyboard > gamepad > existing (mouse/decay) velocity.</summary>
    public static float MergeAxis(float existing, float keyboard, float gamepad)
    {
        if (keyboard != 0f) return keyboard;
        if (gamepad != 0f) return gamepad;
        return existing;
    }
}
