using Orivy;
using Orivy.Animation;
using Orivy.Binding;
using Orivy.Controls;
using Orivy.Validations;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orivy.Example;

internal partial class MainWindow
{
    internal void InitializeComponent()
    {
        this.SuspendLayout();
        
        this.panel3 = new()
        {
            Text = "Designer",
            Name = "panel3",
            Padding = new(24),
            Dock = Orivy.DockStyle.Fill,
            Radius = new(0),
            Border = new(0),
            AutoScroll = true,
            AutoScrollMargin = new(0, 24)
        };

        this.panel4 = new()
        {
            Text = "Visual Styles",
            Name = "panel4",
            Padding = new(24),
            Dock = Orivy.DockStyle.Fill,
            Radius = new(0),
            Border = new(0),
            AutoScroll = true,
            AutoScrollMargin = new(0, 24)
        };

        this.panel5 = new()
        {
            Text = "Scroll Lab",
            Name = "panel5",
            Padding = new(24),
            Dock = Orivy.DockStyle.Fill,
            Radius = new(0),
            Border = new(0),
            AutoScroll = true,
            AutoScrollMargin = new(0, 24)
        };

        this.panel6 = new()
        {
            Text = "Grid List",
            Name = "panel6",
            Padding = new(20),
            Dock = Orivy.DockStyle.Fill,
            Radius = new(0),
            Border = new(0),
            AutoScroll = true,
            AutoScrollMargin = new(0, 24)
        };

        this.visualStyleHeader = new()
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

        this.visualStyleInteractiveCard = new()
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

        this.visualStyleMotionHero = new()
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

        this.visualStyleDangerCard = new()
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

        this.visualStyleDisabledCard = new()
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

        this.visualStyleFooterAction = new()
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

        this.visualStylePrimaryButton = new Button
        {
            Name = "visualStylePrimaryButton",
            Text = "Primary Button - Accent Motion On",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 12),
            AccentMotionEnabled = true
        };

