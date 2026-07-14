// Global using aliases to resolve WPF vs WinForms namespace conflicts
// (Both UseWPF and UseWindowsForms are enabled, causing ambiguities)
global using Application = System.Windows.Application;
global using Color = System.Windows.Media.Color;
global using Point = System.Windows.Point;
global using Brush = System.Windows.Media.Brush;
global using TextBox = System.Windows.Controls.TextBox;
global using ListBox = System.Windows.Controls.ListBox;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MessageBox = System.Windows.MessageBox;
global using Button = System.Windows.Controls.Button;
global using Orientation = System.Windows.Controls.Orientation;
global using Rectangle = System.Windows.Shapes.Rectangle;
global using Size = System.Windows.Size;
global using FontFamily = System.Windows.Media.FontFamily;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
