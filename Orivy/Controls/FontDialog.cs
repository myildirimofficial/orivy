using SkiaSharp;
using System;

namespace Orivy.Controls;

/// <summary>
/// Modal font-selection dialog (WinForms FontDialog equivalent) built from Orivy controls:
/// family <see cref="ComboBox"/>, size <see cref="NumericUpDown"/>, bold/italic
/// <see cref="CheckBox"/>es and a live preview. Read the result from <see cref="Font"/>.
/// </summary>
public class FontDialog : Window
{
    private readonly ComboBox _family;
    private readonly NumericUpDown _size;
    private readonly CheckBox _bold;
    private readonly CheckBox _italic;
    private readonly Element _preview;
    private SKFont? _font;

    /// <summary>The selected font. Set before <see cref="WindowBase.ShowDialog"/> to preselect.</summary>
    public SKFont? Font
    {
        get => _font;
        set
        {
            _font = value;
            if (value == null)
                return;

            SelectFamily(value.Typeface?.FamilyName ?? string.Empty);
            _size.Value = (decimal)Math.Clamp(value.Size, (float)_size.Minimum, (float)_size.Maximum);
            _bold.Checked = (value.Typeface?.FontWeight ?? 400) >= 600;
            _italic.Checked = value.Typeface?.IsItalic == true;
            UpdatePreview();
        }
    }

    public FontDialog()
    {
        Text = "Font";
        ClientSize = new SKSize(420, 380);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        Padding = new Thickness(Padding.Left + 14, Padding.Top + 12, Padding.Right + 14, 12);

        var buttonRow = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Thickness(0, 8, 0, 0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0)
        };
        var cancel = new Button { Text = "Cancel", Dock = DockStyle.Right, Width = 96, Margin = new Thickness(8, 0, 0, 0), DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "OK", Dock = DockStyle.Right, Width = 96 };
        buttonRow.Controls.Add(ok);
        buttonRow.Controls.Add(cancel);

        _preview = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 84,
            Margin = new Thickness(0, 12, 0, 0),
            Padding = new Thickness(12),
            Text = "The quick brown fox 0123",
            BackColor = ColorScheme.SurfaceContainerLow,
            ForeColor = ColorScheme.ForeColor,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(96),
            Radius = new Radius(12),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _family = new ComboBox { Dock = DockStyle.Top, Margin = new Thickness(0, 0, 0, 10), PlaceholderText = "Font family" };
        foreach (var name in SKFontManager.Default.FontFamilies)
            _family.Items.Add(name);

        _size = new NumericUpDown { Dock = DockStyle.Top, Margin = new Thickness(0, 0, 0, 10), Minimum = 6, Maximum = 96, Value = 10 };
        _bold = new CheckBox { Text = "Bold", Dock = DockStyle.Top, Height = 32, Margin = new Thickness(0, 0, 0, 4) };
        _italic = new CheckBox { Text = "Italic", Dock = DockStyle.Top, Height = 32 };

        Controls.Add(_italic);
        Controls.Add(_bold);
        Controls.Add(_size);
        Controls.Add(_family);
        Controls.Add(_preview);
        Controls.Add(buttonRow);

        _family.SelectedIndexChanged += (_, _) => UpdatePreview();
        _size.ValueChanged += (_, _) => UpdatePreview();
        _bold.CheckedChanged += (_, _) => UpdatePreview();
        _italic.CheckedChanged += (_, _) => UpdatePreview();

        ok.Click += (_, _) =>
        {
            _font = BuildFont();
            DialogResult = DialogResult.OK;
            Close(DialogResult.OK);
        };
    }

    private void SelectFamily(string familyName)
    {
        for (var i = 0; i < _family.Items.Count; i++)
        {
            if (string.Equals(_family.Items[i]?.ToString(), familyName, StringComparison.OrdinalIgnoreCase))
            {
                _family.SelectedIndex = i;
                return;
            }
        }
    }

    private SKFont BuildFont()
    {
        var familyName = _family.SelectedItem?.ToString();
        var typeface = SKTypeface.FromFamilyName(
            string.IsNullOrWhiteSpace(familyName) ? null : familyName,
            _bold.Checked ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            _italic.Checked ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) ?? SKTypeface.Default;

        var font = new SKFont(typeface, (float)_size.Value);
        Application.ApplyPreferredFontRendering(font);
        return font;
    }

    private void UpdatePreview()
    {
        _preview.Font = BuildFont();
        _preview.Invalidate();
    }
}
