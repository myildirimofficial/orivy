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

internal sealed partial class EmbeddedTabsDemoPage
{
    private void InitializeComponent()
    {
        Text = "Tab Control";
        Image = CreateEmbeddedIcon(new SKColor(0x06, 0xB6, 0xD4), ExampleIconKind.Pulse);

        var embeddedTabsPage = new Container
        {
            Name    = "panelEmbeddedTabs",
            Text    = "Tab Control",
            Image   = CreateEmbeddedIcon(new SKColor(0x06, 0xB6, 0xD4), ExampleIconKind.Pulse),
            Padding = new Thickness(24),
            Dock    = DockStyle.Fill,
            Radius  = new Radius(0),
            Border  = new Thickness(0),
            AutoScroll = true
        };

        // Main embedded tab control
        var embeddedTabView = new TabView
        {
            Name                      = "embeddedTabView",
            Dock                      = DockStyle.Fill,
            Padding                   = new Thickness(0),
            Radius                    = new Radius(14),
            Border                    = new Thickness(1),
            TabMode                   = TabViewMode.Embedded,
            TabDesignMode             = TabViewDesignMode.RoundedCompact,
            TabAlignment              = TabViewAlignment.Start,
            TabCloseButton            = true,
            NewTabButton              = true,
            DrawTabIcons              = true,
            TabStripHeight            = 44,
            TransitionEffect          = TabViewTransitionEffect.Fade,
            TransitionAnimationType   = AnimationType.Linear,
            TransitionDurationMs      = 300,
            LockInputDuringTransition = true,
            TextAlign                   = ContentAlignment.MiddleCenter,
            MinimumSize = new SKSize(0, 300),
        };
        _embeddedTabView = embeddedTabView;

        // Toolbar shell
        var embeddedToolbar = new Container
        {
            Name       = "embeddedTabToolbar",
            Dock       = DockStyle.Top,
            Height     = 640,
            Margin     = new Thickness(0, 0, 0, 16),
            Padding    = new Thickness(16),
            Radius     = new Radius(16),
            Border     = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88),
            BackColor  = ColorScheme.SurfaceContainerHigh,
        };

        // Status bar
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

        // Row 1 label
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

