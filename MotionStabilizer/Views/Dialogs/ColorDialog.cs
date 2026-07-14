using System.Windows.Media;

namespace MotionStabilizer.Views;

/// <summary>
/// Wraps the WinForms ColorDialog for use in WPF.
/// </summary>
public class ColorDialog
{
    private readonly System.Windows.Forms.ColorDialog _dialog = new()
    {
        FullOpen = true,
        AllowFullOpen = true
    };

    public Color Color
    {
        get => Color.FromArgb(_dialog.Color.A, _dialog.Color.R, _dialog.Color.G, _dialog.Color.B);
        set => _dialog.Color = System.Drawing.Color.FromArgb(value.A, value.R, value.G, value.B);
    }

    public bool? ShowDialog()
    {
        return _dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK;
    }
}
