using SkiaSharp;
using System;

namespace Orivy.Controls;

/// <summary>
/// Modal color-selection dialog (WinForms ColorDialog equivalent) hosting Orivy's
/// <see cref="ColorPicker"/>. Usage:
/// <code>
/// var dlg = new ColorDialog { Color = current };
/// if (dlg.ShowDialog() == DialogResult.OK) current = dlg.Color;
/// </code>
/// </summary>
public class ColorDialog : Window
{
    private readonly ColorPicker _picker;

    /// <summary>The selected color. Set before <see cref="WindowBase.ShowDialog"/> to preselect.</summary>
    public SKColor Color
    {
        get => _picker.SelectedColor;
        set
        {
            _picker.SelectedColor = value;
            _picker.ReferenceColor = value;
        }
    }

    public ColorDialog()
    {
        Text = "Color";
        ClientSize = new SKSize(360, 500);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;

        var buttonRow = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Thickness(12, 10, 12, 10),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0)
        };

        var cancel = new Button
        {
            Text = "Cancel",
            Dock = DockStyle.Right,
            Width = 96,
            Margin = new Thickness(8, 0, 0, 0),
            DialogResult = DialogResult.Cancel
        };
        var ok = new Button
        {
            Text = "OK",
            Dock = DockStyle.Right,
            Width = 96,
            DialogResult = DialogResult.OK
        };

        buttonRow.Controls.Add(ok);
        buttonRow.Controls.Add(cancel);

        _picker = new ColorPicker
        {
            Dock = DockStyle.Fill,
            Margin = new Thickness(12)
        };

        Controls.Add(_picker);
        Controls.Add(buttonRow);
    }
}