        // Row 1: design mode buttons
        var embeddedModeButtons = new Container
        {
            Name      = "embeddedModeButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 12),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var customStyleLabel = new Element
        {
            Text      = "Custom Style",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        var customStyleButtons = new Container
        {
            Name      = "customStyleButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 12),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        // Row 2 label
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

        // Row 2: alignment buttons
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

        // Row 4 label
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

        // Row 4: text alignment buttons
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

        // Row 5 label
        var iconAlignmentLabel = new Element
        {
            Text      = "Icon Alignment",
            Dock      = DockStyle.Top,
            Height    = 22,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };

        // Row 5: icon alignment buttons
        var embeddedIconAlignButtons = new Container
        {
            Name      = "embeddedIconAlignButtons",
            Dock      = DockStyle.Top,
            Height    = 124,
            Margin    = new Thickness(0, 0, 0, 10),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var embeddedIconAlignTopButtons = new Container
        {
            Name      = "embeddedIconAlignTopButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 8),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var embeddedIconAlignMiddleButtons = new Container
        {
            Name      = "embeddedIconAlignMiddleButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            Margin    = new Thickness(0, 0, 0, 8),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        var embeddedIconAlignBottomButtons = new Container
        {
            Name      = "embeddedIconAlignBottomButtons",
            Dock      = DockStyle.Top,
            Height    = 36,
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
        };

        // Design mode buttons
        Button MakeToolButton(string name, string text) => new Button
        {
            Name                = name,
            Text                = text,
            Dock                = DockStyle.Left,
            Width               = 88,
            Height              = 36,
            Margin              = new Thickness(0, 0, 8, 0),
            Radius              = new Radius(8),
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
        };

        var roundedCompactModeButton = MakeToolButton("roundedCompactModeButton", "RoundedCompact");
        var rectangleModeButton      = MakeToolButton("rectangleModeButton",      "Rectangle");
        var roundedModeButton        = MakeToolButton("roundedModeButton",        "Rounded");
        var chromedModeButton        = MakeToolButton("chromedModeButton",        "Chromed");
        var pillModeButton           = MakeToolButton("pillModeButton",           "Pill");
        var outlinedModeButton       = MakeToolButton("outlinedModeButton",       "Outlined");
        var minimalModeButton        = MakeToolButton("minimalModeButton",        "Minimal");
        var fluentModeButton         = MakeToolButton("fluentModeButton",         "Fluent");
        var macOSModeButton          = MakeToolButton("macOSModeButton",          "MacOS");
        var customStyleButton        = MakeToolButton("customStyleButton",        "Custom");
        var clearStyleButton         = MakeToolButton("clearStyleButton",         "Clear");

        var startAlignButton  = MakeToolButton("startAlignButton",  "Start");
        var centerAlignButton = MakeToolButton("centerAlignButton", "Center");
        var endAlignButton    = MakeToolButton("endAlignButton",    "End");

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

        var iconAlignTopLeftButton      = MakeTextAlignButton("iconAlignTopLeftButton",      "Top Left");
        var iconAlignTopCenterButton    = MakeTextAlignButton("iconAlignTopCenterButton",    "Top Center");
        var iconAlignTopRightButton     = MakeTextAlignButton("iconAlignTopRightButton",     "Top Right");
        var iconAlignMiddleLeftButton   = MakeTextAlignButton("iconAlignMiddleLeftButton",   "Middle Left");
        var iconAlignMiddleCenterButton = MakeTextAlignButton("iconAlignMiddleCenterButton", "Middle Center");
        var iconAlignMiddleRightButton  = MakeTextAlignButton("iconAlignMiddleRightButton",  "Middle Right");
        var iconAlignBottomLeftButton   = MakeTextAlignButton("iconAlignBottomLeftButton",   "Bottom Left");
        var iconAlignBottomCenterButton = MakeTextAlignButton("iconAlignBottomCenterButton", "Bottom Center");
        var iconAlignBottomRightButton  = MakeTextAlignButton("iconAlignBottomRightButton",  "Bottom Right");
        var iconAlignButtons = new[]
        {
            iconAlignTopLeftButton,
            iconAlignTopCenterButton,
            iconAlignTopRightButton,
            iconAlignMiddleLeftButton,
            iconAlignMiddleCenterButton,
            iconAlignMiddleRightButton,
            iconAlignBottomLeftButton,
            iconAlignBottomCenterButton,
            iconAlignBottomRightButton,
        };

        // Helper: visual active/inactive state for tool buttons
        void SetButtonActive(Button btn, bool active)
        {
            btn.BackColor   = active ? ColorScheme.Primary : ColorScheme.Surface;
            btn.ForeColor   = active ? SKColors.White : ColorScheme.ForeColor;
            btn.BorderColor = active ? ColorScheme.Primary : ColorScheme.Outline.WithAlpha(100);
            btn.Invalidate();
        }

        // Apply design mode
        void ApplyEmbeddedTabDesignMode(TabViewDesignMode mode)
        {
            embeddedTabView.TabDesignMode = mode;
            _embeddedTabView.TabDesignMode   = mode;

            var modeDesc = mode switch
            {
                TabViewDesignMode.RoundedCompact => "RoundedCompact - muted full-width container, elevated card on selected tab.",
                TabViewDesignMode.Rectangle      => "Rectangle - no container, subtle ghost hover, full-width primary indicator.",
                TabViewDesignMode.Rounded        => "Rounded - muted segmented container, Surface card on selected.",
                TabViewDesignMode.Pill           => "Pill - filled Primary pill on selected, no container background.",
                TabViewDesignMode.Outlined       => "Outlined - classic 3-sided border tab, open bottom merges with content.",
                TabViewDesignMode.Minimal        => "Minimal - minimal surface, Primary left-accent bar and tint on selected.",
                TabViewDesignMode.Fluent         => "Fluent - soft acrylic tint, reveal highlight, rounded selected surface.",
                TabViewDesignMode.MacOS          => "MacOS - compact inset capsule, subtle outline, sidebar-friendly spacing.",
                _                                      => "Chromed - browser-style top-rounded tabs, Surface elevated on selected.",
            };
            var alignDesc = embeddedTabView.TabAlignment switch
            {
                TabViewAlignment.Center => "Center",
                TabViewAlignment.End    => "End",
                _                             => "Start",
            };
            var layoutDesc = embeddedTabView.TabLayoutMode switch
            {
                TabViewLayoutMode.Left => "Left",
                TabViewLayoutMode.Right => "Right",
                TabViewLayoutMode.Bottom => "Bottom",
                _ => "Top",
            };
            embeddedModeStatus.Text = embeddedTabView.CustomTabStyle.HasValue
                ? $"Mode: Custom - builder-defined colors, spacing, shape and indicator.\nBase preset: {mode} - Alignment: {alignDesc} - Layout: {layoutDesc}"
                : $"Mode: {modeDesc}\nAlignment: {alignDesc} - Layout: {layoutDesc}";

            SetButtonActive(roundedCompactModeButton, mode == TabViewDesignMode.RoundedCompact);
            SetButtonActive(rectangleModeButton,      mode == TabViewDesignMode.Rectangle);
            SetButtonActive(roundedModeButton,        mode == TabViewDesignMode.Rounded);
            SetButtonActive(chromedModeButton,        mode == TabViewDesignMode.Chromed);
            SetButtonActive(pillModeButton,           mode == TabViewDesignMode.Pill);
            SetButtonActive(outlinedModeButton,       mode == TabViewDesignMode.Outlined);
            SetButtonActive(minimalModeButton,        mode == TabViewDesignMode.Minimal);
            SetButtonActive(fluentModeButton,         mode == TabViewDesignMode.Fluent);
            SetButtonActive(macOSModeButton,          mode == TabViewDesignMode.MacOS);
            SetButtonActive(customStyleButton,        embeddedTabView.CustomTabStyle.HasValue);
            SetButtonActive(clearStyleButton,         embeddedTabView.CustomTabStyle.HasValue);
        }

        void ApplyCustomTabStyle(TabView tabViewControl)
        {
            var isDark = ColorScheme.IsDarkMode;
            var accent = ColorScheme.Primary;
            var selectedSurface = isDark
                ? ColorScheme.SurfaceContainerHigh.WithAlpha(238)
                : SKColors.White.WithAlpha(246);
            var hoverSurface = accent.WithAlpha(isDark ? (byte)24 : (byte)18);

            tabViewControl.ConfigureTabStyle(style => style
                .Header(header => header
                    .Background(ColorScheme.SurfaceContainer.WithAlpha(isDark ? (byte)130 : (byte)172))
                    .Border(ColorScheme.Outline.WithAlpha(isDark ? (byte)44 : (byte)46)))
                .Metrics(metrics => metrics
                    .Padding(horizontal: 18, vertical: 8)
                    .SurfaceInset(new Thickness(4, 4, 4, 4))
                    .Gap(5)
                    .Width(min: 112, max: 252)
                    .Height(min: 38, max: 72))
                .Normal(tab => tab
                    .Background(SKColors.Transparent)
                    .Foreground(ColorScheme.ForeColor.WithAlpha(isDark ? (byte)176 : (byte)160))
                    .Border(SKColors.Transparent, thickness: 0)
                    .Radius(8))
                .Hover(tab => tab
                    .Background(hoverSurface)
                    .Foreground(ColorScheme.ForeColor.WithAlpha(isDark ? (byte)226 : (byte)210))
                    .Border(accent.WithAlpha(isDark ? (byte)42 : (byte)34), thickness: 1)
                    .Radius(8))
                .Selected(tab => tab
                    .Background(selectedSurface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(accent.WithAlpha(isDark ? (byte)82 : (byte)70), thickness: 1)
                    .Radius(9))
                .Indicator(indicator => indicator
                    .Color(accent)
                    .Thickness(2f)),
                clearExisting: true);
        }

        void ApplyEmbeddedCustomTabStyle()
        {
            embeddedTabView.TabDesignMode = TabViewDesignMode.Fluent;
            _embeddedTabView.TabDesignMode   = TabViewDesignMode.Fluent;

            ApplyCustomTabStyle(embeddedTabView);
            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);
        }

        void ClearEmbeddedCustomTabStyle()
        {
            embeddedTabView.ClearCustomTabStyle();
            _embeddedTabView.ClearCustomTabStyle();
            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);
        }

        void ApplyEmbeddedPresetDesignMode(TabViewDesignMode mode)
        {
            embeddedTabView.ClearCustomTabStyle();
            _embeddedTabView.ClearCustomTabStyle();
            ApplyEmbeddedTabDesignMode(mode);
        }

        // Apply alignment
        void ApplyEmbeddedTabAlignment(TabViewAlignment alignment)
        {
            embeddedTabView.TabAlignment = alignment;
            _embeddedTabView.TabAlignment = alignment;
            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);

            SetButtonActive(startAlignButton,  alignment == TabViewAlignment.Start);
            SetButtonActive(centerAlignButton, alignment == TabViewAlignment.Center);
            SetButtonActive(endAlignButton,    alignment == TabViewAlignment.End);
        }

        void ApplyEmbeddedTabLayout(TabViewLayoutMode layoutMode)
        {
            embeddedTabView.TabLayoutMode = layoutMode;

            if (layoutMode == TabViewLayoutMode.Top)
            {
                _embeddedTabView.TabMode = TabViewMode.TitleBar;
                _embeddedTabView.TabLayoutMode = TabViewLayoutMode.Top;
            }
            else
            {
                _embeddedTabView.TabMode = TabViewMode.Embedded;
                _embeddedTabView.TabLayoutMode = layoutMode;
            }

            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);

            SetButtonActive(topLayoutButton,    layoutMode == TabViewLayoutMode.Top);
            SetButtonActive(leftLayoutButton,   layoutMode == TabViewLayoutMode.Left);
            SetButtonActive(rightLayoutButton,  layoutMode == TabViewLayoutMode.Right);
            SetButtonActive(bottomLayoutButton, layoutMode == TabViewLayoutMode.Bottom);
        }

        // Apply text alignment
        void ApplyEmbeddedTextAlign(ContentAlignment align)
        {
            embeddedTabView.TextAlign = align;
            _embeddedTabView.TextAlign = align;

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

        // Apply icon alignment
        void ApplyEmbeddedIconAlign(ContentAlignment align)
        {
            embeddedTabView.ImageAlign = align;
            _embeddedTabView.ImageAlign   = align;

            for (var buttonIndex = 0; buttonIndex < iconAlignButtons.Length; buttonIndex++)
                SetButtonActive(iconAlignButtons[buttonIndex], false);

            var activeButton = align switch
            {
                ContentAlignment.TopLeft     => iconAlignTopLeftButton,
                ContentAlignment.TopCenter   => iconAlignTopCenterButton,
                ContentAlignment.TopRight    => iconAlignTopRightButton,
                ContentAlignment.MiddleLeft  => iconAlignMiddleLeftButton,
                ContentAlignment.MiddleCenter => iconAlignMiddleCenterButton,
                ContentAlignment.MiddleRight  => iconAlignMiddleRightButton,
                ContentAlignment.BottomLeft   => iconAlignBottomLeftButton,
                ContentAlignment.BottomCenter => iconAlignBottomCenterButton,
                ContentAlignment.BottomRight  => iconAlignBottomRightButton,
                _                             => iconAlignMiddleLeftButton,
            };

            SetButtonActive(activeButton, true);
        }

        roundedCompactModeButton.Click += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.RoundedCompact);
        rectangleModeButton.Click      += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Rectangle);
        roundedModeButton.Click        += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Rounded);
        chromedModeButton.Click        += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Chromed);
        pillModeButton.Click           += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Pill);
        outlinedModeButton.Click       += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Outlined);
        minimalModeButton.Click        += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Minimal);
        fluentModeButton.Click         += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.Fluent);
        macOSModeButton.Click          += (_, _) => ApplyEmbeddedPresetDesignMode(TabViewDesignMode.MacOS);
        customStyleButton.Click        += (_, _) => ApplyEmbeddedCustomTabStyle();
        clearStyleButton.Click         += (_, _) => ClearEmbeddedCustomTabStyle();

        startAlignButton.Click  += (_, _) => ApplyEmbeddedTabAlignment(TabViewAlignment.Start);
        centerAlignButton.Click += (_, _) => ApplyEmbeddedTabAlignment(TabViewAlignment.Center);
        endAlignButton.Click    += (_, _) => ApplyEmbeddedTabAlignment(TabViewAlignment.End);

        topLayoutButton.Click    += (_, _) => ApplyEmbeddedTabLayout(TabViewLayoutMode.Top);
        leftLayoutButton.Click   += (_, _) => ApplyEmbeddedTabLayout(TabViewLayoutMode.Left);
        rightLayoutButton.Click  += (_, _) => ApplyEmbeddedTabLayout(TabViewLayoutMode.Right);
        bottomLayoutButton.Click += (_, _) => ApplyEmbeddedTabLayout(TabViewLayoutMode.Bottom);

        textAlignTopLeftButton.Click      += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.TopLeft);
        textAlignTopCenterButton.Click    += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.TopCenter);
        textAlignTopRightButton.Click     += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.TopRight);
        textAlignMiddleLeftButton.Click   += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.MiddleLeft);
        textAlignMiddleCenterButton.Click += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.MiddleCenter);
        textAlignMiddleRightButton.Click  += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.MiddleRight);
        textAlignBottomLeftButton.Click   += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.BottomLeft);
        textAlignBottomCenterButton.Click += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.BottomCenter);
        textAlignBottomRightButton.Click  += (_, _) => ApplyEmbeddedTextAlign(ContentAlignment.BottomRight);

        iconAlignTopLeftButton.Click      += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.TopLeft);
        iconAlignTopCenterButton.Click    += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.TopCenter);
        iconAlignTopRightButton.Click     += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.TopRight);
        iconAlignMiddleLeftButton.Click   += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.MiddleLeft);
        iconAlignMiddleCenterButton.Click += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.MiddleCenter);
        iconAlignMiddleRightButton.Click  += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.MiddleRight);
        iconAlignBottomLeftButton.Click   += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.BottomLeft);
        iconAlignBottomCenterButton.Click += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.BottomCenter);
        iconAlignBottomRightButton.Click  += (_, _) => ApplyEmbeddedIconAlign(ContentAlignment.BottomRight);

        embeddedModeButtons.Controls.Add(macOSModeButton);
        embeddedModeButtons.Controls.Add(fluentModeButton);
        embeddedModeButtons.Controls.Add(minimalModeButton);
        embeddedModeButtons.Controls.Add(outlinedModeButton);
        embeddedModeButtons.Controls.Add(pillModeButton);
        embeddedModeButtons.Controls.Add(chromedModeButton);
        embeddedModeButtons.Controls.Add(roundedModeButton);
        embeddedModeButtons.Controls.Add(rectangleModeButton);
        embeddedModeButtons.Controls.Add(roundedCompactModeButton);

        customStyleButtons.Controls.Add(clearStyleButton);
        customStyleButtons.Controls.Add(customStyleButton);

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

        embeddedIconAlignTopButtons.Controls.Add(iconAlignTopRightButton);
        embeddedIconAlignTopButtons.Controls.Add(iconAlignTopCenterButton);
        embeddedIconAlignTopButtons.Controls.Add(iconAlignTopLeftButton);

        embeddedIconAlignMiddleButtons.Controls.Add(iconAlignMiddleRightButton);
        embeddedIconAlignMiddleButtons.Controls.Add(iconAlignMiddleCenterButton);
        embeddedIconAlignMiddleButtons.Controls.Add(iconAlignMiddleLeftButton);

        embeddedIconAlignBottomButtons.Controls.Add(iconAlignBottomRightButton);
        embeddedIconAlignBottomButtons.Controls.Add(iconAlignBottomCenterButton);
        embeddedIconAlignBottomButtons.Controls.Add(iconAlignBottomLeftButton);

        embeddedIconAlignButtons.Controls.Add(embeddedIconAlignBottomButtons);
        embeddedIconAlignButtons.Controls.Add(embeddedIconAlignMiddleButtons);
        embeddedIconAlignButtons.Controls.Add(embeddedIconAlignTopButtons);

        embeddedToolbar.Controls.Add(embeddedModeStatus);
        embeddedToolbar.Controls.Add(embeddedIconAlignButtons);
        embeddedToolbar.Controls.Add(iconAlignmentLabel);
        embeddedToolbar.Controls.Add(embeddedTextAlignButtons);
        embeddedToolbar.Controls.Add(textAlignmentLabel);
        embeddedToolbar.Controls.Add(embeddedLayoutButtons);
        embeddedToolbar.Controls.Add(layoutLabel);
        embeddedToolbar.Controls.Add(embeddedAlignmentButtons);
        embeddedToolbar.Controls.Add(alignmentLabel);
        embeddedToolbar.Controls.Add(customStyleButtons);
        embeddedToolbar.Controls.Add(customStyleLabel);
        embeddedToolbar.Controls.Add(embeddedModeButtons);
        embeddedToolbar.Controls.Add(designModeLabel);

        // Tab page factory
        var overviewTabIcon    = CreateEmbeddedIcon(new SKColor(0x22, 0xC5, 0x5E), ExampleIconKind.Healthy);
        var workflowTabIcon    = CreateEmbeddedIcon(new SKColor(0xF5, 0x9E, 0x0B), ExampleIconKind.Pulse);
        var compositionTabIcon = CreateEmbeddedIcon(new SKColor(0xA8, 0x55, 0xF7), ExampleIconKind.Locked);
        var settingsTabIcon    = CreateEmbeddedIcon(new SKColor(0xEF, 0x44, 0x44), ExampleIconKind.Warning);

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

        embeddedTabView.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabOverview", "Overview", overviewTabIcon,
            new SKColor(0x22, 0xC5, 0x5E),
            "Tab Strip - Embedded Mode\nThe control owns hit-testing, layout and rendering. No dependency on the outer title bar.",
            "Tab alignment (Start / Center / End) shifts the whole strip. Close and new-tab buttons adjust automatically. All four design modes share the same hit-test and animation pipeline.",
            ("Design Modes", "4"), ("Alignments", "3"), ("Transitions", "11")));

        embeddedTabView.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabWorkflow", "Workflow", workflowTabIcon,
            new SKColor(0xF5, 0x9E, 0x0B),
            "Animated Page Transitions\nEvery tab switch goes through the snapshot-based transition engine.",
            "ScaleFade, Push, Cover, Reveal, Iris and six more effects work identically in Embedded mode. Switch a design mode and the tab strip redraws on the next frame with zero layout recalculation.",
            ("Effects", "11"), ("Easing Curves", "10"), ("Duration Range", "100 - 1000 ms")));

        embeddedTabView.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabComposition", "Composition", compositionTabIcon,
            new SKColor(0xA8, 0x55, 0xF7),
            "Nested Page Controls\nAn embedded strip can live inside any Container, panel or inspector rail.",
            "The outer window here uses TitleBar tabs. This inner control uses Embedded mode - both run inside the same render loop with no coordination overhead.",
            ("Nesting Depth", "Unlimited"), ("DPI Aware", "Yes"), ("ScaleFactor", "1x - 4x")));

        embeddedTabView.Controls.Add(CreateEmbeddedTabPage(
            "embeddedTabSettings", "Settings", settingsTabIcon,
            new SKColor(0xEF, 0x44, 0x44),
            "Runtime Configuration\nDesign mode and alignment update live - no rebuild, no layout pass.",
            "RoundedCompact renders a card-lift effect. Rectangle draws a full-width primary indicator. Rounded builds a segmented control. Chromed uses a top-rounded browser-tab silhouette.",
            ("Live Swap", "Yes"), ("Repaint", "1 Frame"), ("CPU Alloc", "~0 B/frame")));

        // Seed initial state
        ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);
        ApplyEmbeddedTabAlignment(embeddedTabView.TabAlignment);
        ApplyEmbeddedTabLayout(embeddedTabView.TabLayoutMode);
        ApplyEmbeddedTextAlign(embeddedTabView.TextAlign);
        ApplyEmbeddedIconAlign(embeddedTabView.ImageAlign);

        embeddedTabView.NewTabButtonClick += (_, _) =>
            NotificationToast.Show(
                "New Tab",
                "A new tab was requested from the embedded tab strip.",
                NotificationKind.Info);

        embeddedTabView.TabCloseButtonClick += (_, tabIndex) =>
        {
            var page         = embeddedTabView.GetPageAt(tabIndex);
            var tabTitle     = page?.Text ?? $"Tab {tabIndex + 1}";
            NotificationToast.Show(
                "Tab Closed",
                $"\u201c{tabTitle}\u201d tab close was requested.",
                NotificationKind.Warning);
        };

        embeddedTabsPage.Controls.Add(embeddedTabView);
        embeddedTabsPage.Controls.Add(embeddedToolbar);

        Controls.Add(embeddedTabsPage);
        embeddedTabsPage.BringToFront();
        PerformLayout();
        Invalidate();
    
    }
}
