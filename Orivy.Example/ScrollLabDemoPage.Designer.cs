using Orivy;
using Orivy.Controls;
using SkiaSharp;

namespace Orivy.Example;

internal sealed partial class ScrollLabDemoPage
{
    private void InitializeComponent()
    {
        Text = "Scroll Lab";
        Name = "panel5";
        Padding = new(24);
        Dock = Orivy.DockStyle.Fill;
        Radius = new(0);
        Border = new(0);
        AutoScroll = true;
        AutoScrollMargin = new(0, 24);

        var scrollLabHeader = new Element
        {
            Name = "scrollLabHeader",
            Text = "Scroll Lab\nUse this page to test thumb drag, track click, wheel scroll, nested scroll hosts, and wheel routing while hovering child controls.",
            Dock = Orivy.DockStyle.Top,
            Height = 96,
            Padding = new(16),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceVariant,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabWheelCards = new Element
        {
            Name = "scrollLabWheelCards",
            Text = "Scenario A\nWheel over these cards. If wheel routing is correct, the page should still move even when the pointer is on child content.",
            Dock = Orivy.DockStyle.Top,
            Height = 180,
            Padding = new(16),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(110),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabActionA = new Button
        {
            Name = "scrollLabActionA",
            Text = "Action Button In Scroll Flow",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 12),
        };

        var scrollLabActionB = new Button
        {
            Name = "scrollLabActionB",
            Text = "Second Button - Hover Then Wheel",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 16),
        };

        var scrollLabNestedShell = new Element
        {
            Name = "scrollLabNestedShell",
            Text = "Scenario B\nNested scroll host. Test outer wheel on shell, then move over inner cards and wheel again.",
            Dock = Orivy.DockStyle.Top,
            Height = 336,
            Padding = new(16),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabNestedHost = new Element
        {
            Name = "scrollLabNestedHost",
            Dock = Orivy.DockStyle.Fill,
            Padding = new(12),
            Margin = new(0),
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(110),
            BackColor = ColorScheme.Surface.WithAlpha(28),
            AutoScroll = true,
            AutoScrollMargin = new(0, 16)
        };

        var scrollLabNestedTopGap = new Element
        {
            Name = "scrollLabNestedTopGap",
            Dock = Orivy.DockStyle.Top,
            Height = 44,
            Margin = new(0, 0, 0, 10),
            BackColor = ColorScheme.Surface.WithAlpha(20),
            Radius = new(10),
            Border = new(0),
            Text = "Nested Host Start",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ColorScheme.ForeColor
        };

        var scrollLabNestedCard1 = new Element
        {
            Name = "scrollLabNestedCard1",
            Text = "Nested Card 1\nWheel here and verify the inner host moves.",
            Dock = Orivy.DockStyle.Top,
            Height = 104,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            BackColor = ColorScheme.Warning.WithAlpha(46),
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Warning.WithAlpha(110),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabNestedCard2 = new Element
        {
            Name = "scrollLabNestedCard2",
            Text = "Nested Card 2\nDrag the inner scrollbar thumb here.",
            Dock = Orivy.DockStyle.Top,
            Height = 104,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            BackColor = ColorScheme.Success.WithAlpha(38),
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Success.WithAlpha(110),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabNestedButton = new Button
        {
            Name = "scrollLabNestedButton",
            Text = "Nested Button - Hover Then Wheel",
            Dock = Orivy.DockStyle.Top,
            Height = 44,
            Margin = new(0, 0, 0, 12),
        };

        var scrollLabNestedCard3 = new Element
        {
            Name = "scrollLabNestedCard3",
            Text = "Nested Card 3\nTrack click should jump inside the inner host.",
            Dock = Orivy.DockStyle.Top,
            Height = 104,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            BackColor = ColorScheme.Primary.WithAlpha(34),
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(110),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabNestedCard4 = new Element
        {
            Name = "scrollLabNestedCard4",
            Text = "Nested Card 4\nBottom probe for inner scrolling.",
            Dock = Orivy.DockStyle.Top,
            Height = 132,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            BackColor = ColorScheme.SurfaceVariant.WithAlpha(34),
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.SurfaceVariant.WithAlpha(110),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var scrollLabLongTail = new Element
        {
            Name = "scrollLabLongTail",
            Text = "Scenario C\nLong tail content. Use outer wheel, outer thumb drag, and track click while hovering this large block and the two buttons above.",
            Dock = Orivy.DockStyle.Top,
            Height = 320,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            Radius = new(18),
            Border = new(5),
            BorderColor = ColorScheme.Warning.WithAlpha(110),
            TextAlign = ContentAlignment.MiddleLeft
        };

        scrollLabNestedHost.Controls.Add(scrollLabNestedCard4);
        scrollLabNestedHost.Controls.Add(scrollLabNestedCard3);
        scrollLabNestedHost.Controls.Add(scrollLabNestedButton);
        scrollLabNestedHost.Controls.Add(scrollLabNestedCard2);
        scrollLabNestedHost.Controls.Add(scrollLabNestedCard1);
        scrollLabNestedHost.Controls.Add(scrollLabNestedTopGap);
        scrollLabNestedShell.Controls.Add(scrollLabNestedHost);

        Controls.Add(scrollLabLongTail);
        Controls.Add(scrollLabNestedShell);
        Controls.Add(scrollLabActionB);
        Controls.Add(scrollLabActionA);
        Controls.Add(scrollLabWheelCards);
        Controls.Add(scrollLabHeader);
    
    }
}