        this.visualStyleGhostButton = new Button
        {
            Name = "visualStyleGhostButton",
            Text = "Secondary Button - Accent Motion Off",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 14),
            AccentMotionEnabled = false
        };

        this.visualStyleScrollProbe = new()
        {
            Name = "visualStyleScrollProbe",
            Text = "Scroll Probe\nIf you can reach this block, AutoScroll is now measuring content after dock layout. The two Button controls above also prove the new control works inside the example page.",
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

        this.visualStyleHeader.ConfigureVisualStyles(styles =>
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

        this.visualStyleMotionHero.ConfigureMotionEffects(scene =>
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

        this.visualStyleInteractiveCard.ConfigureVisualStyles(styles =>
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

        this.visualStyleInteractiveCard.ConfigureMotionEffects(scene =>
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

        this.visualStyleDangerCard.ConfigureVisualStyles(styles =>
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

        this.visualStyleDisabledCard.ConfigureVisualStyles(styles =>
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

        this.visualStyleFooterAction.ConfigureVisualStyles(styles =>
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

        this.visualStyleGhostButton.ConfigureVisualStyles(styles =>
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

        this.visualStyleScrollProbe.ConfigureVisualStyles(styles =>
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

        var designerControlHeader = new Element
        {
            Name = "designerControlHeader",
            Text = "Designer Controls\nComboBox demos live only on this page now. Use the motion preset, multi-select dropdown and inline color picker together.",
            Dock = Orivy.DockStyle.Top,
            Height = 102,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceVariant,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerControlShell = new Element
        {
            Name = "designerControlShell",
            Dock = Orivy.DockStyle.Top,
            Height = 628,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            Text = "Inspector Surface\nSingle-select, multi-select, popup motion and the new color picker live together here.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerControlStatus = new Element
        {
            Name = "designerControlStatus",
            Dock = Orivy.DockStyle.Bottom,
            Height = 72,
            Padding = new(14),
            Margin = new(0, 14, 0, 0),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(86),
            Text = "Designer Status\nDropdown motion, multi-select state and color picker values will report here.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerSurfaceCombo = new ComboBox
        {
            Name = "designerSurfaceCombo",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Margin = new(0, 0, 0, 12),
            PlaceholderText = "Inspector surface",
            MaxDropDownItems = 6,
            DropDownItemHeight = 34,
            ShowSelectionIndicator = true,
            DropDownOpeningEffect = OpeningEffectType.PopFade
        };
        designerSurfaceCombo.Items.AddRange(new object[]
        {
            new ComboBoxItem("Canvas Inspector", "canvas"),
            new ComboBoxItem("Prototype Flow", "prototype"),
            new ComboBoxItem("Component Tokens", "tokens"),
            new ComboBoxItem("Export Review", "export")
        });
        designerSurfaceCombo.SelectedIndex = 0;

        var designerMotionCombo = new ComboBox
        {
            Name = "designerMotionCombo",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Margin = new(0, 0, 0, 12),
            PlaceholderText = "Popup motion preset",
            MaxDropDownItems = 6,
            DropDownItemHeight = 34
        };
        OpeningEffectType[] motionPresets = (OpeningEffectType[])Enum.GetValues(typeof(OpeningEffectType));
        designerMotionCombo.Items.AddRange(motionPresets.Select(effect => new ComboBoxItem(effect.ToString(), effect)).ToArray());

        designerMotionCombo.SelectedValue = OpeningEffectType.PopFade;

        var designerModulesCombo = new ComboBox
        {
            Name = "designerModulesCombo",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Margin = new(0, 0, 0, 12),
            PlaceholderText = "Inspector modules",
            MaxDropDownItems = 8,
            DropDownItemHeight = 34,
            MultiSelect = true,
            DropDownOpeningEffect = OpeningEffectType.PopFade
        };
        designerModulesCombo.Items.AddRange(new object[]
        {
            new ComboBoxItem("Layout Grid", "layout"),
            new ComboBoxItem("Layer Stack", "layers"),
            new ComboBoxItem("Token Studio", "tokens"),
            new ComboBoxItem("Motion Curves", "motion"),
            new ComboBoxItem("Accessibility", "a11y"),
            new ComboBoxItem("Export Hooks", "export")
        });
        designerModulesCombo.SetItemSelected(0, true);
        designerModulesCombo.SetItemSelected(2, true);
        designerModulesCombo.SetItemSelected(3, true);

        var designerAccentPicker = new ColorPicker
        {
            Name = "designerAccentPicker",
            Dock = Orivy.DockStyle.Top,
            Height = 346,
            Margin = new(0, 0, 0, 14),
            ReferenceColor = ColorScheme.Primary,
            SelectedColor = ColorScheme.Primary,
            ShowAlphaChannel = true,
            ShowReferenceSwatch = true
        };

        void UpdateDesignerControlStatus()
        {
            var surfaceText = designerSurfaceCombo.SelectedItem is ComboBoxItem surfaceItem ? surfaceItem.Text : designerSurfaceCombo.Text;
            var motionText = designerMotionCombo.SelectedItem is ComboBoxItem motionItem ? motionItem.Text : designerMotionCombo.Text;
            var modulesText = string.IsNullOrWhiteSpace(designerModulesCombo.Text) ? "None" : designerModulesCombo.Text;
            designerControlStatus.Text = $"Designer Status\nSurface: {surfaceText}. Motion: {motionText}. Modules: {modulesText}. Accent: {designerAccentPicker.HexValue}.";
        }

        designerMotionCombo.SelectionChangeCommitted += (_, _) =>
        {
            if (designerMotionCombo.SelectedValue is not OpeningEffectType effect)
                return;

            designerSurfaceCombo.DropDownOpeningEffect = effect;
            designerModulesCombo.DropDownOpeningEffect = effect;
            UpdateDesignerControlStatus();
        };

        designerSurfaceCombo.SelectionChangeCommitted += (_, _) => UpdateDesignerControlStatus();
        designerModulesCombo.SelectionChangeCommitted += (_, _) => UpdateDesignerControlStatus();
        designerAccentPicker.SelectedColorCommitted += (_, _) =>
        {
            var accent = designerAccentPicker.SelectedColor;
            if (accent != SKColors.Transparent)
                ColorScheme.SetPrimarySeedColor(accent);

            UpdateDesignerControlStatus();
        };
        designerAccentPicker.SelectedColorChanged += (_, _) => UpdateDesignerControlStatus();

        UpdateDesignerControlStatus();

        designerControlShell.Controls.Add(designerControlStatus);
        designerControlShell.Controls.Add(designerAccentPicker);
        designerControlShell.Controls.Add(designerModulesCombo);
        designerControlShell.Controls.Add(designerMotionCombo);
        designerControlShell.Controls.Add(designerSurfaceCombo);

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
            AccentMotionEnabled = true
        };

        var scrollLabActionB = new Button
        {
            Name = "scrollLabActionB",
            Text = "Second Button - Hover Then Wheel",
            Dock = Orivy.DockStyle.Top,
            Height = 46,
            Margin = new(0, 0, 0, 16),
            AccentMotionEnabled = false
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
            AccentMotionEnabled = true
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

        visualStyleDangerCard.Click += VisualStyleDangerToggle_Click;
        visualStylePrimaryButton.Click += VisualStylePrimaryButton_Click;
        visualStyleFooterAction.Click += VisualStyleEnableDisabled_Click;

        this.panel3.Controls.Add(designerControlShell);
        this.panel3.Controls.Add(designerControlHeader);

        this.panel4.Controls.Add(this.visualStyleScrollProbe);
        this.panel4.Controls.Add(this.visualStyleFooterAction);
        this.panel4.Controls.Add(this.visualStyleGhostButton);
        this.panel4.Controls.Add(this.visualStylePrimaryButton);
        this.panel4.Controls.Add(this.visualStyleDisabledCard);
        this.panel4.Controls.Add(this.visualStyleDangerCard);
        this.panel4.Controls.Add(this.visualStyleInteractiveCard);
        this.panel4.Controls.Add(this.visualStyleMotionHero);
        this.panel4.Controls.Add(this.visualStyleHeader);

        this.panel5.Controls.Add(scrollLabLongTail);
        this.panel5.Controls.Add(scrollLabNestedShell);
        this.panel5.Controls.Add(scrollLabActionB);
        this.panel5.Controls.Add(scrollLabActionA);
        this.panel5.Controls.Add(scrollLabWheelCards);
        this.panel5.Controls.Add(scrollLabHeader);

        var gridListHeader = new Element
        {
            Name = "gridListHeader",
            Text = "Grid List Surface\nAnimated groups, sticky header, column resize, optional row resize and denser typography are all visible without leaving this page.",
            Dock = Orivy.DockStyle.Top,
            Height = 124,
            Padding = new(24),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceContainerHigh,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(92),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 15f)
        };

        var gridListToolbar = new Element
        {
            Name = "gridListToolbar",
            Dock = Orivy.DockStyle.Top,
            Height = 72,
            Padding = new(10),
            Margin = new(0, 0, 0, 16),
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(92),
            BackColor = ColorScheme.Surface
        };

        this.gridListToggleHeaderButton = new Button
        {
            Name = "gridListToggleHeaderButton",
            Text = "Header: On",
            Dock = Orivy.DockStyle.Left,
            Width = 128,
            Margin = new(0, 0, 10, 0),
            AccentMotionEnabled = true
        };

        this.gridListToggleStickyButton = new Button
        {
            Name = "gridListToggleStickyButton",
            Text = "Sticky: On",
            Dock = Orivy.DockStyle.Left,
            Width = 128,
            Margin = new(0, 0, 10, 0),
            AccentMotionEnabled = false
        };

        this.gridListToggleGroupingButton = new Button
        {
            Name = "gridListToggleGroupingButton",
            Text = "Grouping: On",
            Dock = Orivy.DockStyle.Left,
            Width = 144,
            Margin = new(0, 0, 10, 0),
            AccentMotionEnabled = false
        };

        this.gridListToggleGridLinesButton = new Button
        {
            Name = "gridListToggleGridLinesButton",
            Text = "Grid Lines: On",
            Dock = Orivy.DockStyle.Left,
            Width = 152,
            Margin = new(0, 0, 0, 0),
            AccentMotionEnabled = false
        };

        this.gridListToggleRowResizeButton = new Button
        {
            Name = "gridListToggleRowResizeButton",
            Text = "Row Resize: Off",
            Dock = Orivy.DockStyle.Left,
            Width = 164,
            Margin = new(0, 0, 10, 0),
            AccentMotionEnabled = false
        };

        gridListToolbar.Controls.Add(this.gridListToggleGridLinesButton);
        gridListToolbar.Controls.Add(this.gridListToggleRowResizeButton);
        gridListToolbar.Controls.Add(this.gridListToggleGroupingButton);
        gridListToolbar.Controls.Add(this.gridListToggleStickyButton);
        gridListToolbar.Controls.Add(this.gridListToggleHeaderButton);

        var gridListWorkspace = new Element
        {
            Name = "gridListWorkspace",
            Dock = Orivy.DockStyle.Top,
            Height = 860,
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0),
            Padding = new(0),
            Margin = new(0, 0, 0, 8)
        };

        var gridListInspectorRail = new Element
        {
            Name = "gridListInspectorRail",
            Dock = Orivy.DockStyle.Right,
            Width = 328,
            Padding = new(0),
            Margin = new(16, 0, 0, 0),
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0)
        };

        this.gridListStatus = new Element
        {
            Name = "gridListStatus",
            Text = "Status\nReady",
            Dock = Orivy.DockStyle.Top,
            Height = 110,
            Padding = new(18),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(96),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 11f)
        };

        var gridListPrimaryShell = new Element
        {
            Name = "gridListPrimaryShell",
            Dock = Orivy.DockStyle.Fill,
            Padding = new(16),
            Margin = new(0),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var gridListPrimaryIntro = new Element
        {
            Name = "gridListPrimaryIntro",
            Text = "Operations Board\nScroll inside the grid to verify sticky header. Resize columns from the header edge, then enable row resize from the toolbar to stretch the body rhythm.",
            Dock = Orivy.DockStyle.Top,
            Height = 84,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(78),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        this.gridListPrimary = new GridList
        {
            Name = "gridListPrimary",
            Dock = Orivy.DockStyle.Fill,
            Margin = new(0),
            Radius = new(14),
            Border = new(1),
            HeaderVisible = true,
            StickyHeader = true,
            GroupingEnabled = true,
            MultiSelect = true,
            FullRowSelect = true,
            CheckBoxes = false,
            AllowColumnResize = true,
            AllowRowResize = true,
            ShowGridLines = true,
            HeaderHeight = 42,
            RowHeight = 38,
            GroupHeaderHeight = 32,
            CellPadding = 11,
        };

        gridListPrimaryShell.Controls.Add(this.gridListPrimary);
        gridListPrimaryShell.Controls.Add(gridListPrimaryIntro);

        var gridListCompactShell = new Element
        {
            Name = "gridListCompactShell",
            Dock = Orivy.DockStyle.Top,
            Height = 286,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(22),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var gridListCompactHeader = new Element
        {
            Name = "gridListCompactHeader",
            Text = "Compact Feed\nHeaderless mode for icon-first rows and faster scanning.",
            Dock = Orivy.DockStyle.Top,
            Height = 72,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Success.WithAlpha(86),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        this.gridListCompact = new GridList
        {
            Name = "gridListCompact",
            Dock = Orivy.DockStyle.Fill,
            Radius = new(14),
            Border = new(1),
            HeaderVisible = false,
            StickyHeader = false,
            GroupingEnabled = false,
            MultiSelect = true,
            FullRowSelect = true,
            ShowGridLines = false,
            AllowRowResize = true,
            RowHeight = 36,
            CellPadding = 11,
        };

        gridListCompactShell.Controls.Add(this.gridListCompact);
        gridListCompactShell.Controls.Add(gridListCompactHeader);

        var gridListFooter = new Element
        {
            Name = "gridListFooter",
            Text = "Guide\n1. Scroll inside the primary grid to verify sticky header.\n2. Collapse a group and watch the rows animate.\n3. Enable row resize only when you want variable density.",
            Dock = Orivy.DockStyle.Fill,
            Padding = new(18),
            Margin = new(0),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(22),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        gridListInspectorRail.Controls.Add(gridListFooter);
        gridListInspectorRail.Controls.Add(gridListCompactShell);
        gridListInspectorRail.Controls.Add(this.gridListStatus);

        gridListWorkspace.Controls.Add(gridListPrimaryShell);
        gridListWorkspace.Controls.Add(gridListInspectorRail);

        this.panel6.Controls.Add(gridListWorkspace);
        this.panel6.Controls.Add(gridListToolbar);
        this.panel6.Controls.Add(gridListHeader);

        this.gridListToggleHeaderButton.Click += GridListToggleHeaderButton_Click;
        this.gridListToggleStickyButton.Click += GridListToggleStickyButton_Click;
        this.gridListToggleGroupingButton.Click += GridListToggleGroupingButton_Click;
        this.gridListToggleGridLinesButton.Click += GridListToggleGridLinesButton_Click;
        this.gridListToggleRowResizeButton.Click += GridListToggleRowResizeButton_Click;

        windowPageControl = new()
        {
            Name = "windowPageControl",
            Dock = Orivy.DockStyle.Fill,
            TabMode = WindowPageTabMode.WindowChrome,
            DrawTabIcons = true,
            TransitionEffect = WindowPageTransitionEffect.ScaleFade,
            TransitionAnimationType = AnimationType.QuarticEaseOut,
            TransitionDurationMs = 350,
            LockInputDuringTransition = true,
        };

        var designerTabIcon = CreateGridListIcon(new SKColor(0xF5, 0x9E, 0x0B), GridListIconKind.Pulse);
        var stylesTabIcon = CreateGridListIcon(new SKColor(0xEC, 0x48, 0x99), GridListIconKind.Healthy);
        var scrollTabIcon = CreateGridListIcon(new SKColor(0x14, 0xB8, 0xA6), GridListIconKind.Pulse);
        var gridTabIcon = CreateGridListIcon(new SKColor(0x8B, 0x5C, 0xF6), GridListIconKind.Warning);
        var notificationsTabIcon = CreateGridListIcon(new SKColor(0xEF, 0x44, 0x44), GridListIconKind.Warning);
        var bindingTabIcon = CreateGridListIcon(new SKColor(0x10, 0xB9, 0x81), GridListIconKind.Healthy);

        this.panel3.Image = designerTabIcon;
        this.panel4.Image = stylesTabIcon;
        this.panel5.Image = scrollTabIcon;
        this.panel6.Image = gridTabIcon;

        // build example menu strip demonstrating top‑level menus and submenus
        this.menuStrip = new MenuStrip
        {
            Name = "menuStrip",
            Dock = DockStyle.Top,
            ShowSubmenuArrow = false,
        };

        
        // use extension helpers for concise syntax
        var fileMenu = this.menuStrip.AddMenuItem("File");
        fileMenu.AddMenuItem("Open", (s, e) => { /* nop */ }, Keys.Control | Keys.O);
        fileMenu.AddSeparator();
        fileMenu.AddMenuItem("Exit", (s, e) => this.Close(), Keys.Control | Keys.X);

        var helpMenu = this.menuStrip.AddMenuItem("Help");
        helpMenu.AddMenuItem("About", (s, e) =>
        {
            Debug.WriteLine("Orivy Example\nA simple demo of the Orivy UI framework.\n\nhttps://github.com/mahmutyildirim/orivy");
        });

        var transitionsMenu = this.menuStrip.AddMenuItem("Transitions");
        InitializeTransitionMenu(transitionsMenu);

        var windowMenu = this.menuStrip.AddMenuItem("Window");
        InitializeWindowMenu(windowMenu);

        // --- ExtendMenu: drop-down that appears when the extend button (⋯) in
        // the title bar is clicked. ExtendBox must be true to show the button.
        this.extendMenu = new ContextMenuStrip();
        
        this.extendMenu.AddMenuItem("Settings", (s, e) => Debug.WriteLine("Settings clicked"), Keys.Control | Keys.O);
        this.extendMenu.AddMenuItem("Check for Updates", (s, e) => Debug.WriteLine("Update check"));
        this.extendMenu.AddSeparator();
        InitializeWindowMenu(this.extendMenu.AddMenuItem("Window"));
        var extendTransitionsMenu = this.extendMenu.AddMenuItem("Page Transition");
        InitializeTransitionMenu(extendTransitionsMenu);

        // assign a real icon so the title bar shows one; the menu glyph option
        // below can be toggled to switch behaviour
        this.Icon = System.Drawing.SystemIcons.Application;
        // Uncomment to replace the icon with a tiny menu glyph:
        // this.ShowMenuInsteadOfIcon = true;

        // wire up ExtendBox + ExtendMenu
        this.ExtendBox = true;
        this.extendMenu.UseAccordionSubmenus = true;
        this.ExtendMenu = this.extendMenu;
        this.ShowMenuInsteadOfIcon = true;
        this.FormMenu = this.extendMenu;

        windowPageControl.Controls.Add(panel3);
        windowPageControl.Controls.Add(panel4);
        windowPageControl.Controls.Add(panel5);
        windowPageControl.Controls.Add(panel6);

        InitializeBindingDemoPage();
        InitializeGridListDemo();
        InitializeNotificationsPage();
        InitializeEmbeddedTabsPage();

        if (_bindingPanel != null)
            _bindingPanel.Image = bindingTabIcon;

        if (panel7 != null)
            panel7.Image = notificationsTabIcon;

        extendMenu.ShowShortcutKeys = true;
        menuStrip.ShowShortcutKeys = true;

        // 
        // MainWindow
        // 
        this.Name = "MainWindow";
        this.Text = "Orivy Example";
        this.Width = 1100;
        this.Height = 650;
        this.DwmMargin = 1000;
        this.Padding = new(10);
        this.WindowThemeType = WindowThemeType.Tabbed;
        RefreshWindowThemeMenuChecks();
        this.ContextMenuStrip = this.extendMenu;
        this.WindowPageControl = windowPageControl;
        this.FormStartPosition = Orivy.FormStartPosition.CenterScreen;
        this.RenderBackend = Orivy.Rendering.RenderBackend.Software;
        this.Controls.Add(this.windowPageControl);
        this.Controls.Add(this.menuStrip);
        this.menuStrip.BringToFront();
        this.ResumeLayout(false);
    }

    private void InitializeNotificationsPage()
    {
        panel7 = new Container
        {
            Name    = "panel7",
            Text    = "Notifications",
            Padding = new Thickness(28),
            Dock    = DockStyle.Fill,
            Radius  = new Radius(0),
            Border  = new Thickness(0),
        };

        var notifHeader = new Element
        {
            Text      = "Notification Surface\nAlert-style toasts, global stack mode, dialog presentation, center positioning, inline actions, theme modes and the manual progress API are all demonstrated on this page.",
            Dock      = DockStyle.Top,
            Height    = 88,
            Padding   = new Thickness(18),
            Margin    = new Thickness(0, 0, 0, 20),
            Radius    = new Radius(16),
            Border    = new Thickness(1),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        notifHeader.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(ColorScheme.SurfaceVariant)
                .Foreground(ColorScheme.ForeColor)
                .BorderColor(ColorScheme.Outline)
                .Radius(16)));

        var notifRow1 = new Container
        {
            Dock    = DockStyle.Top,
            Height  = 46,
            Margin  = new Thickness(0, 0, 0, 10),
            Radius  = new Radius(0),
            Border  = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnInfo = new Button
        {
            Text   = "Info",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnSuccess = new Button
        {
            Text   = "Success",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnSuccess.ConfigureVisualStyles(s => s
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(new SkiaSharp.SKColor(22, 163, 74))
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(new SkiaSharp.SKColor(15, 118, 55))
                .Radius(12)
                .Shadow(new BoxShadow(0f, 6f, 14f, 0, ColorScheme.ShadowColor.WithAlpha(26))))
            .OnHover(r => r
                .Background(new SkiaSharp.SKColor(34, 197, 94))
                .BorderColor(new SkiaSharp.SKColor(22, 163, 74))));

        notifBtnWarning = new Button
        {
            Text   = "Warning",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnWarning.ConfigureVisualStyles(s => s
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(new SkiaSharp.SKColor(202, 138, 4))
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(new SkiaSharp.SKColor(161, 102, 3))
                .Radius(12)
                .Shadow(new BoxShadow(0f, 6f, 14f, 0, ColorScheme.ShadowColor.WithAlpha(26))))
            .OnHover(r => r
                .Background(new SkiaSharp.SKColor(234, 179, 8))
                .BorderColor(new SkiaSharp.SKColor(202, 138, 4))));

        notifBtnError = new Button
        {
            Text   = "Error",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
        };

        notifBtnError.ConfigureVisualStyles(s => s
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(ColorScheme.Error)
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(ColorScheme.Error.Brightness(-0.18f))
                .Radius(12)
                .Shadow(new BoxShadow(0f, 6f, 14f, 0, ColorScheme.ShadowColor.WithAlpha(26))))
            .OnHover(r => r
                .Background(ColorScheme.Error.Brightness(0.06f))
                .BorderColor(ColorScheme.Error.Brightness(-0.08f))));

        var notifRow2 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnAllFour = new Button
        {
            Text   = "Show All Four",
            Dock   = DockStyle.Left,
            Width  = 148,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnDismissAll = new Button
        {
            Text   = "Dismiss All",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
        };

        notifBtnDismissAll.ConfigureVisualStyles(s => s
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(ColorScheme.SurfaceVariant)
                .Foreground(ColorScheme.ForeColor)
                .Border(1)
                .BorderColor(ColorScheme.Outline)
                .Radius(12))
            .OnHover(r => r
                .Background(ColorScheme.SurfaceVariant.Brightness(0.06f))
                .BorderColor(ColorScheme.Primary)));

        var notifRow3 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnLongMessage = new Button
        {
            Text   = "Long Message",
            Dock   = DockStyle.Left,
            Width  = 148,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnLongDuration = new Button
        {
            Text   = "8-Second Timer",
            Dock   = DockStyle.Left,
            Width  = 148,
            Height = 38,
        };

        notifRow1.Controls.Add(notifBtnInfo);
        notifRow1.Controls.Add(notifBtnSuccess);
        notifRow1.Controls.Add(notifBtnWarning);
        notifRow1.Controls.Add(notifBtnError);

        notifRow2.Controls.Add(notifBtnAllFour);
        notifRow2.Controls.Add(notifBtnDismissAll);

        notifRow3.Controls.Add(notifBtnLongMessage);
        notifRow3.Controls.Add(notifBtnLongDuration);

        var notifRow4 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnConfirm = new Button
        {
            Text   = "Confirm Dialog",
            Dock   = DockStyle.Left,
            Width  = 148,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };
        notifBtnConfirm.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(ColorScheme.Primary)
                .Foreground(SkiaSharp.SKColors.White)));

        notifBtnActions = new Button
        {
            Text   = "With Actions",
            Dock   = DockStyle.Left,
            Width  = 148,
            Height = 38,
        };

        notifRow4.Controls.Add(notifBtnConfirm);
        notifRow4.Controls.Add(notifBtnActions);

        var notifRow5 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnManualProgress = new Button
        {
            Text   = "Manual Progress",
            Dock   = DockStyle.Left,
            Width  = 156,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnProgressToggle = new Button
        {
            Text   = "Toggle Progress",
            Dock   = DockStyle.Left,
            Width  = 156,
            Height = 38,
        };

        notifRow5.Controls.Add(notifBtnManualProgress);
        notifRow5.Controls.Add(notifBtnProgressToggle);

        var notifRow6 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnThemeAuto = new Button
        {
            Text   = "Auto Mode",
            Dock   = DockStyle.Left,
            Width  = 124,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnThemeLight = new Button
        {
            Text   = "Light Mode",
            Dock   = DockStyle.Left,
            Width  = 124,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };
        notifBtnThemeLight.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(new SkiaSharp.SKColor(248, 250, 252))
                .Foreground(new SkiaSharp.SKColor(15, 23, 42))
                .Border(1)
                .BorderColor(new SkiaSharp.SKColor(203, 213, 225))
                .Radius(12)));

        notifBtnThemeDark = new Button
        {
            Text   = "Dark Mode",
            Dock   = DockStyle.Left,
            Width  = 124,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };
        notifBtnThemeDark.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(new SkiaSharp.SKColor(30, 41, 59))
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(new SkiaSharp.SKColor(71, 85, 105))
                .Radius(12)));

        notifBtnThemeCustom = new Button
        {
            Text   = "Custom Mode",
            Dock   = DockStyle.Left,
            Width  = 132,
            Height = 38,
        };
        notifBtnThemeCustom.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(new SkiaSharp.SKColor(14, 116, 144))
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(new SkiaSharp.SKColor(21, 94, 117))
                .Radius(12))
            .OnHover(r => r
                .Background(new SkiaSharp.SKColor(8, 145, 178))
                .BorderColor(new SkiaSharp.SKColor(14, 116, 144))));

        notifRow6.Controls.Add(notifBtnThemeAuto);
        notifRow6.Controls.Add(notifBtnThemeLight);
        notifRow6.Controls.Add(notifBtnThemeDark);
        notifRow6.Controls.Add(notifBtnThemeCustom);

        var notifPositionLabel = new Element
        {
            Text      = "Toast Position & Presentation",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 10, 0, 6),
            BackColor = SkiaSharp.SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        var notifRow7 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnTopLeft = new Button
        {
            Text   = "Top Left",
            Dock   = DockStyle.Left,
            Width  = 112,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnTopCenter = new Button
        {
            Text   = "Top Center",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnTopRight = new Button
        {
            Text   = "Top Right",
            Dock   = DockStyle.Left,
            Width  = 112,
            Height = 38,
        };

        var notifRow8 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnBottomLeft = new Button
        {
            Text   = "Bottom Left",
            Dock   = DockStyle.Left,
            Width  = 120,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnBottomCenter = new Button
        {
            Text   = "Bottom Center",
            Dock   = DockStyle.Left,
            Width  = 134,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnBottomRight = new Button
        {
            Text   = "Bottom Right",
            Dock   = DockStyle.Left,
            Width  = 128,
            Height = 38,
        };
        notifBtnBottomRight.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(ColorScheme.Primary)
                .Foreground(SkiaSharp.SKColors.White)));

        notifRow7.Controls.Add(notifBtnTopLeft);
        notifRow7.Controls.Add(notifBtnTopCenter);
        notifRow7.Controls.Add(notifBtnTopRight);

        notifRow8.Controls.Add(notifBtnBottomLeft);
        notifRow8.Controls.Add(notifBtnBottomCenter);
        notifRow8.Controls.Add(notifBtnBottomRight);

        var notifRow9 = new Container
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            Margin    = new Thickness(0, 0, 0, 10),
            Radius    = new Radius(0),
            Border    = new Thickness(0),
            BackColor = SkiaSharp.SKColors.Transparent,
        };

        notifBtnCenter = new Button
        {
            Text   = "Center",
            Dock   = DockStyle.Left,
            Width  = 112,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };

        notifBtnStackMode = new Button
        {
            Text   = "Stack Mode: Off",
            Dock   = DockStyle.Left,
            Width  = 144,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
        };
        notifBtnStackMode.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(new SkiaSharp.SKColor(15, 23, 42))
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(new SkiaSharp.SKColor(51, 65, 85))
                .Radius(12))
            .OnHover(r => r
                .Background(new SkiaSharp.SKColor(30, 41, 59))
                .BorderColor(new SkiaSharp.SKColor(71, 85, 105))));

        notifBtnDialog = new Button
        {
            Text   = "Dialog Toast",
            Dock   = DockStyle.Left,
            Width  = 128,
            Height = 38,
        };
        notifBtnDialog.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(ColorScheme.Primary.WithAlpha(220))
                .Foreground(SkiaSharp.SKColors.White)
                .Border(1)
                .BorderColor(ColorScheme.Primary)
                .Radius(12))
            .OnHover(r => r
                .Background(ColorScheme.Primary.Brightness(0.08f))
                .BorderColor(ColorScheme.Primary.Brightness(-0.08f))));

        notifRow9.Controls.Add(notifBtnCenter);
        notifRow9.Controls.Add(notifBtnStackMode);
        notifRow9.Controls.Add(notifBtnDialog);

        panel7.Controls.Add(notifRow9);
        panel7.Controls.Add(notifRow8);
        panel7.Controls.Add(notifRow7);
        panel7.Controls.Add(notifPositionLabel);
        panel7.Controls.Add(notifRow6);
        panel7.Controls.Add(notifRow5);
        panel7.Controls.Add(notifRow4);
        panel7.Controls.Add(notifRow3);
        panel7.Controls.Add(notifRow2);
        panel7.Controls.Add(notifRow1);
        panel7.Controls.Add(notifHeader);

        windowPageControl.Controls.Add(panel7);
        windowPageControl.PerformLayout();
        windowPageControl.Invalidate();

        notifBtnInfo.Click         += NotifBtnInfo_Click;
        notifBtnSuccess.Click      += NotifBtnSuccess_Click;
        notifBtnWarning.Click      += NotifBtnWarning_Click;
        notifBtnError.Click        += NotifBtnError_Click;
        notifBtnAllFour.Click      += NotifBtnAllFour_Click;
        notifBtnDismissAll.Click   += NotifBtnDismissAll_Click;
        notifBtnLongMessage.Click  += NotifBtnLongMessage_Click;
        notifBtnLongDuration.Click += NotifBtnLongDuration_Click;
        notifBtnConfirm.Click      += NotifBtnConfirm_Click;
        notifBtnActions.Click      += NotifBtnActions_Click;
        notifBtnManualProgress.Click += NotifBtnManualProgress_Click;
        notifBtnProgressToggle.Click += NotifBtnProgressToggle_Click;
        notifBtnThemeAuto.Click    += NotifBtnThemeAuto_Click;
        notifBtnThemeLight.Click   += NotifBtnThemeLight_Click;
        notifBtnThemeDark.Click    += NotifBtnThemeDark_Click;
        notifBtnThemeCustom.Click  += NotifBtnThemeCustom_Click;
        notifBtnTopLeft.Click      += NotifBtnTopLeft_Click;
        notifBtnTopCenter.Click    += NotifBtnTopCenter_Click;
        notifBtnTopRight.Click     += NotifBtnTopRight_Click;
        notifBtnBottomLeft.Click   += NotifBtnBottomLeft_Click;
        notifBtnBottomCenter.Click += NotifBtnBottomCenter_Click;
        notifBtnBottomRight.Click  += NotifBtnBottomRight_Click;
        notifBtnCenter.Click       += NotifBtnCenter_Click;
        notifBtnStackMode.Click    += NotifBtnStackMode_Click;
        notifBtnDialog.Click       += NotifBtnDialog_Click;
    }

    private void InitializeEmbeddedTabsPage()
    {
        var embeddedTabsPage = new Container
        {
            Name    = "panelEmbeddedTabs",
            Text    = "Tab Control",
            Image   = CreateGridListIcon(new SKColor(0x06, 0xB6, 0xD4), GridListIconKind.Pulse),
            Padding = new Thickness(24),
            Dock    = DockStyle.Fill,
            Radius  = new Radius(0),
            Border  = new Thickness(0),
        };

        // ── Main embedded tab control ─────────────────────────────────────────
        var embeddedPageControl = new WindowPageControl
        {
            Name                      = "embeddedPageControl",
            Dock                      = DockStyle.Fill,
            Padding                   = new Thickness(0),
            Radius                    = new Radius(14),
            Border                    = new Thickness(1),
            TabMode                   = WindowPageTabMode.Embedded,
            TabDesignMode             = WindowPageTabDesignMode.RoundedCompact,
            TabAlignment              = WindowPageTabAlignment.Start,
            TabCloseButton            = true,
            NewTabButton              = true,
            DrawTabIcons              = true,
            TabStripHeight            = 44,
            TransitionEffect          = WindowPageTransitionEffect.Fade,
            TransitionAnimationType   = AnimationType.Linear,
            TransitionDurationMs      = 300,
            LockInputDuringTransition = true,
            TextAlign                   = ContentAlignment.MiddleCenter,
        };
        _embeddedPageControl = embeddedPageControl;

        // ── Toolbar shell ─────────────────────────────────────────────────────
        var embeddedToolbar = new Container
        {
            Name       = "embeddedTabToolbar",
            Dock       = DockStyle.Top,
            Height     = 402,
            Margin     = new Thickness(0, 0, 0, 16),
            Padding    = new Thickness(16),
            Radius     = new Radius(16),
            Border     = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88),
            BackColor  = ColorScheme.SurfaceContainerHigh,
        };

        // ── Status bar ────────────────────────────────────────────────────────
        var embeddedModeStatus = new Element
        {
            Name      = "embeddedModeStatus",
            Dock      = DockStyle.Fill,
            Padding   = new Thickness(12, 0, 12, 0),
            Radius    = new Radius(10),
            Border    = new Thickness(1),
            BorderColor = ColorScheme.Primary.WithAlpha(80),
            BackColor = ColorScheme.Primary.WithAlpha(20),
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // ── Row 1 label ───────────────────────────────────────────────────────
        var designModeLabel = new Element
        {
            Text      = "Design Mode",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        // ── Row 1: design mode buttons ────────────────────────────────────────
        var embeddedModeButtons = new Container
        {
            Name      = "embeddedModeButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 12),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        // ── Row 2 label ───────────────────────────────────────────────────────
        var alignmentLabel = new Element
        {
            Text      = "Tab Alignment",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        // ── Row 2: alignment buttons ──────────────────────────────────────────
        var embeddedAlignmentButtons = new Container
        {
            Name      = "embeddedAlignmentButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 10),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var layoutLabel = new Element
        {
            Text      = "Tab Layout",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        var embeddedLayoutButtons = new Container
        {
            Name      = "embeddedLayoutButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 10),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        // ── Row 4 label ───────────────────────────────────────────────────────
        var textAlignmentLabel = new Element
        {
            Text      = "Text Alignment",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        // ── Row 4: text alignment buttons ─────────────────────────────────────
        var embeddedTextAlignButtons = new Container
        {
            Name      = "embeddedTextAlignButtons",
            Dock      = DockStyle.Top,
            Height    = 124,
            Margin    = new Thickness(0, 0, 0, 10),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var embeddedTextAlignTopButtons = new Container
        {
            Name      = "embeddedTextAlignTopButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 8),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var embeddedTextAlignMiddleButtons = new Container
        {
            Name      = "embeddedTextAlignMiddleButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 8),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var embeddedTextAlignBottomButtons = new Container
        {
            Name      = "embeddedTextAlignBottomButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        // ── Design mode buttons ───────────────────────────────────────────────
        Button MakeToolButton(string name, string text) => new Button
        {
            Name                = name,
            Text                = text,
            Dock                = DockStyle.Left,
            Width               = 88,
            Height              = 36,
            Margin              = new Thickness(0, 0, 8, 0),
            Radius              = new Radius(8),
            AccentMotionEnabled = false,
        };

        Button MakeTextAlignButton(string name, string text) => new Button
        {
            Name                = name,
            Text                = text,
            Dock                = DockStyle.Left,
            Width               = 108,
            Height              = 36,
            Margin              = new Thickness(0, 0, 8, 0),
            Radius              = new Radius(8),
            AccentMotionEnabled = false,
        };

        var roundedCompactModeButton = MakeToolButton("roundedCompactModeButton", "RoundedCompact");
        var rectangleModeButton      = MakeToolButton("rectangleModeButton",      "Rectangle");
        var roundedModeButton        = MakeToolButton("roundedModeButton",        "Rounded");
        var chromedModeButton        = MakeToolButton("chromedModeButton",        "Chromed");
        var pillModeButton           = MakeToolButton("pillModeButton",           "Pill");
        var outlinedModeButton       = MakeToolButton("outlinedModeButton",       "Outlined");
        var minimalModeButton        = MakeToolButton("minimalModeButton",        "Minimal");

        var startAlignButton  = MakeToolButton("startAlignButton",  "· Start");
        var centerAlignButton = MakeToolButton("centerAlignButton", "· Center");
        var endAlignButton    = MakeToolButton("endAlignButton",    "· End");

        var topLayoutButton    = MakeToolButton("topLayoutButton",    "Top");
        var leftLayoutButton   = MakeToolButton("leftLayoutButton",   "Left");
        var rightLayoutButton  = MakeToolButton("rightLayoutButton",  "Right");
        var bottomLayoutButton = MakeToolButton("bottomLayoutButton", "Bottom");

        var textAlignTopLeftButton      = MakeTextAlignButton("textAlignTopLeftButton",      "Top Left");
        var textAlignTopCenterButton    = MakeTextAlignButton("textAlignTopCenterButton",    "Top Center");
        var textAlignTopRightButton     = MakeTextAlignButton("textAlignTopRightButton",     "Top Right");
        var textAlignMiddleLeftButton   = MakeTextAlignButton("textAlignMiddleLeftButton",   "Middle Left");
        var textAlignMiddleCenterButton = MakeTextAlignButton("textAlignMiddleCenterButton", "Middle Center");
        var textAlignMiddleRightButton  = MakeTextAlignButton("textAlignMiddleRightButton",  "Middle Right");
        var textAlignBottomLeftButton   = MakeTextAlignButton("textAlignBottomLeftButton",   "Bottom Left");
        var textAlignBottomCenterButton = MakeTextAlignButton("textAlignBottomCenterButton", "Bottom Center");
        var textAlignBottomRightButton  = MakeTextAlignButton("textAlignBottomRightButton",  "Bottom Right");
        var textAlignButtons = new[]
        {
            textAlignTopLeftButton,
            textAlignTopCenterButton,
            textAlignTopRightButton,
            textAlignMiddleLeftButton,
            textAlignMiddleCenterButton,
            textAlignMiddleRightButton,
            textAlignBottomLeftButton,
            textAlignBottomCenterButton,
            textAlignBottomRightButton,
        };

        // ── Helper: visual active/inactive state for tool buttons ─────────────
        void SetButtonActive(Button btn, bool active)
        {
            btn.BackColor   = active ? ColorScheme.Primary : ColorScheme.Surface;
            btn.ForeColor   = active ? SKColors.White : ColorScheme.ForeColor;
            btn.BorderColor = active ? ColorScheme.Primary : ColorScheme.Outline.WithAlpha(100);
            btn.Invalidate();
        }

        // ── Apply design mode ─────────────────────────────────────────────────
        void ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode mode)
        {
            embeddedPageControl.TabDesignMode = mode;
            windowPageControl.TabDesignMode   = mode;

            var modeDesc = mode switch
            {
                WindowPageTabDesignMode.RoundedCompact => "RoundedCompact — muted full-width container, elevated card on selected tab.",
                WindowPageTabDesignMode.Rectangle      => "Rectangle — no container, subtle ghost hover, full-width primary indicator.",
                WindowPageTabDesignMode.Rounded        => "Rounded — muted segmented container, Surface card on selected.",
                WindowPageTabDesignMode.Pill           => "Pill — filled Primary pill on selected, no container background.",
                WindowPageTabDesignMode.Outlined       => "Outlined — classic 3-sided border tab, open bottom merges with content.",
                WindowPageTabDesignMode.Minimal        => "Minimal — no chrome, Primary left-accent bar and tint on selected.",
                _                                      => "Chromed — browser-style top-rounded tabs, Surface elevated on selected.",
            };
            var alignDesc = embeddedPageControl.TabAlignment switch
            {
                WindowPageTabAlignment.Center => "Center",
                WindowPageTabAlignment.End    => "End",
                _                             => "Start",
            };
            var layoutDesc = embeddedPageControl.TabLayoutMode switch
            {
                WindowPageTabLayoutMode.Left => "Left",
                WindowPageTabLayoutMode.Right => "Right",
                WindowPageTabLayoutMode.Bottom => "Bottom",
                _ => "Top",
            };
            embeddedModeStatus.Text = $"Mode: {modeDesc}\nAlignment: {alignDesc} · Layout: {layoutDesc}";

            SetButtonActive(roundedCompactModeButton, mode == WindowPageTabDesignMode.RoundedCompact);
            SetButtonActive(rectangleModeButton,      mode == WindowPageTabDesignMode.Rectangle);
            SetButtonActive(roundedModeButton,        mode == WindowPageTabDesignMode.Rounded);
            SetButtonActive(chromedModeButton,        mode == WindowPageTabDesignMode.Chromed);
            SetButtonActive(pillModeButton,           mode == WindowPageTabDesignMode.Pill);
            SetButtonActive(outlinedModeButton,       mode == WindowPageTabDesignMode.Outlined);
            SetButtonActive(minimalModeButton,        mode == WindowPageTabDesignMode.Minimal);
        }

        // ── Apply alignment ───────────────────────────────────────────────────
        void ApplyEmbeddedTabAlignment(WindowPageTabAlignment alignment)
        {
            embeddedPageControl.TabAlignment = alignment;
            windowPageControl.TabAlignment = alignment;
            ApplyEmbeddedTabDesignMode(embeddedPageControl.TabDesignMode);

            SetButtonActive(startAlignButton,  alignment == WindowPageTabAlignment.Start);
            SetButtonActive(centerAlignButton, alignment == WindowPageTabAlignment.Center);
            SetButtonActive(endAlignButton,    alignment == WindowPageTabAlignment.End);
        }

        void ApplyEmbeddedTabLayout(WindowPageTabLayoutMode layoutMode)
        {
            embeddedPageControl.TabLayoutMode = layoutMode;

            if (layoutMode == WindowPageTabLayoutMode.Top)
            {
                windowPageControl.TabMode = WindowPageTabMode.WindowChrome;
                windowPageControl.TabLayoutMode = WindowPageTabLayoutMode.Top;
            }
            else
            {
                windowPageControl.TabMode = WindowPageTabMode.Embedded;
                windowPageControl.TabLayoutMode = layoutMode;
            }

            ApplyEmbeddedTabDesignMode(embeddedPageControl.TabDesignMode);

            SetButtonActive(topLayoutButton,    layoutMode == WindowPageTabLayoutMode.Top);
            SetButtonActive(leftLayoutButton,   layoutMode == WindowPageTabLayoutMode.Left);
            SetButtonActive(rightLayoutButton,  layoutMode == WindowPageTabLayoutMode.Right);
            SetButtonActive(bottomLayoutButton, layoutMode == WindowPageTabLayoutMode.Bottom);
        }

        // ── Apply text alignment ──────────────────────────────────────────────
        void ApplyEmbeddedTextAlign(ContentAlignment align)
        {
            embeddedPageControl.TextAlign = align;
            windowPageControl.TextAlign = align;

            for (var buttonIndex = 0; buttonIndex < textAlignButtons.Length; buttonIndex++)
                SetButtonActive(textAlignButtons[buttonIndex], false);

            var activeButton = align switch
            {
                ContentAlignment.TopLeft => textAlignTopLeftButton,
                ContentAlignment.TopCenter => textAlignTopCenterButton,
                ContentAlignment.TopRight => textAlignTopRightButton,
                ContentAlignment.MiddleLeft => textAlignMiddleLeftButton,
                ContentAlignment.MiddleCenter => textAlignMiddleCenterButton,
                ContentAlignment.MiddleRight => textAlignMiddleRightButton,
                ContentAlignment.BottomLeft => textAlignBottomLeftButton,
                ContentAlignment.BottomCenter => textAlignBottomCenterButton,
                ContentAlignment.BottomRight => textAlignBottomRightButton,
                _ => textAlignMiddleRightButton,
            };

            SetButtonActive(activeButton, true);
        }

        roundedCompactModeButton.Click += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.RoundedCompact);
        rectangleModeButton.Click      += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.Rectangle);
        roundedModeButton.Click        += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.Rounded);
        chromedModeButton.Click        += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.Chromed);
        pillModeButton.Click           += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.Pill);
        outlinedModeButton.Click       += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.Outlined);
        minimalModeButton.Click        += (_, _) => ApplyEmbeddedTabDesignMode(WindowPageTabDesignMode.Minimal);

        startAlignButton.Click  += (_, _) => ApplyEmbeddedTabAlignment(WindowPageTabAlignment.Start);
        centerAlignButton.Click += (_, _) => ApplyEmbeddedTabAlignment(WindowPageTabAlignment.Center);
        endAlignButton.Click    += (_, _) => ApplyEmbeddedTabAlignment(WindowPageTabAlignment.End);

        topLayoutButton.Click    += (_, _) => ApplyEmbeddedTabLayout(WindowPageTabLayoutMode.Top);
        leftLayoutButton.Click   += (_, _) => ApplyEmbeddedTabLayout(WindowPageTabLayoutMode.Left);
        rightLayoutButton.Click  += (_, _) => ApplyEmbeddedTabLayout(WindowPageTabLayoutMode.Right);
        bottomLayoutButton.Click += (_, _) => ApplyEmbeddedTabLayout(WindowPageTabLayoutMode.Bottom);

        textAlignTopLeftButton.Click      += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.TopLeft);
        textAlignTopCenterButton.Click    += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.TopCenter);
        textAlignTopRightButton.Click     += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.TopRight);
        textAlignMiddleLeftButton.Click   += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.MiddleLeft);
        textAlignMiddleCenterButton.Click += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.MiddleCenter);
        textAlignMiddleRightButton.Click  += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.MiddleRight);
        textAlignBottomLeftButton.Click   += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.BottomLeft);
        textAlignBottomCenterButton.Click += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.BottomCenter);
        textAlignBottomRightButton.Click  += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.BottomRight);

        embeddedModeButtons.Controls.Add(minimalModeButton);
        embeddedModeButtons.Controls.Add(outlinedModeButton);
        embeddedModeButtons.Controls.Add(pillModeButton);
        embeddedModeButtons.Controls.Add(minimalModeButton);
        embeddedModeButtons.Controls.Add(outlinedModeButton);
        embeddedModeButtons.Controls.Add(pillModeButton);
        embeddedModeButtons.Controls.Add(chromedModeButton);
        embeddedModeButtons.Controls.Add(roundedModeButton);
        embeddedModeButtons.Controls.Add(rectangleModeButton);
        embeddedModeButtons.Controls.Add(roundedCompactModeButton);

        embeddedAlignmentButtons.Controls.Add(endAlignButton);
        embeddedAlignmentButtons.Controls.Add(centerAlignButton);
        embeddedAlignmentButtons.Controls.Add(startAlignButton);

        embeddedLayoutButtons.Controls.Add(bottomLayoutButton);
        embeddedLayoutButtons.Controls.Add(rightLayoutButton);
        embeddedLayoutButtons.Controls.Add(leftLayoutButton);
        embeddedLayoutButtons.Controls.Add(topLayoutButton);

        embeddedTextAlignTopButtons.Controls.Add(textAlignTopRightButton);
        embeddedTextAlignTopButtons.Controls.Add(textAlignTopCenterButton);
        embeddedTextAlignTopButtons.Controls.Add(textAlignTopLeftButton);

        embeddedTextAlignMiddleButtons.Controls.Add(textAlignMiddleRightButton);
        embeddedTextAlignMiddleButtons.Controls.Add(textAlignMiddleCenterButton);
        embeddedTextAlignMiddleButtons.Controls.Add(textAlignMiddleLeftButton);

        embeddedTextAlignBottomButtons.Controls.Add(textAlignBottomRightButton);
        embeddedTextAlignBottomButtons.Controls.Add(textAlignBottomCenterButton);
        embeddedTextAlignBottomButtons.Controls.Add(textAlignBottomLeftButton);

        embeddedTextAlignButtons.Controls.Add(embeddedTextAlignBottomButtons);
        embeddedTextAlignButtons.Controls.Add(embeddedTextAlignMiddleButtons);
        embeddedTextAlignButtons.Controls.Add(embeddedTextAlignTopButtons);

        embeddedToolbar.Controls.Add(embeddedModeStatus);
        embeddedToolbar.Controls.Add(embeddedTextAlignButtons);
        embeddedToolbar.Controls.Add(textAlignmentLabel);
        embeddedToolbar.Controls.Add(embeddedLayoutButtons);
        embeddedToolbar.Controls.Add(layoutLabel);
        embeddedToolbar.Controls.Add(embeddedAlignmentButtons);
        embeddedToolbar.Controls.Add(alignmentLabel);
        embeddedToolbar.Controls.Add(embeddedModeButtons);
        embeddedToolbar.Controls.Add(designModeLabel);

        // ── Tab page factory ──────────────────────────────────────────────────
        var overviewTabIcon    = CreateGridListIcon(new SKColor(0x22, 0xC5, 0x5E), GridListIconKind.Healthy);
        var workflowTabIcon    = CreateGridListIcon(new SKColor(0xF5, 0x9E, 0x0B), GridListIconKind.Pulse);
        var compositionTabIcon = CreateGridListIcon(new SKColor(0xA8, 0x55, 0xF7), GridListIconKind.Locked);
        var settingsTabIcon    = CreateGridListIcon(new SKColor(0xEF, 0x44, 0x44), GridListIconKind.Warning);

        Container CreateEmbeddedTabPage(
            string name, string title, SKImage icon,
            SKColor accentColor,
            string headlineText, string bodyText,
            params (string label, string value)[] stats)
        {
            var page = new Container
            {
                Name    = name,
                Text    = title,
                Image   = icon,
                Dock    = DockStyle.Fill,
                Padding = new Thickness(14),
                Radius  = new Radius(0),
                Border  = new Thickness(0),
            };

            // Hero card
            var hero = new Element
            {
                Text      = headlineText,
                Dock      = DockStyle.Top,
                Height    = 80,
                Padding   = new Thickness(18),
                Margin    = new Thickness(0, 0, 0, 12),
                Radius    = new Radius(12),
                Border    = new Thickness(1),
                BorderColor = accentColor.WithAlpha(130),
                BackColor = accentColor.WithAlpha(22),
                ForeColor = ColorScheme.ForeColor,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            // Body card
            var body = new Element
            {
                Text      = bodyText,
                Dock      = DockStyle.Top,
                Height    = 72,
                Padding   = new Thickness(16),
                Margin    = new Thickness(0, 0, 0, 12),
                Radius    = new Radius(10),
                Border    = new Thickness(1),
                BorderColor = ColorScheme.Outline.WithAlpha(90),
                BackColor = ColorScheme.Surface,
                ForeColor = ColorScheme.ForeColor,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            // Stats row
            if (stats.Length > 0)
            {
                var statsRow = new Container
                {
                    Dock      = DockStyle.Top,
                    Height    = 64,
                    Margin    = new Thickness(0, 0, 0, 0),
                    BackColor = SKColors.Transparent,
                    Border    = new Thickness(0),
                };

                foreach (var (label, value) in stats)
                {
                    var statCard = new Element
                    {
                        Text      = $"{value}\n{label}",
                        Dock      = DockStyle.Left,
                        Width     = 140,
                        Margin    = new Thickness(0, 0, 10, 0),
                        Padding   = new Thickness(14, 10, 14, 10),
                        Radius    = new Radius(10),
                        Border    = new Thickness(1),
                        BorderColor = ColorScheme.Outline.WithAlpha(80),
                        BackColor = ColorScheme.SurfaceContainerHigh,
                        ForeColor = ColorScheme.ForeColor,
                        TextAlign = ContentAlignment.MiddleLeft,
                    };
                    statsRow.Controls.Add(statCard);
                }

                page.Controls.Add(statsRow);
            }

            page.Controls.Add(body);
            page.Controls.Add(hero);
            return page;
        }

        embeddedPageControl.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabOverview", "Overview", overviewTabIcon,
            new SKColor(0x22, 0xC5, 0x5E),
            "Tab Strip — Embedded Mode\nThe control owns hit-testing, layout and rendering. No dependency on window chrome.",
            "Tab alignment (Start / Center / End) shifts the whole strip. Close and new-tab buttons adjust automatically. All four design modes share the same hit-test and animation pipeline.",
            ("Design Modes", "4"), ("Alignments", "3"), ("Transitions", "11")));

        embeddedPageControl.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabWorkflow", "Workflow", workflowTabIcon,
            new SKColor(0xF5, 0x9E, 0x0B),
            "Animated Page Transitions\nEvery tab switch goes through the snapshot-based transition engine.",
            "ScaleFade, Push, Cover, Reveal, Iris and six more effects work identically in Embedded mode. Switch a design mode and the tab strip redraws on the next frame with zero layout recalculation.",
            ("Effects", "11"), ("Easing Curves", "10"), ("Duration Range", "100 – 1 000 ms")));

        embeddedPageControl.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabComposition", "Composition", compositionTabIcon,
            new SKColor(0xA8, 0x55, 0xF7),
            "Nested Page Controls\nAn embedded strip can live inside any Container, panel or inspector rail.",
            "The outer window here uses WindowChrome tabs. This inner control uses Embedded mode — both run inside the same render loop with no coordination overhead.",
            ("Nesting Depth", "Unlimited"), ("DPI Aware", "Yes"), ("ScaleFactor", "1 × – 4 ×")));

        embeddedPageControl.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabSettings", "Settings", settingsTabIcon,
            new SKColor(0xEF, 0x44, 0x44),
            "Runtime Configuration\nDesign mode and alignment update live — no rebuild, no layout pass.",
            "RoundedCompact renders a card-lift effect. Rectangle draws a full-width primary indicator. Rounded builds a segmented control. Chromed uses a top-rounded browser-tab silhouette.",
            ("Live Swap", "Yes"), ("Repaint", "1 Frame"), ("CPU Alloc", "~0 B/frame")));

        // ── Seed initial state ────────────────────────────────────────────────
        ApplyEmbeddedTabDesignMode(embeddedPageControl.TabDesignMode);
        ApplyEmbeddedTabAlignment(embeddedPageControl.TabAlignment);
        ApplyEmbeddedTabLayout(embeddedPageControl.TabLayoutMode);
        ApplyEmbeddedTextAlign(embeddedPageControl.TextAlign);

        embeddedPageControl.NewTabButtonClick += (_, _) =>
            NotificationToast.Show(
                "New Tab",
                "A new tab was requested from the embedded tab strip.",
                NotificationKind.Info);

        embeddedPageControl.TabCloseButtonClick += (_, tabIndex) =>
        {
            var page         = embeddedPageControl.GetPageAt(tabIndex);
            var tabTitle     = page?.Text ?? $"Tab {tabIndex + 1}";
            NotificationToast.Show(
                "Tab Closed",
                $"\u201c{tabTitle}\u201d tab close was requested.",
                NotificationKind.Warning);
        };

        embeddedTabsPage.Controls.Add(embeddedPageControl);
        embeddedTabsPage.Controls.Add(embeddedToolbar);

        windowPageControl.Controls.Add(embeddedTabsPage);
        windowPageControl.PerformLayout();
        windowPageControl.Invalidate();
    }

    private Button notifBtnInfo;
    private Button notifBtnSuccess;
    private Button notifBtnWarning;
    private Button notifBtnError;
    private Button notifBtnAllFour;
    private Button notifBtnDismissAll;
    private Button notifBtnLongMessage;
    private Button notifBtnLongDuration;
    private Button notifBtnConfirm;
    private Button notifBtnActions;
    private Button notifBtnManualProgress;
    private Button notifBtnProgressToggle;
    private Button notifBtnThemeAuto;
    private Button notifBtnThemeLight;
    private Button notifBtnThemeDark;
    private Button notifBtnThemeCustom;
    private Button notifBtnTopLeft;
    private Button notifBtnTopCenter;
    private Button notifBtnTopRight;
    private Button notifBtnBottomLeft;
    private Button notifBtnBottomCenter;
    private Button notifBtnBottomRight;
    private Button notifBtnCenter;
    private Button notifBtnStackMode;
    private Button notifBtnDialog;

    private void InitializeBindingDemoPage()
    {
        _bindingPanel = new Container
        {
            Name = "bindingPanel",
            Text = "Binding Lab",
            Padding = new Thickness(24),
            Dock = DockStyle.Fill,
            Radius = new Radius(0),
            Border = new Thickness(0),
            AutoScroll = true,
            AutoScrollMargin = new SKSize(0, 24)
        }.SetDataContext(_bindingDemoViewModel);

        var header = CreateBindingCard(
            "bindingHeader",
            "Binding Lab\nOrivy keeps state flow local to the control tree. Bindings start from the consumer control, and user actions stay on the existing event system instead of forcing command objects everywhere.",
            110,
            new Thickness(0, 0, 0, 16),
            ColorScheme.SurfaceContainerHigh);

        var snippet = CreateBindingCard(
            "bindingSnippet",
            "panel.SetDataContext(viewModel)\nhero.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.HeroText).OneWay()\npresetCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedPreset).TwoWay()\ndeployButton.When<BindingDemoViewModel>(\"Click\", (vm, _) => vm.MarkDeployed(vm.DeploymentPayload))",
            132,
            new Thickness(0, 0, 0, 14),
            ColorScheme.SurfaceVariant);

        var hero = CreateBindingCard("bindingHero", string.Empty, 108, new Thickness(0, 0, 0, 14), ColorScheme.Primary.WithAlpha(230));
        hero.ForeColor = SKColors.White;
        hero.Border = new Thickness(0);
        hero.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.HeroText).OneWay();
        hero.Link(view => view.BackColor).FromData((BindingDemoViewModel vm) => vm.AccentColor).OneWay();

        var summary = CreateBindingCard("bindingSummary", string.Empty, 92, new Thickness(0, 0, 0, 14), ColorScheme.SurfaceContainer);
        summary.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.SummaryText).OneWay();

        var alertCard = CreateBindingCard("bindingAlertCard", string.Empty, 78, new Thickness(0, 0, 0, 14), new SKColor(185, 28, 28));
        alertCard.ForeColor = SKColors.White;
        alertCard.Border = new Thickness(0);
        alertCard.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.AlertText).OneWay();
        alertCard.Link(view => view.Visible).FromData((BindingDemoViewModel vm) => vm.AlertVisible).OneWay();

        var statusCard = CreateBindingCard("bindingStatus", string.Empty, 94, new Thickness(0, 0, 0, 16), ColorScheme.SurfaceContainerHigh);
        statusCard.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.StatusText).OneWay();

        var selectionRow = new Container
        {
            Name = "bindingSelectionRow",
            Dock = DockStyle.Top,
            Height = 52,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Black.WithAlpha(12),
            Border = new Thickness(0)
        };

        var teamCombo = new ComboBox
        {
            Name = "bindingTeamCombo",
            Dock = DockStyle.Right,
            Width = 240,
            Margin = new Thickness(12, 0, 0, 0)
        };
        teamCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Teams).OneWay();
        teamCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedTeam).TwoWay();

        var presetCombo = new ComboBox
        {
            Name = "bindingPresetCombo",
            Dock = DockStyle.Fill
        };
        presetCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Presets).OneWay();
        presetCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedPreset).TwoWay();
        selectionRow.Controls.Add(teamCombo);
        selectionRow.Controls.Add(presetCombo);

        var actionRow = new Container
        {
            Name = "bindingActionRow",
            Height = 42,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var deployButton = new Button
        {
            Name = "bindingDeployButton",
            Dock = DockStyle.Right,
            Width = 190,
            Margin = new Thickness(12, 0, 0, 0)
        };
        deployButton.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.DeployActionText).OneWay();
        deployButton.Link(view => view.Enabled).FromData((BindingDemoViewModel vm) => vm.CanDeploy).OneWay();
        deployButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.MarkDeployed(vm.DeploymentPayload));

        var addTeamButton = new Button
        {
            Name = "bindingAddTeamButton",
            Dock = DockStyle.Right,
            Width = 150,
            Margin = new Thickness(12, 0, 0, 0),
            Text = "Add Team"
        };
        addTeamButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.AddTeam());

        var toggleAlertButton = new Button
        {
            Name = "bindingToggleAlertButton",
            Dock = DockStyle.Right,
            Width = 170,
            Margin = new Thickness(12, 0, 0, 0),
            Text = "Toggle Signal"
        };
        toggleAlertButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.ToggleAlert());

        var cyclePresetButton = new Button
        {
            Name = "bindingCyclePresetButton",
            Dock = DockStyle.Fill,
            Text = "Cycle Preset"
        };
        cyclePresetButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.CyclePreset());

        var taskShell = new Container
        {
            Name = "bindingTaskShell",
            Dock = DockStyle.Top,
            Height = 266,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Black.WithAlpha(12),
            Border = new Thickness(0)
        };

        var taskDetail = CreateBindingCard("bindingTaskDetail", string.Empty, 266, new Thickness(14, 0, 0, 0), ColorScheme.SurfaceContainerHigh);
        taskDetail.Dock = DockStyle.Right;
        taskDetail.Width = 300;
        taskDetail.Height = 266;
        taskDetail.Margin = new Thickness(14, 0, 0, 0);
        taskDetail.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.TaskDetailText).OneWay();

        var taskGrid = new GridList
        {
            Name = "bindingTaskGrid",
            Dock = DockStyle.Fill,
            HeaderVisible = true,
            StickyHeader = true,
            ShowGridLines = true,
            GroupingEnabled = false,
            MultiSelect = true,
            AutoScroll = false
        };
        taskGrid.Columns.Add(new GridListColumn { Name = "name", HeaderText = "Task", Width = 180f, MinWidth = 120f, SizeMode = GridListColumnSizeMode.Auto });
        taskGrid.Columns.Add(new GridListColumn { Name = "state", HeaderText = "State", Width = 92f, MinWidth = 72f, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        taskGrid.Columns.Add(new GridListColumn { Name = "lane", HeaderText = "Lane", Width = 110f, MinWidth = 84f, SizeMode = GridListColumnSizeMode.Auto });
        taskGrid.Columns.Add(new GridListColumn { Name = "summary", HeaderText = "Summary", Width = 260f, MinWidth = 180f, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.5f, Sortable = false });
        taskGrid.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Tasks).OneWay();
        taskGrid.Link(view => view.SelectedIndex).FromData((BindingDemoViewModel vm) => vm.SelectedTaskIndex).TwoWay();

        taskShell.Controls.Add(taskDetail);
        taskShell.Controls.Add(taskGrid);

        var taskActionRow = new Container
        {
            Name = "bindingTaskActionRow",
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var advanceTaskButton = new Button
        {
            Name = "bindingAdvanceTaskButton",
            Dock = DockStyle.Right,
            Width = 180,
            Margin = new Thickness(12, 0, 0, 0),
            Text = "Advance Task"
        };
        advanceTaskButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.AdvanceSelectedTask());

        var addTaskButton = new Button
        {
            Name = "bindingAddTaskButton",
            Dock = DockStyle.Fill,
            Text = "Add Task"
        };
        addTaskButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.AddTask());

        taskActionRow.Controls.Add(advanceTaskButton);
        taskActionRow.Controls.Add(addTaskButton);

        var taskFooter = CreateBindingCard("bindingTaskFooter", string.Empty, 84, new Thickness(0, 0, 0, 14), ColorScheme.SurfaceVariant);
        taskFooter.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.TaskFooterText).OneWay();

        var colorShell = new Container
        {
            Name = "bindingColorShell",
            Dock = DockStyle.Top,
            Height = 228,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var colorDetail = CreateBindingCard(
            "bindingColorDetail",
            "Color Sync\nThe ColorPicker is bound two-way. Move any channel and the hero surface above updates immediately because both are driven by the same view-model color property.",
            228,
            new Thickness(14, 0, 0, 0),
            ColorScheme.SurfaceContainerHigh);
        colorDetail.Dock = DockStyle.Right;
        colorDetail.Width = 300;
        colorDetail.Height = 228;

        var colorPicker = new ColorPicker
        {
            Name = "bindingColorPicker",
            Dock = DockStyle.Fill,
            Margin = new Thickness(0),
            Radius = new Radius(16),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(110),
            BackColor = ColorScheme.SurfaceContainer
        };
        colorPicker.Link(view => view.SelectedColor).FromData((BindingDemoViewModel vm) => vm.PickerAccentColor).TwoWay();

        colorShell.Controls.Add(colorDetail);
        colorShell.Controls.Add(colorPicker);

        var validationShell = new Container
        {
            Name = "bindingValidationShell",
            Dock = DockStyle.Top,
            Height = 306,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var validationGuide = CreateBindingCard(
            "bindingValidationGuide",
            "Validation Flow\nThese controls use the existing ValidationRule system. Error cards are bound directly to control ValidationText and HasValidationError, so no extra glue object is required.",
            306,
            new Thickness(14, 0, 0, 0),
            ColorScheme.SurfaceContainerHigh);
        validationGuide.Dock = DockStyle.Right;
        validationGuide.Width = 320;
        validationGuide.Height = 306;

        var validationStack = new Container
        {
            Name = "bindingValidationStack",
            Dock = DockStyle.Fill,
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        bindingValidationStatusCard = CreateBindingCard(
            "bindingValidationStatus",
            "Validation Submit\nChoose a team and a preset to satisfy the rules.",
            76,
            new Thickness(0, 0, 0, 12),
            ColorScheme.SurfaceVariant);

        bindingValidationRegionError = CreateBindingCard(
            "bindingValidationRegionError",
            string.Empty,
            52,
            new Thickness(0, 0, 0, 10),
            new SKColor(185, 28, 28));
        bindingValidationRegionError.ForeColor = SKColors.White;
        bindingValidationRegionError.Border = new Thickness(0);

        bindingValidationPresetError = CreateBindingCard(
            "bindingValidationPresetError",
            string.Empty,
            60,
            new Thickness(0, 0, 0, 10),
            new SKColor(127, 29, 29));
        bindingValidationPresetError.ForeColor = SKColors.White;
        bindingValidationPresetError.Border = new Thickness(0);

        bindingValidationSubmitButton = new Button
        {
            Name = "bindingValidationSubmitButton",
            Text = "Validate Workflow",
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Thickness(0, 0, 0, 10)
        };
        bindingValidationSubmitButton.Click += BindingValidationSubmitButton_Click;

        bindingValidationPresetCombo = new ComboBox
        {
            Name = "bindingValidationPresetCombo",
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Thickness(0, 0, 0, 10)
        };
        bindingValidationPresetCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Presets).OneWay();
        bindingValidationPresetCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedPreset).TwoWay();

        bindingValidationTeamCombo = new ComboBox
        {
            Name = "bindingValidationTeamCombo",
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Thickness(0, 0, 0, 10)
        };
        bindingValidationTeamCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Teams).OneWay();
        bindingValidationTeamCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedTeam).TwoWay();

        bindingValidationTeamCombo.AddValidationRule(new RequiredFieldValidationRule
        {
            ErrorMessage = "Choose a team before deployment validation can pass."
        });
        bindingValidationPresetCombo.AddValidationRule(new RequiredFieldValidationRule
        {
            ErrorMessage = "Choose a preset before deployment validation can pass."
        });
        bindingValidationPresetCombo.AddValidationRule(new CustomValidationRule(
            _ =>
            {
                var teamName = bindingValidationTeamCombo.SelectedItem as string ?? string.Empty;
                var presetName = bindingValidationPresetCombo.SelectedItem as string ?? string.Empty;

                if (presetName.IndexOf("Incident", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    teamName.IndexOf("Ops", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return (false, "Incident presets are reserved for Ops teams in this example workflow.");
                }

                return (true, string.Empty);
            }));

        bindingValidationTeamCombo.SelectedItemChanged += (_, _) =>
        {
            bindingValidationTeamCombo.ValidateNow();
            bindingValidationPresetCombo.ValidateNow();
        };
        bindingValidationPresetCombo.SelectedItemChanged += (_, _) => bindingValidationPresetCombo.ValidateNow();

        bindingValidationRegionError.Link(view => view.Text).From(bindingValidationTeamCombo, (ComboBox combo) => combo.ValidationText).OneWay();
        bindingValidationRegionError.Link(view => view.Visible).From(bindingValidationTeamCombo, (ComboBox combo) => combo.HasValidationError).OneWay();
        bindingValidationPresetError.Link(view => view.Text).From(bindingValidationPresetCombo, (ComboBox combo) => combo.ValidationText).OneWay();
        bindingValidationPresetError.Link(view => view.Visible).From(bindingValidationPresetCombo, (ComboBox combo) => combo.HasValidationError).OneWay();

        validationStack.Controls.Add(bindingValidationStatusCard);
        validationStack.Controls.Add(bindingValidationRegionError);
        validationStack.Controls.Add(bindingValidationPresetError);
        validationStack.Controls.Add(bindingValidationSubmitButton);
        validationStack.Controls.Add(bindingValidationPresetCombo);
        validationStack.Controls.Add(bindingValidationTeamCombo);

        validationShell.Controls.Add(validationGuide);
        validationShell.Controls.Add(validationStack);

        actionRow.Controls.Add(deployButton);
        actionRow.Controls.Add(addTeamButton);
        actionRow.Controls.Add(toggleAlertButton);
        actionRow.Controls.Add(cyclePresetButton);

        _bindingPanel.Controls.Add(statusCard);
        _bindingPanel.Controls.Add(validationShell);
        _bindingPanel.Controls.Add(colorShell);
        _bindingPanel.Controls.Add(taskFooter);
        _bindingPanel.Controls.Add(taskActionRow);
        _bindingPanel.Controls.Add(taskShell);
        _bindingPanel.Controls.Add(actionRow);
        _bindingPanel.Controls.Add(selectionRow);
        _bindingPanel.Controls.Add(alertCard);
        _bindingPanel.Controls.Add(summary);
        _bindingPanel.Controls.Add(hero);
        _bindingPanel.Controls.Add(snippet);
        _bindingPanel.Controls.Add(header);

        windowPageControl.Controls.Add(_bindingPanel);
        _bindingPanel.BringToFront();
        windowPageControl.PerformLayout();
        windowPageControl.Invalidate();
    }

    private static Element CreateBindingCard(string name, string text, float height, Thickness margin, SKColor background)
    {
        return new Element
        {
            Name = name,
            Text = text,
            Dock = DockStyle.Top,
            Height = (int)Math.Round(height),
            Margin = margin,
            Padding = new Thickness(16),
            Radius = new Radius(16),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(140),
            BackColor = background,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Container panel3;
    private Container panel4;
    private Container panel5;
    private Container panel6;
    private Container panel7;
    private Element visualStyleHeader;
    private Element visualStyleMotionHero;
    private Element visualStyleInteractiveCard;
    private Element visualStyleDangerCard;
    private Element visualStyleDisabledCard;
    private Element visualStyleFooterAction;
    private Button visualStylePrimaryButton;
    private Button visualStyleGhostButton;
    private Element visualStyleScrollProbe;
    private Element gridListStatus;
    private GridList gridListPrimary;
    private GridList gridListCompact;
    private Button gridListToggleHeaderButton;
    private Button gridListToggleStickyButton;
    private Button gridListToggleGroupingButton;
    private Button gridListToggleGridLinesButton;
    private Button gridListToggleRowResizeButton;

    private MenuStrip menuStrip;
    private ContextMenuStrip extendMenu;
    private WindowPageControl windowPageControl;
    private ComboBox bindingValidationTeamCombo;
    private ComboBox bindingValidationPresetCombo;
    private Element bindingValidationRegionError;
    private Element bindingValidationPresetError;
    private Element bindingValidationStatusCard;
    private Button bindingValidationSubmitButton;

}
