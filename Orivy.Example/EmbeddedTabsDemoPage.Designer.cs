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
    private enum EmbeddedTabCustomStyleAction
    {
        Custom,
        Clear
    }

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
            MinimumSize = new SKSize(0, 300)
        };
        _embeddedTabView = embeddedTabView;

        // Toolbar shell
        var embeddedToolbar = new Container
        {
            Name       = "embeddedTabToolbar",
            Dock       = DockStyle.Top,
            AutoSize = true,
            Margin     = new Thickness(0, 0, 0, 16),
            Padding    = new Thickness(16),
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
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Row 1: design mode buttons
        var embeddedModeButtons = new ButtonGroup<TabViewDesignMode>
        {
            Name        = "embeddedModeButtons",
            Dock        = DockStyle.Top,
            Margin      = new Thickness(0, 0, 0, 12),
            Alignment   = ContentAlignment.MiddleCenter,
            Orientation = Orientation.Horizontal,
            Gap         = 0,
        };

        var customStyleLabel = new Element
        {
            Text      = "Custom Style",
            Dock      = DockStyle.Top,
            Margin    = new Thickness(0, 0, 0, 6),
            BackColor = SKColors.Transparent,
            Border    = new Thickness(0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var customStyleButtons = new ButtonGroup<EmbeddedTabCustomStyleAction>
        {
            Name                = "customStyleButtons",
            Dock                = DockStyle.Top,
            Margin              = new Thickness(0, 0, 0, 12),
            Alignment           = ContentAlignment.MiddleLeft,
            AllowEmptySelection = true,
            Gap = 0
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
        };

        // Row 2: alignment buttons
        var embeddedAlignmentButtons = new ButtonGroup<TabViewAlignment>
        {
            Name            = "embeddedAlignmentButtons",
            Dock            = DockStyle.Top,
            Margin          = new Thickness(0, 0, 0, 10),
            Alignment       = ContentAlignment.MiddleLeft,
            BackColor       = SKColors.Transparent,
            Border          = new Thickness(0),
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
        };

        var embeddedLayoutButtons = new ButtonGroup<TabViewLayoutMode>
        {
            Name            = "embeddedLayoutButtons",
            Dock            = DockStyle.Top,
            Margin          = new Thickness(0, 0, 0, 10),
            Alignment       = ContentAlignment.MiddleLeft,
            BackColor       = SKColors.Transparent,
            Border          = new Thickness(0)
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
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Row 4: text alignment buttons
        var embeddedTextAlignButtons = new ButtonGroup<ContentAlignment>
        {
            Name            = "embeddedTextAlignButtons",
            Margin          = new Thickness(0, 0, 0, 10),
            Dock            = DockStyle.Top,
            Gap             = 0,
            Border          = new Thickness(0),
            ConfigureButton = (btn, val) =>
            {
                btn.Text   = SplitPascalCase(val.ToString());
            }
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
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Row 5: icon alignment buttons
        var embeddedIconAlignButtons = new ButtonGroup<ContentAlignment>
        {
            Name            = "embeddedIconAlignButtons",
            Dock            = DockStyle.Top,
            Margin          = new Thickness(0, 0, 0, 10),
            Alignment       = ContentAlignment.MiddleLeft,
            BackColor       = SKColors.Transparent,
            Border          = new Thickness(0),
            Orientation     = Orientation.Horizontal,
            ConfigureButton = (btn, val) =>
            {
                btn.Text   = SplitPascalCase(val.ToString());
            }
        };

        static string SplitPascalCase(string s)
        {
            var sb = new StringBuilder(s.Length + 4);
            for (var i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]))
                    sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        embeddedModeButtons.SetItems(new[]
        {
            TabViewDesignMode.RoundedCompact, TabViewDesignMode.Rectangle, TabViewDesignMode.Rounded,
            TabViewDesignMode.Chromed, TabViewDesignMode.Pill, TabViewDesignMode.Outlined,
            TabViewDesignMode.Minimal, TabViewDesignMode.Fluent, TabViewDesignMode.MacOS
        });

        void ApplyToLinkedTabView(Action<TabView> apply)
        {
            if (_linkedTabView != null && !ReferenceEquals(_linkedTabView, embeddedTabView))
                apply(_linkedTabView);
        }

        void SyncCustomStyleButtons()
        {
            if (embeddedTabView.CustomTabStyle.HasValue)
                customStyleButtons.SetSelectedValue(EmbeddedTabCustomStyleAction.Custom, raiseChanged: false);
            else
                customStyleButtons.ClearSelection(raiseChanged: false);
        }

        // Apply design mode
        void ApplyEmbeddedTabDesignMode(TabViewDesignMode mode)
        {
            embeddedTabView.TabDesignMode = mode;
            ApplyToLinkedTabView(tabViewControl => tabViewControl.TabDesignMode = mode);

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

            embeddedModeButtons.SetSelectedValue(mode, raiseChanged: false);
            SyncCustomStyleButtons();
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
            ApplyToLinkedTabView(tabViewControl => tabViewControl.TabDesignMode = TabViewDesignMode.Fluent);

            ApplyCustomTabStyle(embeddedTabView);
            ApplyToLinkedTabView(ApplyCustomTabStyle);
            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);
        }

        void ClearEmbeddedCustomTabStyle()
        {
            embeddedTabView.ClearCustomTabStyle();
            ApplyToLinkedTabView(tabViewControl => tabViewControl.ClearCustomTabStyle());
            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);
        }

        void ApplyEmbeddedPresetDesignMode(TabViewDesignMode mode)
        {
            embeddedTabView.ClearCustomTabStyle();
            ApplyToLinkedTabView(tabViewControl => tabViewControl.ClearCustomTabStyle());
            ApplyEmbeddedTabDesignMode(mode);
        }

        // Apply alignment
        void ApplyEmbeddedTabAlignment(TabViewAlignment alignment)
        {
            embeddedTabView.TabAlignment = alignment;
            ApplyToLinkedTabView(tabViewControl => tabViewControl.TabAlignment = alignment);
            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);

            embeddedAlignmentButtons.SetSelectedValue(alignment, raiseChanged: false);
        }

        void ApplyEmbeddedTabLayout(TabViewLayoutMode layoutMode)
        {
            embeddedTabView.TabLayoutMode = layoutMode;

            if (layoutMode == TabViewLayoutMode.Top)
            {
                ApplyToLinkedTabView(tabViewControl =>
                {
                    tabViewControl.TabMode = TabViewMode.TitleBar;
                    tabViewControl.TabLayoutMode = TabViewLayoutMode.Top;
                });
            }
            else
            {
                ApplyToLinkedTabView(tabViewControl =>
                {
                    tabViewControl.TabMode = TabViewMode.Embedded;
                    tabViewControl.TabLayoutMode = layoutMode;
                });
            }

            ApplyEmbeddedTabDesignMode(embeddedTabView.TabDesignMode);

            embeddedLayoutButtons.SetSelectedValue(layoutMode, raiseChanged: false);
        }

        // Apply text alignment
        void ApplyEmbeddedTextAlign(ContentAlignment align)
        {
            embeddedTabView.TextAlign = align;
            ApplyToLinkedTabView(tabViewControl => tabViewControl.TextAlign = align);

            embeddedTextAlignButtons.SetSelectedValue(align, raiseChanged: false);
        }

        // Apply icon alignment
        void ApplyEmbeddedIconAlign(ContentAlignment align)
        {
            embeddedTabView.ImageAlign = align;
            ApplyToLinkedTabView(tabViewControl => tabViewControl.ImageAlign = align);

            embeddedIconAlignButtons.SetSelectedValue(align, raiseChanged: false);
        }

        embeddedModeButtons.SelectedValueChanged += (_, e) => ApplyEmbeddedPresetDesignMode(e.SelectedValue);
        embeddedAlignmentButtons.SelectedValueChanged += (_, e) => ApplyEmbeddedTabAlignment(e.SelectedValue);
        embeddedLayoutButtons.SelectedValueChanged += (_, e) => ApplyEmbeddedTabLayout(e.SelectedValue);
        embeddedTextAlignButtons.SelectedValueChanged += (_, e) => ApplyEmbeddedTextAlign(e.SelectedValue);
        embeddedIconAlignButtons.SelectedValueChanged += (_, e) => ApplyEmbeddedIconAlign(e.SelectedValue);
        customStyleButtons.SelectedValueChanged += (_, e) =>
        {
            if (e.SelectedValue == EmbeddedTabCustomStyleAction.Custom)
                ApplyEmbeddedCustomTabStyle();
            else
                ClearEmbeddedCustomTabStyle();
        };

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
