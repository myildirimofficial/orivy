using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class ModernControlsDemoPage
{
    private void InitializeComponent()
    {
        Text = "Modern Controls";
        Name = "modernControlsDemoPage";
        Padding = new(24);
        Dock = DockStyle.Fill;
        Radius = new(0);
        Border = new(0);
        AutoScroll = true;
        AutoScrollMargin = new(0, 24);

        var header = CreateCard(
            "Modern Controls",
            "VisualStyles and MotionEffects driven controls: checkbox, radio, switch, progress, trackbar, numeric input and dropdown buttons.");

        var selectionCard = CreateCard("Selection", "Checkbox and RadioButton animate both the indicator and the surrounding visual state.");
        var switchRow = CreateRow(42);
        var elasticSwitch = new SwitchButton
        {
            Name = "modernElasticSwitch",
            Text = "Elastic switch",
            Location = new SKPoint(0, 4),
            Size = new SKSize(148, 30),
            Checked = true,
            TransitionMode = SwitchButtonTransitionMode.Cupertino,
            TransitionDuration = TimeSpan.FromMilliseconds(260),
            Elasticity = 0.07f,
            PressStretch = 1.18f,
            AnimationFunction = t => 1f - MathF.Pow(1f - t, 3.2f)
        };
        var quietSwitch = new SwitchButton
        {
            Name = "modernQuietSwitch",
            Text = "Quiet mode",
            Location = new SKPoint(164, 4),
            Size = new SKSize(136, 30),
            OnColor = new SKColor(0x0D, 0x94, 0xA8),
            TransitionMode = SwitchButtonTransitionMode.Material,
            TransitionDuration = TimeSpan.FromMilliseconds(160),
            AnimationFunction = t => t * t * (3f - 2f * t),
            ToggleArea = SwitchButtonToggleArea.SwitchOnly
        };
        var bounceSwitch = new SwitchButton
        {
            Name = "modernBounceSwitch",
            Text = "Bounce",
            Location = new SKPoint(316, 4),
            Size = new SKSize(116, 30),
            OnColor = new SKColor(0x22, 0xC5, 0x5E),
            TransitionMode = SwitchButtonTransitionMode.Jelly,
            TransitionDuration = TimeSpan.FromMilliseconds(280),
            Elasticity = 0.09f,
            PressStretch = 1.35f
        };
        switchRow.Controls.Add(bounceSwitch);
        switchRow.Controls.Add(quietSwitch);
        switchRow.Controls.Add(elasticSwitch);

        var checkRow = CreateRow(36);
        checkRow.Controls.Add(CreateCheckBox("Compact spacing", true, 0));
        checkRow.Controls.Add(CreateCheckBox("Three state", false, 170, threeState: true));

        var radioRow = CreateRow(36);
        var slideRadio = CreateRadioButton("Slide", true, 0, "numericMode");
        var fadeRadio = CreateRadioButton("Fade", false, 104, "numericMode");
        var scaleRadio = CreateRadioButton("Scale", false, 208, "numericMode");
        var odometerRadio = CreateRadioButton("Odometer", false, 312, "numericMode");
        var noneRadio = CreateRadioButton("None", false, 436, "numericMode");
        slideRadio.CheckedChanged += (_, _) => progressNumeric.AnimationMode = NumericUpDownAnimationMode.Slide;
        fadeRadio.CheckedChanged += (_, _) => progressNumeric.AnimationMode = NumericUpDownAnimationMode.Fade;
        scaleRadio.CheckedChanged += (_, _) => progressNumeric.AnimationMode = NumericUpDownAnimationMode.Scale;
        odometerRadio.CheckedChanged += (_, _) => progressNumeric.AnimationMode = NumericUpDownAnimationMode.Odometer;
        noneRadio.CheckedChanged += (_, _) => progressNumeric.AnimationMode = NumericUpDownAnimationMode.None;
        radioRow.Controls.Add(noneRadio);
        radioRow.Controls.Add(odometerRadio);
        radioRow.Controls.Add(scaleRadio);
        radioRow.Controls.Add(fadeRadio);
        radioRow.Controls.Add(slideRadio);

        var toggleRow = CreateRow(40);
        motionToggle = new ToggleButton
        {
            Name = "modernMotionToggle",
            Text = "Enable Motion",
            Location = new SKPoint(0, 0),
            Size = new SKSize(156, 34),
            Checked = true
        };
        var secondaryToggle = new ToggleButton
        {
            Name = "modernSecondaryToggle",
            Text = "Compact",
            Location = new SKPoint(168, 0),
            Size = new SKSize(120, 34)
        };
        motionToggle.CheckedChanged += (_, _) => ToggleMotion();
        toggleRow.Controls.Add(secondaryToggle);
        toggleRow.Controls.Add(motionToggle);

        AddCardContent(selectionCard, radioRow);
        AddCardContent(selectionCard, checkRow);
        AddCardContent(selectionCard, switchRow);
        AddCardContent(selectionCard, toggleRow);

        var progressCard = CreateCard("Progress", "Percent text can be unconditional, width-aware, or value/range based.");
        linearProgress = new ProgressBar
        {
            Name = "modernLinearProgress",
            Dock = DockStyle.Top,
            Height = 22,
            Margin = new(0, 0, 0, 14),
            Value = 62,
            TextMode = ProgressBarTextMode.PercentWhenWide
        };
        segmentedProgress = new ProgressBar
        {
            Name = "modernSegmentedProgress",
            ForeColor = new SKColor(0x0D, 0x94, 0xA8),
            Dock = DockStyle.Top,
            Height = 20,
            Margin = new(0, 0, 0, 14),
            Mode = ProgressBarMode.Segmented,
            Value = 46,
            TextMode = ProgressBarTextMode.ValueRange
        };
        gradientProgress = new ProgressBar
        {
            Name = "modernGradientProgress",
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new(0, 0, 0, 14),
            Mode = ProgressBarMode.Gradient,
            Value = 58,
            TextMode = ProgressBarTextMode.PercentWhenWide
        };
        stripedProgress = new ProgressBar
        {
            Name = "modernStripedProgress",
            ForeColor = new SKColor(0x7C, 0x3A, 0xED),
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new(0, 0, 0, 14),
            Mode = ProgressBarMode.Striped,
            Value = 70,
            TextMode = ProgressBarTextMode.PercentWhenWide
        };
        var hatchProgress = new ProgressBar
        {
            Name = "modernHatchProgress",
            ForeColor = new SKColor(0x0E, 0xA5, 0xE9),
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new(0, 0, 0, 14),
            Mode = ProgressBarMode.Gradient,
            UseHatchFill = true,
            HatchStyle = HatchStyle.DiagonalCross,
            Value = 48,
            TextMode = ProgressBarTextMode.PercentWhenWide
        };
        dotsProgress = new ProgressBar
        {
            Name = "modernDotsProgress",
            ForeColor = new SKColor(0x10, 0xB9, 0x81),
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new(0, 0, 0, 14),
            Mode = ProgressBarMode.Dots,
            Value = 52
        };
        blocksProgress = new ProgressBar
        {
            Name = "modernBlocksProgress",
            ForeColor = new SKColor(0xF5, 0x9E, 0x0B),
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new(0, 0, 0, 14),
            Mode = ProgressBarMode.Blocks,
            Value = 64
        };
        var circularRow = CreateRow(108);
        circularProgress = new ProgressBar
        {
            Name = "modernCircularProgress",
            Mode = ProgressBarMode.Circular,
            Size = new SKSize(84, 84),
            Location = new SKPoint(0, 10),
            Value = 72,
            TextMode = ProgressBarTextMode.Percent
        };
        ringProgress = new ProgressBar
        {
            Name = "modernRingProgress",
            Mode = ProgressBarMode.Ring,
            Size = new SKSize(84, 84),
            Location = new SKPoint(104, 10),
            Value = 34,
            TextMode = ProgressBarTextMode.Value
        };
        var indeterminateProgress = new ProgressBar
        {
            Name = "modernIndeterminateProgress",
            Dock = DockStyle.Top,
            Height = 12,
            Margin = new(0, 0, 0, 16),
            Mode = ProgressBarMode.Indeterminate
        };
        circularRow.Controls.Add(ringProgress);
        circularRow.Controls.Add(circularProgress);

        AddCardContent(progressCard, circularRow);
        AddCardContent(progressCard, indeterminateProgress);
        AddCardContent(progressCard, blocksProgress);
        AddCardContent(progressCard, dotsProgress);
        AddCardContent(progressCard, stripedProgress);
        AddCardContent(progressCard, hatchProgress);
        AddCardContent(progressCard, gradientProgress);
        AddCardContent(progressCard, segmentedProgress);
        AddCardContent(progressCard, linearProgress);

        var inputCard = CreateCard("Input controls", "NumericUpDown, DatePicker and TimePicker share compact field styling and controlled popup APIs.");
        progressTrack = new TrackBar
        {
            Name = "modernProgressTrack",
            Dock = DockStyle.Top,
            Height = 48,
            Margin = new(0, 0, 0, 12),
            Value = 62,
            ShowValue = true,
            ToolTipText = "Drag or use the mouse wheel to update the progress value."
        };
        progressNumeric = new NumericUpDown
        {
            Name = "modernProgressNumeric",
            Location = new SKPoint(0, 0),
            Size = new SKSize(126, 38),
            Minimum = 0,
            Maximum = 100,
            Value = 62,
            Suffix = "%",
            MouseWheelEnabled = true,
            AnimationMode = NumericUpDownAnimationMode.Slide,
            ToolTipText = "Percent value. Up/down buttons, keyboard arrows and mouse wheel are supported."
        };
        var currencyNumeric = new NumericUpDown
        {
            Name = "modernCurrencyNumeric",
            Location = new SKPoint(140, 0),
            Size = new SKSize(156, 38),
            Minimum = 0,
            Maximum = 250000,
            Increment = 250m,
            Value = 12500m,
            Prefix = "₺",
            DecimalPlaces = 2,
            ThousandsSeparator = true,
            AnimationMode = NumericUpDownAnimationMode.Odometer
        };
        var pixelsNumeric = new NumericUpDown
        {
            Name = "modernPixelsNumeric",
            Location = new SKPoint(310, 0),
            Size = new SKSize(132, 38),
            Minimum = 0,
            Maximum = 240,
            Increment = 4m,
            Value = 32m,
            Suffix = " px",
            MouseWheelEnabled = true,
            ButtonVisibility = NumericUpDownButtonVisibility.HoverOrFocused,
            ToolTipText = "Stepper buttons appear when the whole NumericUpDown is hovered or focused."
        };
        var advanceButton = new Button
        {
            Name = "modernAdvanceProgressButton",
            Text = "Advance",
            Location = new SKPoint(292, 0),
            Size = new SKSize(108, 38),
            ToolTipText = "Adds 17 percent to the progress examples."
        };
        var decimalNumeric = new NumericUpDown
        {
            Name = "modernDecimalNumeric",
            Location = new SKPoint(0, 0),
            Size = new SKSize(138, 38),
            Minimum = -10,
            Maximum = 10,
            Increment = 0.25m,
            Value = 2.5m,
            DecimalPlaces = 2,
            MouseWheelEnabled = true,
            AnimationMode = NumericUpDownAnimationMode.Slide
        };
        var wrapNumeric = new NumericUpDown
        {
            Name = "modernWrapNumeric",
            Location = new SKPoint(152, 0),
            Size = new SKSize(126, 38),
            Minimum = 1,
            Maximum = 5,
            Value = 5,
            WrapValue = true,
            RepeatButtonEnabled = true,
            RepeatAcceleration = true
        };
        var editableNumeric = new NumericUpDown
        {
            Name = "modernEditableNumeric",
            Location = new SKPoint(0, 0),
            Size = new SKSize(150, 38),
            Minimum = -1000,
            Maximum = 1000,
            Increment = 5,
            Value = 128,
            TextBoxMode = true,
            ButtonVisibility = NumericUpDownButtonVisibility.HoverOrFocused,
            ToolTipText = "TextBoxMode uses the shared TextBox control for direct numeric input."
        };
        var inputRow = CreateRow(48);
        inputRow.Controls.Add(pixelsNumeric);
        inputRow.Controls.Add(currencyNumeric);
        inputRow.Controls.Add(progressNumeric);
        var advancedInputRow = CreateRow(48);
        advancedInputRow.Controls.Add(advanceButton);
        advancedInputRow.Controls.Add(wrapNumeric);
        advancedInputRow.Controls.Add(decimalNumeric);
        var editInputRow = CreateRow(48);
        editInputRow.Controls.Add(editableNumeric);
        var pickerRow = CreateRow(48);
        pickerRow.Controls.Add(new DatePicker
        {
            Name = "modernDatePicker",
            Location = new SKPoint(0, 0),
            Size = new SKSize(250, 38),
            Value = DateTime.Today.AddDays(3).AddHours(9).AddMinutes(30),
            ShowTimePicker = true,
            DateTimeFormat = "MMM d, yyyy HH:mm",
            TextBoxMode = true,
            ToolTipText = "DatePicker can show or hide the time picker and supports direct TextBox input."
        });
        pickerRow.Controls.Add(new TimePicker
        {
            Name = "modernTimePicker",
            Location = new SKPoint(264, 0),
            Size = new SKSize(150, 38),
            Value = new TimeSpan(14, 30, 0),
            MinuteStep = 5,
            TextBoxMode = true,
            ToolTipText = "TimePicker selects hours one by one, minutes by MinuteStep, and supports TextBox input."
        });
        progressTrack.ValueChanged += (_, _) => SetProgress(progressTrack.Value);
        progressNumeric.ValueChanged += (_, _) => SetProgress((float)progressNumeric.Value);
        advanceButton.Click += (_, _) => AdvanceProgress(17f);

        AddCardContent(inputCard, pickerRow);
        AddCardContent(inputCard, editInputRow);
        AddCardContent(inputCard, advancedInputRow);
        AddCardContent(inputCard, inputRow);
        AddCardContent(inputCard, progressTrack);

        var dropdownCard = CreateCard("Dropdown button", "DropDownMenu draws an animated chevron and opens a ContextMenuStrip.");
        var dropdownMenu = new ContextMenuStrip
        {
            UseAccordionSubmenus = true,
            ShowShortcutKeys = true
        };
        dropdownMenu.AddMenuItem("Refresh", (_, _) => AdvanceProgress(9f), Keys.Control | Keys.R);
        dropdownMenu.AddMenuItem("Set 25%", (_, _) => SetProgress(25));
        dropdownMenu.AddMenuItem("Set 75%", (_, _) => SetProgress(75));
        dropdownMenu.AddSeparator();
        MenuItem toggleMotionMenuItem = null!;
        toggleMotionMenuItem = dropdownMenu.AddMenuItem("Toggle Motion", (_, _) =>
        {
            motionToggle.Checked = toggleMotionMenuItem.Checked;
        });
        toggleMotionMenuItem.CheckOnClick = true;
        toggleMotionMenuItem.Checked = motionToggle.Checked;
        motionToggle.CheckedChanged += (_, _) =>
        {
            if (toggleMotionMenuItem.Checked != motionToggle.Checked)
                toggleMotionMenuItem.Checked = motionToggle.Checked;
        };
        var dropdownButton = new Button
        {
            Name = "modernDropdownButton",
            Text = "Progress Actions",
            Dock = DockStyle.Top,
            Size = new SKSize(172, 38),
            DropDownMenu = dropdownMenu,
            ShowDropDownArrow = true,
            SplitDropDownButton = true,
            ToolTipText = "Main click runs the button action; the chevron opens the context menu."
        };
        dropdownButton.Click += (_, _) => AdvanceProgress(5f);
        AddCardContent(dropdownCard, dropdownButton);

        var disclosureCard = CreateCard("Navigation and disclosure", "Breadcrumb, subtle badges and animated disclosure controls.");
        var breadcrumb = new Breadcrumb
        {
            Dock = DockStyle.Top,
            Height = 36,
            Margin = new(0, 0, 0, 12)
        };
        breadcrumb.SetItems("Settings", "System", "Display", "Advanced");

        var badgeRow = CreateRow(42);
        badgeRow.Controls.Add(new Badge
        {
            Text = "Preview",
            Variant = BadgeVariant.Primary,
            Location = new SKPoint(0, 8),
            Size = new SKSize(72, 24)
        });
        badgeRow.Controls.Add(new Badge
        {
            Text = "Stable",
            Variant = BadgeVariant.Success,
            Location = new SKPoint(86, 8),
            Size = new SKSize(68, 24)
        });
        badgeRow.Controls.Add(new Button
        {
            Text = "Inbox",
            BadgeText = "12",
            Location = new SKPoint(170, 2),
            Size = new SKSize(116, 36),
            BadgeAlign = ContentAlignment.MiddleRight,
            BadgeBackColor = new SKColor(239, 68, 68),
            BadgeForeColor = SKColors.White,
            Shadow = BoxShadow.None,
            Controls = {
                new Badge
                {
                    Text = "24",
                    Variant = BadgeVariant.Danger,
                    Size = new SKSize(24, 24),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    //Location = new SKPoint(100, -4)
                }
            }
        });

        var accordion = new Accordion
        {
            Dock = DockStyle.Top,
            Height = 190,
            Gap = 0,
            AllowMultipleExpanded = false
        };
        accordion.Controls.Add(CreateCollapseItem("Advanced display", "Brightness, HDR and scale settings live in a compact animated section.", true));
        accordion.Controls.Add(CreateCollapseItem("Optional features", "Collapse keeps content hidden until the user asks for detail.", false));

        AddCardContent(disclosureCard, accordion);
        AddCardContent(disclosureCard, badgeRow);
        AddCardContent(disclosureCard, breadcrumb);

        var tooltipCard = CreateCard("ToolTip", "Every ElementBase derived control can expose the same lightweight tooltip overlay.");
        var tooltipRow = CreateRow(52);
        var fastTipButton = new Button
        {
            Name = "modernFastToolTipButton",
            Text = "Fast tip",
            Location = new SKPoint(0, 4),
            Size = new SKSize(118, 38),
            ToolTipInitialDelay = 120,
            ToolTipPlacement = Position.Top,
            ToolTipText = "This tooltip opens above the button with a shorter delay."
        };
        var longTipTarget = new Element
        {
            Name = "modernLongToolTipTarget",
            Text = "Styled target",
            Location = new SKPoint(132, 4),
            Size = new SKSize(188, 38),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(10),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(90),
            TextAlign = ContentAlignment.MiddleCenter,
            ToolTipPlacement = Position.Bottom,
            ToolTipText = "This tooltip uses custom colors, padding and width. It is still rendered as a top-level overlay."
        };
        longTipTarget.ConfigureToolTip(
            background: new SKColor(15, 23, 42),
            foreground: SKColors.White,
            border: new SKColor(56, 189, 248, 90),
            shadow: new SKColor(2, 6, 23, 120),
            padding: new Thickness(14, 9, 14, 9),
            radius: 8,
            maxWidth: 260,
            fontSize: 13,
            fontEmbolden: true);
        var delayedTipButton = new Button
        {
            Name = "modernDelayedToolTipButton",
            Text = "Right tip",
            Location = new SKPoint(334, 4),
            Size = new SKSize(128, 38),
        };
        delayedTipButton
            .SetToolTip("This one waits longer and prefers the right side.", Position.Right, 1000)
            .ConfigureToolTip(offset: 14, radius: 7);
        tooltipRow.Controls.Add(delayedTipButton);
        tooltipRow.Controls.Add(longTipTarget);
        tooltipRow.Controls.Add(fastTipButton);
        var placementRow = CreateRow(52);
        var autoTipButton = new Button
        {
            Name = "modernAutoToolTipButton",
            Text = "Auto",
            Location = new SKPoint(0, 4),
            Size = new SKSize(86, 38),
            ToolTipText = "Auto chooses the first placement that fits in the window.",
            ToolTipPlacement = Position.Auto
        };
        var topTipButton = new Button
        {
            Name = "modernTopToolTipButton",
            Text = "Top",
            Location = new SKPoint(98, 4),
            Size = new SKSize(86, 38),
            ToolTipText = "Top placement",
            ToolTipPlacement = Position.Top
        };
        var bottomTipButton = new Button
        {
            Name = "modernBottomToolTipButton",
            Text = "Bottom",
            Location = new SKPoint(196, 4),
            Size = new SKSize(96, 38),
            ToolTipText = "Bottom placement",
            ToolTipPlacement = Position.Bottom
        };
        var leftTipButton = new Button
        {
            Name = "modernLeftToolTipButton",
            Text = "Left",
            Location = new SKPoint(304, 4),
            Size = new SKSize(86, 38),
            ToolTipText = "Left placement",
            ToolTipPlacement = Position.Left
        };
        var rightTipButton = new Button
        {
            Name = "modernRightToolTipButton",
            Text = "Right",
            Location = new SKPoint(402, 4),
            Size = new SKSize(92, 38),
            ToolTipText = "Right placement",
            ToolTipPlacement = Position.Right
        };
        placementRow.Controls.Add(rightTipButton);
        placementRow.Controls.Add(leftTipButton);
        placementRow.Controls.Add(bottomTipButton);
        placementRow.Controls.Add(topTipButton);
        placementRow.Controls.Add(autoTipButton);
        var customToolTipRow = CreateRow(52);
        var customRenderTip = new Element
        {
            Name = "modernCustomRenderToolTipTarget",
            Text = "Custom rendered tooltip",
            Location = new SKPoint(0, 4),
            Size = new SKSize(220, 38),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(10),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88),
            TextAlign = ContentAlignment.MiddleCenter,
            ToolTipPlacement = Position.Top,
            ToolTipText = "Custom render\nGradient surface + dynamic text"
        };
        customRenderTip
            .ConfigureToolTip(maxWidth: 260, padding: new Thickness(16, 11, 16, 11), radius: 12, offset: 16)
            .RenderToolTipWith((_, args) =>
            {
                args.Handled = true;
                using var path = new SKPath();
                path.AddRoundRect(args.Bounds, 12, 12);
                using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
                fill.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(args.Bounds.Left, args.Bounds.Top),
                    new SKPoint(args.Bounds.Right, args.Bounds.Bottom),
                    new[] { new SKColor(15, 23, 42, (byte)(245 * args.Progress)), new SKColor(30, 64, 175, (byte)(245 * args.Progress)) },
                    null,
                    SKShaderTileMode.Clamp);
                using var border = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    Color = new SKColor(125, 211, 252, (byte)(110 * args.Progress))
                };
                using var text = new SKPaint { IsAntialias = true, Color = SKColors.White.WithAlpha((byte)(255 * args.Progress)) };
                args.Canvas.DrawPath(path, fill);
                args.Canvas.DrawPath(path, border);
                var metrics = args.Font.Metrics;
                var y = args.TextBounds.Top - metrics.Ascent;
                for (var i = 0; i < args.Lines.Count; i++)
                    args.Canvas.DrawText(args.Lines[i], args.TextBounds.Left, y + i * (args.Font.Size + 5), SKTextAlign.Left, args.Font, text);
            });
        customToolTipRow.Controls.Add(customRenderTip);
        AddCardContent(tooltipCard, customToolTipRow);
        AddCardContent(tooltipCard, placementRow);
        AddCardContent(tooltipCard, tooltipRow);

        var separatorCard = CreateCard("Separator", "Horizontal and vertical separators use the same lightweight drawing control.");
        var horizontalSeparator = new Separator
        {
            Name = "modernHorizontalSeparator",
            Dock = DockStyle.Top,
            Height = 18,
            Margin = new(0, 4, 0, 12),
            LineThickness = 1.5f
        };
        var verticalHost = new Element
        {
            Name = "modernVerticalSeparatorHost",
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = ColorScheme.SurfaceContainer,
            Radius = new(10),
            Padding = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80)
        };
        var verticalSeparator = new Separator
        {
            Name = "modernVerticalSeparator",
            Orientation = Orientation.Vertical,
            Dock = DockStyle.Left,
            Width = 18,
            Margin = new(0, 0, 12, 0),
            LineThickness = 1.5f
        };
        var verticalLabel = new Element
        {
            Text = "Left divider with content area",
            Dock = DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = ColorScheme.ForeColor
        };
        verticalHost.Controls.Add(verticalLabel);
        verticalHost.Controls.Add(verticalSeparator);
        AddCardContent(separatorCard, verticalHost);
        AddCardContent(separatorCard, horizontalSeparator);

        Controls.Add(tooltipCard);
        Controls.Add(dropdownCard);
        Controls.Add(disclosureCard);
        Controls.Add(separatorCard);
        Controls.Add(inputCard);
        Controls.Add(progressCard);
        Controls.Add(selectionCard);
        Controls.Add(header);
    }

    private static Card CreateCard(string title, string description)
    {
        return new Card
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            Title = title,
            Description = description
        };
    }

    private static Element CreateRow(int height)
    {
        return new Element
        {
            Dock = DockStyle.Top,
            Height = height,
            Margin = new(0, 0, 0, 10),
            BackColor = SKColors.Transparent,
            Border = new(0)
        };
    }

    private static CheckBox CreateCheckBox(string text, bool isChecked, int x, bool threeState = false)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            ThreeState = threeState,
            Location = new SKPoint(x, 2),
            Size = new SKSize(160, 30)
        };
    }

    private static RadioButton CreateRadioButton(string text, bool isChecked, int x, string groupName)
    {
        return new RadioButton
        {
            Text = text,
            Checked = isChecked,
            GroupName = groupName,
            Location = new SKPoint(x, 2),
            Size = new SKSize(96, 30)
        };
    }

    private static void AddCardContent(Card card, ElementBase content)
    {
        card.AddContent(content);
    }

    private static Collapse CreateCollapseItem(string title, string body, bool expanded)
    {
        var collapse = new Collapse
        {
            HeaderText = title,
            IsExpanded = expanded,
            Height = expanded ? 92 : 54,
            Margin = new(0, 0, 0, 8)
        };
        collapse.Controls.Add(new Element
        {
            Text = body,
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = SKColors.Transparent,
            Border = new(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(180),
            TextAlign = ContentAlignment.MiddleLeft,
            WrapMode = TextWrap.WordWrap
        });
        return collapse;
    }
}
