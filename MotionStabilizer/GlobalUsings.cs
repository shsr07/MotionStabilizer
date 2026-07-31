// ──────────────────────────────────────────────────────────────────────────
// WPF / WinForms Dual-Framework Global Using Aliases
// ──────────────────────────────────────────────────────────────────────────
//
// This project enables BOTH UseWPF and UseWindowsForms in the .csproj because:
//
//   • WPF (UseWPF=true) — primary UI framework for the settings window,
//     overlay canvas, and all user-facing pages. Chosen for its mature
//     vector graphics pipeline (Shapes, Path, Geometry) and XAML data binding.
//
//   • WinForms (UseWindowsForms=true) — required for NativeWindow, which is
//     used by DirectCompositionMotionRenderer to create a bare Win32 window
//     (without WPF's compositor) for Direct3D/Direct2D/DirectComposition
//     rendering. WPF's HwndSource cannot host a DirectComposition swap chain
//     directly because WPF's own compositor claims the window's redirection
//     surface. The WS_EX_NOREDIRECTIONBITMAP style required by DirectComposition
//     is only achievable via a Win32 window created through NativeWindow.
//
// Enabling both frameworks causes namespace ambiguities because both WPF and
// WinForms define types with the same simple names (e.g., Application, Color,
// MessageBox). These global using aliases resolve the ambiguity by binding
// each ambiguous name to the WPF type, which is the primary framework.
//
// This is a necessary architectural compromise, not a code smell. The
// aliases below are the minimal set required for unambiguous compilation.
// ──────────────────────────────────────────────────────────────────────────

// Types that exist in both WPF and WinForms — aliased to WPF:
global using Application = System.Windows.Application;           // WinForms: System.Windows.Forms.Application
global using Color = System.Windows.Media.Color;                 // WinForms: System.Drawing.Color
global using Point = System.Windows.Point;                       // WinForms: System.Drawing.Point
global using Brush = System.Windows.Media.Brush;                 // WinForms: System.Drawing.Brush
global using TextBox = System.Windows.Controls.TextBox;          // WinForms: System.Windows.Forms.TextBox
global using ListBox = System.Windows.Controls.ListBox;          // WinForms: System.Windows.Forms.ListBox
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;   // WinForms: System.Windows.Forms.KeyEventArgs
global using MessageBox = System.Windows.MessageBox;             // WinForms: System.Windows.Forms.MessageBox
global using Button = System.Windows.Controls.Button;            // WinForms: System.Windows.Forms.Button
global using Orientation = System.Windows.Controls.Orientation;  // WinForms: System.Windows.Forms.Orientation
global using Rectangle = System.Windows.Shapes.Rectangle;        // WinForms: System.Drawing.Rectangle
global using Size = System.Windows.Size;                         // WinForms: System.Drawing.Size
global using FontFamily = System.Windows.Media.FontFamily;       // WinForms: System.Drawing.FontFamily
global using ColorConverter = System.Windows.Media.ColorConverter; // WinForms: System.Drawing.ColorConverter
global using HorizontalAlignment = System.Windows.HorizontalAlignment; // WinForms: System.Windows.Forms.HorizontalAlignment
