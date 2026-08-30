# Motion Stabilizer · Anti-Motion-Sickness Overlay

[![Build & Test](https://github.com/shsr07/MotionStabilizer/workflows/Build%20&%20Test/badge.svg)](https://github.com/shsr07/MotionStabilizer/actions)

> A safe, zero-intrusion visual stabilization overlay for 3D game motion sickness relief.

## 📸 Screenshots

<table>
  <tr>
    <td align="center"><b>Edge Overlay</b></td>
    <td align="center"><b>In-Game Test</b></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/边缘叠加界面.jpg" width="480" alt="Edge Overlay" /></td>
    <td><img src="docs/screenshots/运行测试画面.gif" width="480" alt="In-Game Test" /></td>
  </tr>
  <tr>
    <td align="center"><b>Floating Clock</b></td>
    <td align="center"><b>Crosshair</b></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/悬浮时钟界面.png" width="480" alt="Floating Clock" /></td>
    <td><img src="docs/screenshots/中心准星界面.png" width="480" alt="Crosshair" /></td>
  </tr>
</table>

## ✨ Features

- **Edge Overlay** — draws reference borders at the screen edges (four shapes: Pole / Box / Dome / Flag) to provide visual stability anchors

> [!TIP]
> **Core Feature: Motion Dots**
>
> An ultra-minimal artificial optical-flow stimulus that feeds "your body is moving" visual evidence to your peripheral vision, aligning the visual signal with the inner-ear vestibular signal to relieve sensory conflict at the source.
>
> - **Direction inversion** — invert mouse, keyboard, and gamepad control direction to match different game camera styles
> - **Gamepad control** — optional XInput dual sticks: left stick = WASD role (analog omnidirectional), right stick = mouse role; Xbox controllers work natively, DS5 / Switch controllers work once converted to XInput via Steam Input, BetterJoy, or similar tools
> - **Parallax scaling** — dots automatically shrink near the screen center for a natural sense of depth
> - **Configurable refresh rate** — 30–360 Hz custom animation refresh rate to match your monitor

- **Crosshair** — draws a crosshair at the screen center as a visual focus point
- **Floating Clock** — a draggable real-time clock with multiple time formats and an outline font
- **Global Hotkeys** — switch settings anytime in-game with global hotkeys
- **Multi-Monitor** — automatically detects all displays, lets you pick a **target monitor** in global options (render the overlay only on that screen), positions the edge overlay, motion dots, and crosshair correctly across multiple screens, and supports mixed-DPI monitors (PerMonitorV2)
- **Multilingual** — Chinese / English
- **Profile Management** — save / load / delete custom configuration profiles, auto-saved on change

## 🔒 Safety

- ✓ Pure external desktop overlay — no DLL injection
- ✓ Does not modify game files or access memory
- ✓ Anti-cheat compatibility:
  - Default mode (mouse Raw Input only): compatible with all anti-cheat systems, zero risk
  - WASD keyboard control (optional): uses the standard GetAsyncKeyState API — very low risk, but recommended for single-player games only
  - Gamepad control (optional): uses standard XInput polling — passive stick reads that inject no input into the system, very low risk, but recommended for single-player games only
  - This tool uses a read-only Raw Input bypass: it never intercepts input, never modifies the input stream, and adds no latency

## 📋 Requirements

- Windows 10/11 (64-bit)
- The download release bundles the .NET runtime — no installation needed
- Building from source requires the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## 🚀 Getting Started

### Download (Recommended)

1. Go to the [Releases page](https://github.com/shsr07/MotionStabilizer/releases)
2. Download `MotionStabilizer-v2.8.0-win-x64.zip`
3. Extract it to any folder
4. Double-click `MotionStabilizer.exe` to run

> The .NET runtime is bundled — no installation required.

> **Checksum (v2.8.0)**
>
> SHA-256: `01E813178B850E12D70B0A4DEE8B3193E5184FE25B79A2D9A2DCD99EC91E61A7`
>
> Verify:
> - Windows: `certutil -hashfile MotionStabilizer-v2.8.0-win-x64.zip SHA256`

### Build from Source

```bash
git clone https://github.com/shsr07/MotionStabilizer.git
cd MotionStabilizer
dotnet build -c Release
```

Build output is at `MotionStabilizer/bin/Release/net8.0-windows/`.

## 📖 Usage

1. After launching, the main window appears in the system tray and taskbar
2. Configure the edge overlay, crosshair, and floating clock via the left navigation
3. Set global hotkeys on the "Hotkeys" page
4. Adjust the UI, language, monitor, and profiles on the "Options" page
5. Minimizing the window keeps the program in the tray with the overlay running
6. **IMPORTANT!!!**: Once in-game, control the features with hotkeys

> ⚠️ **Notes**
> 1. **Exclusive Fullscreen**: The overlay may not show in some games' exclusive-fullscreen mode. Switch the game to **borderless** or **windowed** mode and it will work normally.
> 2. **Administrator Notice**: If Steam or your game is running as administrator, MotionStabilizer must also be run as administrator; otherwise the motion dots may not follow the mouse and global hotkeys may stop working. This is caused by a Windows permission isolation mechanism, not a bug in the app.

## ⌨️ Default Hotkeys

| Hotkey | Action |
|--------|--------|
| F1 | Toggle edge overlay |
| F2 | Toggle crosshair |
| F3 | Toggle floating clock |
| F4 | Cycle overlay shape |
| F5 | Cycle crosshair shape |
| F6 | Cycle display mode |
| F7 | Cycle opacity mode |
| F9 | Cycle overlay color (Red/Green/Blue/Custom) |
| F10 | Cycle crosshair color (Red/Green/Blue/Custom) |

> 💡 **Managing bindings**: click a hotkey field on the Hotkeys page, then press **Esc** to cancel the capture or **Delete** to unbind. The app occupies F1–F7, F9 and F10 by default (no modifier keys — captured system-wide); rebind them on that page if they clash with a game.

## 🛠️ Tech Stack

- **.NET 8.0** + **WPF** (Windows Presentation Foundation)
- **Vortice** (DirectComposition / Direct2D1 / Direct3D11 / DXGI) — hardware-accelerated motion dot rendering
- **Win32 API** — click-through windows, global hotkey registration, system tray, multi-monitor virtual screen, Raw Input, XInput gamepad polling
- **xUnit** — unit tests (266 tests covering render helpers, config models, hotkey bindings, observable config, area computation, key-code mapping, monitor selection, gamepad input math, OSD text mapping, compact-surface decision, foreground-rect change detection)
- **C# 12** — latest C# features

## 📁 Project Structure

```
MotionStabilizer/                    # Main project
├── Models/                          # Data models (configs, enums)
│   ├── ObservableObject.cs          #   INotifyPropertyChanged base class
│   ├── OverlayConfig.cs             #   Edge overlay config (observable)
│   ├── CrosshairConfig.cs           #   Crosshair config (observable)
│   ├── ClockConfig.cs               #   Floating clock config (observable)
│   ├── AppConfig.cs                 #   Global options config (observable)
│   ├── HotkeyConfig.cs              #   Hotkey bindings
│   └── Enums.cs                     #   Enum definitions
├── Overlay/                         # Overlay rendering
│   ├── DirectCompositionMotionRenderer.cs  # Motion dots DirectComposition renderer
│   ├── OverlayWindow.xaml(.cs)             # Transparent overlay window
│   └── RenderHelper.cs                     # Shape building helpers
├── Resources/                       # Multilingual string resources
├── Services/                        # Service layer
│   ├── ConfigStore.cs               #   Centralized config store (observable)
│   ├── ConfigManager.cs             #   Config file persistence
│   ├── HotkeyManager.cs             #   Global hotkey management
│   ├── ProfileService.cs            #   Profile management
│   ├── Win32Interop.cs              #   Win32 API interop
│   ├── XInputInterop.cs             #   XInput gamepad stick interop
│   ├── OsdTextBuilder.cs            #   Hotkey → OSD text mapping (pure, unit-tested)
│   ├── AppIcon.cs                   #   Application icon
│   └── TrayService.cs               #   System tray service
├── Themes/                          # Global WPF styles
├── Views/                           # Settings pages (overlay, crosshair, clock, hotkeys, options)
│   └── Dialogs/                     #   Custom dialogs
├── GlobalUsings.cs                  # WPF/WinForms aliases
├── app.manifest                    # PerMonitorV2 DPI declaration
├── App.xaml(.cs)                    # Application entry point
└── MainWindow.xaml(.cs)             # Main window

MotionStabilizer.Tests/              # Unit test project (266 tests)
├── RenderHelperTests.cs             #   Render size mapping, safe-area computation
├── ConfigModelTests.cs              #   Config models: color parsing, edge visibility/opacity
├── HotkeyBindingTests.cs            #   Hotkey display strings, cloning
├── ObservableConfigTests.cs         #   ConfigStore event notification, profile load/reset, safety-gate serialization
├── ComputeMotionZonesTests.cs       #   Motion dot zone geometry computation
├── XInputInteropTests.cs            #   Gamepad input math (normalization, deadzone, velocity synthesis, priority)
├── KeyNameToVkTests.cs              #   Key name → virtual key code mapping
├── MonitorSelectionTests.cs         #   Target monitor selection and layered matching
├── ForegroundRectTests.cs           #   Window-mode foreground-rect change detection
├── OsdTextBuilderTests.cs           #   Hotkey → OSD text mapping
├── PositionClampTests.cs            #   Resolution-change position clamping
├── CompactSurfaceTests.cs           #   1×1 compact-surface decision
└── MotionStabilizer.Tests.csproj    #   Test project file
```

## 📄 License

[MIT License](LICENSE) © 2026 shsr07

## 👤 Author

- **GitHub:** [shsr07](https://github.com/shsr07)
