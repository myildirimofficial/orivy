using SkiaSharp;
using System;
using System.ComponentModel;

namespace Orivy.Controls;

public class Card : Container
{
    private readonly Element _header;
    private readonly Element _descriptionLabel;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private bool _useThemeColors = true;

    public Card()
    {
        Radius = new Radius(14);
        Border = new Thickness(1);
        TextAlign = ContentAlignment.MiddleLeft;
        BackgroundImageLayout = ImageLayout.Zoom;
        Shadow = new BoxShadow(0f, 1f, 2f, 0, ColorScheme.ShadowColor.WithAlpha(14));

        Content = new Container
        {
            Name = "cardContent",
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(0)
        };

        _descriptionLabel = new Element
        {
            Name = "cardDescription",
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Padding = new Thickness(0, 0, 0, 16),
            Margin = new Thickness(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };

        _header = new Element
        {
            Name = "cardHeader",
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Padding = new Thickness(0, 0, 0, 4),
            Margin = new Thickness(0),
            Radius = new Radius(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        var headerFont = _header.Font.CloneFont();
        headerFont.Embolden = true;
        headerFont.Size += 1f;
        _header.Font = headerFont;
        headerFont.Dispose();

        Controls.Add(Content);
        Controls.Add(_descriptionLabel);
        Controls.Add(_header);
        ApplyThemeColors();
        ColorScheme.ThemeChanged += HandleThemeChanged;
    }

    [Browsable(false)]
    public Container Content { get; }

    [DefaultValue("")]
    public string Title
    {
        get => _title;
        set
        {
            var normalized = value ?? string.Empty;
            if (_title == normalized)
                return;

            _title = normalized;
            RefreshHeaderText();
        }
    }

    [DefaultValue("")]
    public string Description
    {
        get => _description;
        set
        {
            var normalized = value ?? string.Empty;
            if (_description == normalized)
                return;

            _description = normalized;
            RefreshHeaderText();
        }
    }

    [DefaultValue(true)]
    public bool UseThemeColors
    {
        get => _useThemeColors;
        set
        {
            if (_useThemeColors == value)
                return;

            _useThemeColors = value;
            if (value)
                ApplyThemeColors();
        }
    }

    public void AddContent(ElementBase content)
    {
        Content.Controls.Add(content);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ColorScheme.ThemeChanged -= HandleThemeChanged;

        base.Dispose(disposing);
    }

    private void HandleThemeChanged(object? sender, EventArgs e)
    {
        if (UseThemeColors)
            ApplyThemeColors();
    }

    private void ApplyThemeColors()
    {
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        BorderColor = ColorScheme.Outline.WithAlpha(ColorScheme.IsDarkMode ? (byte)82 : (byte)92);
        Shadow = new BoxShadow(
            0f,
            ColorScheme.IsDarkMode ? 0.5f : 1f,
            ColorScheme.IsDarkMode ? 0f : 2f,
            0,
            ColorScheme.ShadowColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)0 : (byte)14));
        _header.ForeColor = ColorScheme.ForeColor;
        _header.BackColor = SKColors.Transparent;
        _header.BorderColor = SKColors.Transparent;
        _descriptionLabel.ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)168 : (byte)142);
        _descriptionLabel.BackColor = SKColors.Transparent;
        Content.BackColor = SKColors.Transparent;
    }

    private void RefreshHeaderText()
    {
        _header.Text = Title;
        _header.Visible = !string.IsNullOrWhiteSpace(_header.Text);
        _descriptionLabel.Text = Description;
        _descriptionLabel.Visible = !string.IsNullOrWhiteSpace(_descriptionLabel.Text);
        PerformLayout();
        Invalidate();
    }
}
