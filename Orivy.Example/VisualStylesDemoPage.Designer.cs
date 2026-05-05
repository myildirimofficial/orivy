using Orivy;
using Orivy.Animation;
using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class VisualStylesDemoPage
{
    private Element visualStyleHeader = null!;
    private Element visualStyleMotionHero = null!;
    private Element visualStyleInteractiveCard = null!;
    private Element visualStyleDangerCard = null!;
    private Element visualStyleDisabledCard = null!;
    private Element visualStyleFooterAction = null!;
    private Button visualStylePrimaryButton = null!;
    private Button visualStyleGhostButton = null!;
    private Element visualStyleDrivenTarget = null!;
    private Element visualStyleScrollProbe = null!;

    private void InitializeComponent()
    {
        Text = "Visual Styles";
        Name = "panel4";
        Padding = new(0);
        Dock = Orivy.DockStyle.Fill;
        Radius = new(0);
        Border = new(0);
        AutoScroll = false;
        AutoScrollMargin = new(0, 24);

        visualStyleHeader = new()
        {
            Name = "visualStyleHeader",
            Text = "Visual Style Builder\nOpt-in only: state refresh and transitions start when a control explicitly configures visual styles.",
            Dock = Orivy.DockStyle.Top,
            Height = 84,
            Padding = new(14),
            Margin = new(0, 0, 0, 16),
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            TextAlign = ContentAlignment.MiddleLeft
        };

        visualStyleInteractiveCard = new()
        {
            Name = "visualStyleInteractiveCard",
            Text = "Interactive Card\nHover, press or focus this element to see layered transitions and subtle rectangle drift.",
            Dock = Orivy.DockStyle.Top,
            Height = 92,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            Radius = new(16),
            Border = new(1),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };

        visualStyleMotionHero = new()
        {
            Name = "visualStyleMotionHero",
            Text = "Motion Builder\nFloating circles, orbiting shapes and bezier path motion are rendered through ConfigureMotionEffects(...).",
            Dock = Orivy.DockStyle.Top,
            Height = 196,
            Padding = new(18),
            Margin = new(0, 0, 0, 14),
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };

        visualStyleDangerCard = new()
        {
            Name = "visualStyleDangerCard",
            Text = "Predicate Card\nClick to toggle a custom predicate state.",
            Dock = Orivy.DockStyle.Top,
            Height = 92,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            Radius = new(16),
            Border = new(1),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Tag = "normal"
        };

        visualStyleDisabledCard = new()
        {
            Name = "visualStyleDisabledCard",
            Text = "Disabled State Card\nThis card is disabled and styled by OnDisabled.",
            Dock = Orivy.DockStyle.Top,
            Height = 92,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            Radius = new(16),
            Border = new(1),
            TextAlign = ContentAlignment.MiddleLeft,
            Enabled = false
        };

        visualStyleFooterAction = new()
        {
            Name = "visualStyleFooterAction",
            Text = "Toggle Disabled Card",
            Dock = Orivy.DockStyle.Top,
            Height = 54,
            Padding = new(12),
            Margin = new(0, 0, 0, 14),
            Radius = new(14),
            Border = new(0),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };

        visualStylePrimaryButton = new Button
        {
            Name = "visualStylePrimaryButton",
            Text = "Primary Button",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 12),
        };

        visualStyleGhostButton = new Button
        {
            Name = "visualStyleGhostButton",
            Text = "Secondary Button",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 14),
        };

        visualStyleDrivenTarget = new()
        {
            Name = "visualStyleDrivenTarget",
            Text = "Driven Target\nHover, press or focus the Primary Button above. This card is animated by another element's visual state.",
            Dock = Orivy.DockStyle.Top,
            Height = 78,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            Radius = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var visualStyleContentHost = new Container
        {
            Name = "visualStyleContentHost",
            Dock = Orivy.DockStyle.Fill,
            Padding = new(18),
            Radius = new(0),
            Border = new(0),
            AutoScroll = true,
            AutoScrollMargin = new(0, 24)
        };

        var visualStyleSidebarToolbar = new Container
        {
            Name = "visualStyleSidebarToolbar",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Padding = new(0),
            Margin = new(0, 0, 0, 14),
            Radius = new(0),
            Border = new(0),
            BackColor = SKColors.Empty,
        };

        var visualStyleSidebarDrawer = new Container
        {
            Name = "visualStyleSidebarDrawer",
            Text = "Navigation",
            Dock = Orivy.DockStyle.Right,
            Width = 0,
            Padding = new(14, 18, 14, 14),
            Margin = new(0),
            Radius = new(0),
            Border = new(0),
            BorderColor = SKColors.Empty,
            BackColor = SKColors.Empty,
        };

        var visualStyleSidebarToggle = new Element
        {
            Name = "visualStyleSidebarToggle",
            Text = "\u2630",
            Dock = Orivy.DockStyle.Left,
            Width = 44,
            Height = 40,
            Padding = new(0),
            Margin = new(0),
            Radius = new(10),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Tag = "closed"
        };

        var visualStyleSidebarTitle = new Element
        {
            Name = "visualStyleSidebarTitle",
            Text = "Navigation",
            Dock = Orivy.DockStyle.Top,
            Height = 34,
            Padding = new(4, 0, 4, 0),
            Margin = new(0, 0, 0, 12),
            Radius = new(0),
            Border = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 15f)
        };

        var visualStyleSidebarReports = new Element
        {
            Name = "visualStyleSidebarReports",
            Text = "Reports",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Padding = new(12, 0, 12, 0),
            Margin = new(0, 0, 0, 4),
            Radius = new(8),
            Border = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Tag = "active"
        };

        var visualStyleSidebarDesign = new Element
        {
            Name = "visualStyleSidebarDesign",
            Text = "Design",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Padding = new(12, 0, 12, 0),
            Margin = new(0, 0, 0, 4),
            Radius = new(8),
            Border = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
        };

        var visualStyleSidebarDeploy = new Element
        {
            Name = "visualStyleSidebarDeploy",
            Text = "Deploy",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Padding = new(12, 0, 12, 0),
            Margin = new(0, 0, 0, 4),
            Radius = new(8),
            Border = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
        };
        visualStyleScrollProbe = new()
        {
            Name = "visualStyleScrollProbe",
            Text = "Scroll Probe\nIf you can reach this block, AutoScroll is now measuring content after dock layout. The two Button controls above also prove visual styles work inside the example page.",
            Dock = Orivy.DockStyle.Top,
            Height = 240,
            Padding = new(18),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainerHigh,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(120),
            TextAlign = ContentAlignment.MiddleLeft
        };

        visualStyleHeader.ConfigureVisualStyles(styles =>
        {
            styles
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.SurfaceVariant)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline)
                    .Radius(14))
                .OnHover(rule => rule
                    .BorderColor(ColorScheme.Primary)
                    .Background(ColorScheme.SurfaceVariant.Brightness(0.04f)));
        });

        visualStyleMotionHero.ConfigureMotionEffects(scene =>
        {
            scene
                .Circle(circle => circle
                    .Anchor(0.18f, 0.34f)
                    .Size(84f, 84f)
                    .Orbit(24f, 16f)
                    .Duration(4.4d)
                    .Opacity(0.16f, 0.42f)
                    .Scale(0.92f, 1.12f)
                    .SpeedOnHover(1.6f)
                    .Color(new SKColor(56, 189, 248, 120)))
                .Circle(circle => circle
                    .Anchor(0.82f, 0.28f)
                    .Size(56f, 56f)
                    .Drift(-16f, 22f)
                    .Delay(0.8d)
                    .Duration(5.1d)
                    .Opacity(0.14f, 0.34f)
                    .Scale(0.88f, 1.18f)
                    .SpeedOnHover(1.35f)
                    .Color(new SKColor(192, 132, 252, 110)))
                .Rectangle(rect => rect
                    .Anchor(0.64f, 0.68f)
                    .Size(120f, 24f)
                    .CornerRadius(12f)
                    .Bezier(new SKPoint(-42f, 10f), new SKPoint(28f, -36f), new SKPoint(78f, 26f), new SKPoint(-16f, 6f))
                    .Rotate(10f)
                    .Duration(4.9d)
                    .Opacity(0.10f, 0.24f)
                    .SpeedOnHover(1.8f)
                    .Color(new SKColor(255, 255, 255, 96)))
                .Rectangle(rect => rect
                    .Anchor(0.28f, 0.74f)
                    .Size(72f, 72f)
                    .CornerRadius(22f)
                    .Orbit(18f, 14f)
                    .Rotate(-14f)
                    .Delay(0.45d)
                    .Duration(5.6d)
                    .Opacity(0.08f, 0.18f)
                    .Scale(0.94f, 1.08f)
                    .Color(new SKColor(255, 255, 255, 84)));
        });

        visualStyleInteractiveCard.ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(180), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline)
                    .Radius(16)
                    .Opacity(1f)
                    .Shadow(new BoxShadow(0f, 2f, 8f, 0, ColorScheme.ShadowColor)))
                .OnHover(rule => rule
                    .Background(ColorScheme.SurfaceVariant)
                    .BorderColor(ColorScheme.Primary)
                    .Shadow(new BoxShadow(0f, 12f, 24f, 0, ColorScheme.ShadowColor)))
                .OnPressed(rule => rule
                    .Opacity(0.93f)
                    .Background(ColorScheme.SurfaceVariant.Brightness(-0.03f))
                    .Shadow(new BoxShadow(0f, 4f, 12f, 0, ColorScheme.ShadowColor)))
                .OnFocused(rule => rule
                    .Border(2)
                    .BorderColor(ColorScheme.Primary));
        });

        visualStyleInteractiveCard.ConfigureMotionEffects(scene =>
        {
            scene
                .Rectangle(rect => rect
                    .Anchor(0.88f, 0.5f)
                    .Size(58f, 58f)
                    .CornerRadius(18f)
                    .Orbit(10f, 10f)
                    .Rotate(18f)
                    .Duration(3.8d)
                    .Opacity(0.04f, 0.12f)
                    .Scale(0.94f, 1.05f)
                    .SpeedOnHover(2f)
                    .SpeedOnPressed(2.6f)
                    .SpeedOnFocused(1.45f)
                    .Color(new SKColor(59, 130, 246, 88)))
                .Circle(circle => circle
                    .Anchor(0.74f, 0.38f)
                    .Size(22f, 22f)
                    .Bezier(new SKPoint(-10f, 4f), new SKPoint(8f, -16f), new SKPoint(22f, 12f), new SKPoint(-6f, 18f))
                    .Duration(2.9d)
                    .Opacity(0.06f, 0.16f)
                    .Scale(0.9f, 1.14f)
                    .SpeedOnHover(2.2f)
                    .Color(new SKColor(255, 255, 255, 90)));
        });

        visualStyleDangerCard.ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(220), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline)
                    .Radius(16))
                .OnHover(rule => rule
                    .BorderColor(ColorScheme.Primary)
                    .Background(ColorScheme.SurfaceVariant))
                .When((element, state) => Equals(element.Tag, "danger"), rule => rule
                    .Background(ColorScheme.Error)
                    .Foreground(ColorScheme.ForeColor)
                    .BorderColor(ColorScheme.Error)
                    .Shadow(new BoxShadow(0f, 14f, 30f, 0, ColorScheme.ShadowColor)))
                .When((element, state) => Equals(element.Tag, "danger") && state.IsPointerOver, rule => rule
                    .Opacity(0.95f));
        });

        visualStyleDisabledCard.ConfigureVisualStyles(styles =>
        {
            styles
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline)
                    .Radius(16))
                .OnDisabled(rule => rule
                    .Background(ColorScheme.SurfaceVariant)
                    .Foreground(ColorScheme.ForeColor.WithAlpha(170))
                    .BorderColor(ColorScheme.Outline.WithAlpha(140))
                    .Opacity(0.82f));
        });

        visualStyleFooterAction.ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(160), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Primary)
                    .Foreground(ColorScheme.ForeColor)
                    .Radius(14)
                    .Shadow(new BoxShadow(0f, 4f, 12f, 0, ColorScheme.ShadowColor)))
                .OnHover(rule => rule
                    .Background(ColorScheme.Primary.Brightness(0.06f))
                    .Shadow(new BoxShadow(0f, 10f, 18f, 0, ColorScheme.ShadowColor)))
                .OnPressed(rule => rule
                    .Opacity(0.9f));
        });

        visualStyleGhostButton.ConfigureVisualStyles(styles =>
        {
            styles
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline)
                    .Shadow(BoxShadow.None))
                .OnHover(rule => rule
                    .Background(ColorScheme.SurfaceVariant)
                    .BorderColor(ColorScheme.Primary))
                .OnPressed(rule => rule
                    .Background(ColorScheme.SurfaceVariant.Brightness(-0.04f))
                    .Opacity(0.95f));
        });

        visualStyleSidebarToggle.ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(120), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline.WithAlpha(96))
                    .Radius(10)
                    .Shadow(BoxShadow.None))
                .When((element, state) => state.IsPointerOver && !Equals(element.Tag, "open"), rule => rule
                    .Background(ColorScheme.SurfaceVariant)
                    .BorderColor(ColorScheme.Primary.WithAlpha(142))
                    .Scale(1.02f))
                .OnPressed(rule => rule
                    .Scale(0.96f)
                    .Opacity(0.9f))
                .When((element, _) => Equals(element.Tag, "open"), rule => rule
                    .Background(ColorScheme.Primary.WithAlpha(34))
                    .Foreground(ColorScheme.Primary)
                    .BorderColor(ColorScheme.Primary.WithAlpha(132))
                    .Scale(1.01f)
                    .Shadow(new BoxShadow(0f, 6f, 16f, 0, ColorScheme.Primary.WithAlpha(26))));
        });

        visualStyleSidebarDrawer.ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(150), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Width(0)
                    .Background(ColorScheme.SurfaceContainerHigh.WithAlpha(0))
                    .Foreground(ColorScheme.ForeColor)
                    .Border(0)
                    .BorderColor(ColorScheme.Outline.WithAlpha(0))
                    .Radius(0)
                    .Opacity(0f)
                    .TranslateX(32)
                    .Shadow(BoxShadow.None));
        });

        visualStyleSidebarToggle.VisualStyles
            .With(visualStyleSidebarDrawer)
            .DefaultTransition(TimeSpan.FromMilliseconds(150), AnimationType.CubicEaseOut)
            .WhenSource((source, _) => Equals(source.Tag, "open"), rule => rule
                .Width(260)
                .Background(ColorScheme.SurfaceContainerHigh)
                .Border(new Thickness(1, 0, 0, 0))
                .BorderColor(ColorScheme.Outline.WithAlpha(86))
                .Radius(0)
                .Opacity(1f)
                .TranslateX(0)
                .Shadow(new BoxShadow(-14f, 0f, 30f, 0, ColorScheme.ShadowColor.WithAlpha(42))));

        void ConfigureSidebarItem(Element item, SKColor accent)
        {
            item.ConfigureVisualStyles(styles => styles
                .DefaultTransition(TimeSpan.FromMilliseconds(90), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.SurfaceContainerHigh)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(0)
                    .BorderColor(SKColors.Empty)
                    .Radius(8)
                    .TranslateX(0)
                    .Scale(1f))
                .When((element, state) => state.IsPointerOver && !state.IsPressed, rule => rule
                    .Background(ColorScheme.SurfaceVariant))
                .When((_, state) => state.IsPressed, rule => rule
                    .Background(ColorScheme.SurfaceVariant.Brightness(-0.03f)))
                .When((element, _) => Equals(element.Tag, "active"), rule => rule
                    .Background(accent.WithAlpha(34))
                    .Foreground(ColorScheme.ForeColor)));
        }

        ConfigureSidebarItem(
            visualStyleSidebarReports,
            ColorScheme.Primary);
        ConfigureSidebarItem(
            visualStyleSidebarDesign,
            new SKColor(168, 85, 247));
        ConfigureSidebarItem(
            visualStyleSidebarDeploy,
            ColorScheme.Success);

        visualStyleSidebarTitle.ConfigureVisualStyles(styles => styles
            .Base(baseStyle => baseStyle
                .Background(SKColors.Empty)
                .Foreground(ColorScheme.ForeColor.WithAlpha(220))
                .Border(0)
                .Radius(0)
                .Opacity(0.96f)));

        visualStyleSidebarToggle.Click += (_, _) =>
        {
            var isOpen = Equals(visualStyleSidebarToggle.Tag, "open");
            visualStyleSidebarToggle.Tag = isOpen ? "closed" : "open";
            visualStyleSidebarToggle.Text = isOpen ? "\u2630" : "\u00d7";
            visualStyleSidebarToggle.ReevaluateVisualStyles();
            visualStyleSidebarDrawer.ReevaluateVisualStyles();
        };

        visualStyleDrivenTarget.ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(150), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline)
                    .Radius(16)
                    .Opacity(0.92f))
                .OnHover(rule => rule
                    .BorderColor(ColorScheme.Primary.WithAlpha(130)));
        });

        visualStylePrimaryButton.VisualStyles
            .With(visualStyleDrivenTarget)
            .DefaultTransition(TimeSpan.FromMilliseconds(150), AnimationType.CubicEaseOut)
            .OnHover(rule => rule
                .Height(118)
                .Background(ColorScheme.SurfaceContainerHigh)
                .BorderColor(ColorScheme.Primary)
                .Scale(1.015f)
                .Opacity(1f)
                .Shadow(new BoxShadow(0f, 12f, 24f, 0, ColorScheme.Primary.WithAlpha(34))))
            .OnPressed(rule => rule
                .Height(104)
                .Background(ColorScheme.Primary.WithAlpha(46))
                .BorderColor(ColorScheme.Primary.Brightness(-0.1f))
                .Scale(0.99f)
                .Opacity(0.96f))
            .OnFocused(rule => rule
                .Border(2)
                .BorderColor(ColorScheme.Primary.Brightness(0.12f)));
        visualStyleScrollProbe.ConfigureVisualStyles(styles =>
        {
            styles
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.SurfaceContainerHigh)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Primary.WithAlpha(120))
                    .Radius(18))
                .OnHover(rule => rule
                    .BorderColor(ColorScheme.Primary)
                    .Background(ColorScheme.SurfaceVariant));
        });

        visualStyleSidebarDrawer.Controls.Add(visualStyleSidebarDeploy);
        visualStyleSidebarDrawer.Controls.Add(visualStyleSidebarDesign);
        visualStyleSidebarDrawer.Controls.Add(visualStyleSidebarReports);
        visualStyleSidebarDrawer.Controls.Add(visualStyleSidebarTitle);
        visualStyleSidebarToolbar.Controls.Add(visualStyleSidebarToggle);
        visualStyleDangerCard.Click += VisualStyleDangerToggle_Click;
        visualStylePrimaryButton.Click += VisualStylePrimaryButton_Click;
        visualStyleFooterAction.Click += VisualStyleEnableDisabled_Click;


        visualStyleContentHost.Controls.Add(visualStyleScrollProbe);
        visualStyleContentHost.Controls.Add(visualStyleDrivenTarget);
        visualStyleContentHost.Controls.Add(visualStyleFooterAction);
        visualStyleContentHost.Controls.Add(visualStyleGhostButton);
        visualStyleContentHost.Controls.Add(visualStylePrimaryButton);
        visualStyleContentHost.Controls.Add(visualStyleDisabledCard);
        visualStyleContentHost.Controls.Add(visualStyleDangerCard);
        visualStyleContentHost.Controls.Add(visualStyleInteractiveCard);
        visualStyleContentHost.Controls.Add(visualStyleMotionHero);
        visualStyleContentHost.Controls.Add(visualStyleHeader);
        visualStyleContentHost.Controls.Add(visualStyleSidebarToolbar);
        Controls.Add(visualStyleContentHost);
        Controls.Add(visualStyleSidebarDrawer);
        visualStyleSidebarDrawer.BringToFront();
    
    }
}
