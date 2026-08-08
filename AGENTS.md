# Repository Guidelines

Motion Stabilizer is a Windows-only WPF app (.NET 8) that renders a DirectComposition overlay for 3D motion sickness relief. Requires Windows 10/11 and the .NET 8 SDK.

## Project Structure & Module Organization

- `MotionStabilizer/` - main WPF app:
  - `Models/` - config models, enums, and the `ObservableObject` base class
  - `Overlay/` - overlay window and DirectComposition motion-dot renderer
  - `Services/` - config persistence, hotkeys, profiles, tray, and Win32 interop
  - `Views/` - settings pages; `Views/Dialogs/` - custom dialogs
  - `Resources/` - en-US and zh-CN localized strings; `Themes/` - WPF styles
- `MotionStabilizer.Tests/` - xUnit unit tests, one file per subject (`ConfigModelTests.cs`)
- `docs/screenshots/` - README images

There is no `.sln`; target the `.csproj` files directly.

## Build, Test, and Development Commands

```bash
dotnet build MotionStabilizer/MotionStabilizer.csproj -c Release --nologo
dotnet build MotionStabilizer.Tests/MotionStabilizer.Tests.csproj -c Release --nologo
dotnet test MotionStabilizer.Tests/MotionStabilizer.Tests.csproj -c Release --no-build --nologo
dotnet run --project MotionStabilizer/MotionStabilizer.csproj
```

CI (`.github/workflows/dotnet.yml`) mirrors these commands on `windows-latest` for pushes and PRs to `main`.

## Packaging & Release Build

- Publish a self-contained folder build that bundles the runtime:
```bash
dotnet publish MotionStabilizer/MotionStabilizer.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```
- For a single portable exe, publish single-file with native extraction:
```bash
dotnet publish MotionStabilizer/MotionStabilizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o publish/single-file
```
- Use the single-file exe for distribution; it runs without installing .NET. Framework-dependent `dotnet build` output under `bin/` requires the Windows Desktop Runtime and is not portable.

## Coding Style & Naming Conventions

- C# 12 with nullable enabled and implicit usings; use 4-space indentation and file-scoped namespaces.
- Use PascalCase for public types/members, camelCase for locals/private fields, and `readonly` for immutable fields.
- Keep Win32 P/Invoke calls in `Services/Win32Interop.cs`; pass plain data into testable logic.
- `GlobalUsings.cs` resolves WPF/WinForms name collisions (`Color`, `MessageBox`); extend it only when a new ambiguity appears.
- Use XML doc comments on public APIs.
- No `.editorconfig` or linter is configured; match the surrounding code style.

## Testing Guidelines

- Use xUnit in `MotionStabilizer.Tests/`.
- Name tests `Subject_Action_ExpectedResult`, e.g. `OverlayConfig_GetColor_Red`.
- Prefer `[Fact]` tests of pure logic (zone geometry, config parsing, hotkey mapping) that need no display or hardware.
- Use `InternalsVisibleTo` for internal members instead of making test-only members public.

## Commit & Pull Request Guidelines

- Use Conventional Commits for summaries (`feat:`, `fix:`, `docs:`, `ci:`, `refactor:`, `chore:`); release commits use the `v2.5.0: summary` pattern.
- Keep commits focused; fold version bumps and README updates into release commits.
- PRs target `main` and must pass CI. Include a short description, linked issues, and screenshots for UI or overlay changes.
- Do not commit build output; `bin/`, `obj/`, `publish*/`, and release archives are gitignored.

## Security & Configuration Notes

- Preserve the zero-intrusion boundary: no DLL injection, game-file modification, or memory access.
- Runtime config lives in `%LOCALAPPDATA%\MotionStabilizer\`: profiles under `Profiles/`, global options in `appconfig.json`.
