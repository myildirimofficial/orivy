using Orivy;
using Orivy.Animation;
using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class ButtonGroupDemoPage
{
    private void InitializeComponent()
    {
        Text = "Button Groups";
        Name = "buttonGroupDemoPage";
        Padding = new(24);
        Dock = DockStyle.Fill;
        Radius = new(0);
        Border = new(0);
        AutoScroll = true;
        AutoScrollMargin = new(0, 24);

        var pageHeader = new Element
        {
            Name = "bgPageHeader",
            Text = "Button Groups\nButtonGroup<T> works with any type: enum, string, int, float, struct, or class. Items can be loaded via SetItems(), Items.Add(), Items.AddRange(), or auto-populated from enum values.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceVariant,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(12),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // ── String ──────────────────────────────────────────────────────────
        var stringCard = MakeCard("String list - Items.Add(string)");

        var stringGroup = new ButtonGroup<string>
        {
            Name = "stringGroup",
            Dock = DockStyle.Top,
            Height = 44,
            Margin = new(0, 0, 0, 0),
            Gap = 8,
        };
        ConfigureGroupButtons(stringGroup);
        stringGroup.Items.Add("Sans-Serif");
        stringGroup.Items.Add("Serif");
        stringGroup.Items.Add("Monospace");
        stringGroup.Items.Add("Display");
        stringGroup.Items.Add("Handwriting");

        // ── Int ─────────────────────────────────────────────────────────────
        var intCard = MakeCard("Int list - Items.AddRange(int[])");

        var intGroup = new ButtonGroup<int>
        {
            Name = "intGroup",
            Dock = DockStyle.Top,
            Height = 44,
            Gap = 8,
            LabelSelector = v => $"{v} px",
        };
        ConfigureGroupButtons(intGroup);
        intGroup.Items.AddRange(new[] { 8, 10, 12, 14, 16, 18, 24, 32, 48, 64 });

        // ── Float ────────────────────────────────────────────────────────────
        var floatCard = MakeCard("Float list - Items.AddRange(float[])");

        var floatGroup = new ButtonGroup<float>
        {
            Name = "floatGroup",
            Dock = DockStyle.Top,
            Height = 44,
            Gap = 8,
            LabelSelector = v => $"{v:0.##}x",
        };
        ConfigureGroupButtons(floatGroup);
        floatGroup.Items.AddRange(new[] { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f });

        // ── Struct ───────────────────────────────────────────────────────────
        var structCard = MakeCard("Struct list - Items.Add(ScaleFactorOption) - value type with label + float");

        var structGroup = new ButtonGroup<ScaleFactorOption>
        {
            Name = "structGroup",
            Dock = DockStyle.Top,
            Height = 44,
            Gap = 8,
        };
        ConfigureGroupButtons(structGroup);
        structGroup.Items.Add(new ScaleFactorOption("Quarter", 0.25f));
        structGroup.Items.Add(new ScaleFactorOption("Half", 0.50f));
        structGroup.Items.Add(new ScaleFactorOption("Normal", 1.00f));
        structGroup.Items.Add(new ScaleFactorOption("Double", 2.00f));
        structGroup.Items.Add(new ScaleFactorOption("4x", 4.00f));

        // ── Class ────────────────────────────────────────────────────────────
        var classCard = MakeCard("Class list - SetItems() + LabelSelector - reference type");

        var classGroup = new ButtonGroup<FontPreset>
        {
            Name = "classGroup",
            Dock = DockStyle.Top,
            Height = 44,
            Gap = 8,
            LabelSelector = p => $"{p.Family} {p.Size}",
        };
        ConfigureGroupButtons(classGroup);
        classGroup.SetItems(new[]
        {
            new FontPreset { Family = "Inter",     Size = 12 },
            new FontPreset { Family = "Roboto",    Size = 14 },
            new FontPreset { Family = "JetBrains", Size = 13 },
            new FontPreset { Family = "Fira Code", Size = 12 },
        });

        // ── Enum (auto-populated) ────────────────────────────────────────────
        var enumCard = MakeCard("Enum list - auto-populated from Orientation enum values");

        var enumGroup = new ButtonGroup<Orientation>
        {
            Name = "enumGroup",
            Dock = DockStyle.Top,
            Height = 44,
            Gap = 8,
        };
        ConfigureGroupButtons(enumGroup);

        // ── Wire up ──────────────────────────────────────────────────────────
        AddCardContent(stringCard, stringGroup);
        AddCardContent(intCard, intGroup);
        AddCardContent(floatCard, floatGroup);
        AddCardContent(structCard, structGroup);
        AddCardContent(classCard, classGroup);
        AddCardContent(enumCard, enumGroup);

        Controls.Add(pageHeader);
        Controls.Add(stringCard);
        Controls.Add(intCard);
        Controls.Add(floatCard);
        Controls.Add(structCard);
        Controls.Add(classCard);
        Controls.Add(enumCard);
    }

    private static Card MakeCard(string title)
    {
        return new Card
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            Radius = new(14),
            Title = title
        };
    }

    private static void AddCardContent(Card card, ElementBase content)
    {
        content.Margin = new(0);
        card.AddContent(content);
    }

    private static void ConfigureGroupButtons<T>(ButtonGroup<T> group)
    {
        group.ConfigureButton = (btn, _) =>
        {
            btn.Height = 36;
            btn.Margin = new(0);
            btn.Radius = new(8);
            btn.ConfigureVisualStyles(styles => styles
                .DefaultTransition(TimeSpan.FromMilliseconds(110), AnimationType.CubicEaseOut)
                .Base(rule => rule
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline.WithAlpha(100))
                    .Radius(8)
                    .Shadow(BoxShadow.None))
                .OnHover(rule => rule
                    .Background(ColorScheme.SurfaceContainerHigh)
                    .BorderColor(ColorScheme.Primary.WithAlpha(80)))
                .OnChecked(rule => rule
                    .Background(ColorScheme.Primary)
                    .Foreground(SKColors.White)
                    .BorderColor(ColorScheme.Primary)
                    .Shadow(new BoxShadow(0f, 4f, 10f, 0, ColorScheme.Primary.WithAlpha(24))))
                .OnPressed(rule => rule
                    .Background(ColorScheme.Primary.Brightness(-0.08f))
                    .Foreground(SKColors.White)
                    .BorderColor(ColorScheme.Primary.Brightness(-0.12f))
                    .Opacity(0.96f))
                .OnFocused(rule => rule
                    .BorderColor(ColorScheme.Primary.Brightness(0.16f))),
                clearExisting: true);
        };
    }
}
