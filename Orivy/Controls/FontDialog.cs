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
        ClientSize = new SKSize(420, 430);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        Padding = new Thickness(Padding.Left + 16, Padding.Top + 16, Padding.Right + 16, 16);

        var buttonRow = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 36,
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
            Height = 72,
            Margin = new Thickness(0, 4, 0, 20),
            Padding = new Thickness(14),
            Text = "AaBbYyZz 123",
            BackColor = ColorScheme.SurfaceContainerLow,
            ForeColor = ColorScheme.ForeColor,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(96),
            Radius = new Radius(10),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
        };
        var previewLabel = FieldLabel("Preview", DockStyle.Bottom, new Thickness(0, 0, 0, 6));

        // Family + Size share one row — family gets the room, size only needs a few digits —
        // instead of each field claiming a full-width row of its own the way a single lonely
        // field would, which is what made the old layout feel so sparse and disconnected.
        var fieldsRow = new Element
        {
            Dock = DockStyle.Top,
            Height = 80,
            Margin = new Thickness(0, 0, 0, 18),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        var sizeColumn = new Element
        {
            Dock = DockStyle.Left,
            Width = 88,
            Margin = new Thickness(0, 0, 12, 0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        _size = new NumericUpDown { Dock = DockStyle.Bottom, Height = 36, Minimum = 6, Maximum = 96, Value = 10 };
        sizeColumn.Controls.Add(FieldLabel("Size", DockStyle.Bottom, new Thickness(0, 0, 0, 6)));
        sizeColumn.Controls.Add(_size);

        var familyColumn = new Element
        {
            Dock = DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        _family = new ComboBox { Dock = DockStyle.Bottom, Height = 36, PlaceholderText = "Font family" };
        foreach (var name in SKFontManager.Default.FontFamilies)
            _family.Items.Add(name);
        familyColumn.Controls.Add(FieldLabel("Family", DockStyle.Bottom, new Thickness(0, 0, 0, 6)));
        familyColumn.Controls.Add(_family);

        fieldsRow.Controls.Add(familyColumn);
        fieldsRow.Controls.Add(sizeColumn);

        // Style row — Bold/Italic side by side under one shared label, instead of each stacked
        // as its own full-width row (which read as two unrelated options rather than one group).
        var styleRow = new Element
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        _bold = new CheckBox { Text = "Bold", Dock = DockStyle.Left, Width = 90, Height = 32, Margin = new Thickness(0, 0, 0, 0) };
        _italic = new CheckBox { Text = "Italic", Dock = DockStyle.Left, Width = 90, Height = 32 };
        var checkboxRow = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        checkboxRow.Controls.Add(_italic);
        checkboxRow.Controls.Add(_bold);
        styleRow.Controls.Add(FieldLabel("Style", DockStyle.Bottom, new Thickness(0, 0, 0, 6)));
        styleRow.Controls.Add(checkboxRow);

        Controls.Add(styleRow);
        Controls.Add(fieldsRow);
        Controls.Add(previewLabel);
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

    /// <summary>A small muted caption above a field — the previous layout had no field labels at
    /// all (just a ComboBox whose only hint was a placeholder that disappears the moment something's
    /// selected), so there was nothing on screen saying which control was "family" vs "size".</summary>
    private Element FieldLabel(string text, DockStyle dock, Thickness margin) => new()
    {
        Text = text,
        Dock = dock,
        Height = 16,
        Margin = margin,
        BackColor = SKColors.Transparent,
        ForeColor = ColorScheme.ForeColor.WithAlpha(150),
        Font = new SKFont(SKTypeface.FromFamilyName(Application.DefaultFont.Typeface?.FamilyName) ?? SKTypeface.Default, 9f),
        // ElementBase defaults to WordWrap — fine for most controls, but this label's own Height
        // (16) is barely under the font's single-line advance-with-spacing, so the wrap-measurer
        // (see TextRenderer.MeasureWrappedText) concludes zero lines fit and renders nothing at
        // all (the exact bug Badge had for the same reason — see Badge's own WrapMode comment).
        WrapMode = TextWrap.None,
        Border = new Thickness(0),
        Radius = new Radius(0),
        TextAlign = ContentAlignment.BottomLeft,
    };

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
