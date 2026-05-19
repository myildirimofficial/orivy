using Orivy.Controls;

namespace Orivy.Example;

internal sealed partial class LayoutControlsDemoPage : Container
{
    public LayoutControlsDemoPage()
    {
        InitializeComponent();
        ColorScheme.ThemeChanged += HandleThemeChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ColorScheme.ThemeChanged -= HandleThemeChanged;

        base.Dispose(disposing);
    }

    private void HandleThemeChanged(object? sender, System.EventArgs e)
    {
        RefreshThemeCards(this);
    }

    private static void RefreshThemeCards(ElementBase root)
    {
        for (var i = 0; i < root.Controls.Count; i++)
        {
            if (root.Controls[i] is not ElementBase child)
                continue;

            if (Equals(child.Tag, "theme-card"))
            {
                child.BackColor = ColorScheme.Surface;
                child.ForeColor = ColorScheme.ForeColor;
                child.BorderColor = ColorScheme.Outline.WithAlpha(90);
            }
            else if (Equals(child.Tag, "theme-card-header"))
            {
                child.BackColor = SkiaSharp.SKColors.Transparent;
                child.ForeColor = ColorScheme.ForeColor;
            }

            RefreshThemeCards(child);
        }
    }
}
