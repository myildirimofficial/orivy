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
            TransitionMode = SwitchButtonTransitionMode.Stretch,
            TransitionDuration = TimeSpan.FromMilliseconds(180)
        };
        var quietSwitch = new SwitchButton
        {
            Name = "modernQuietSwitch",
            Text = "Quiet mode",
            Location = new SKPoint(164, 4),
            Size = new SKSize(136, 30),
            OnColor = new SKColor(0x0D, 0x94, 0xA8),
            TransitionMode = SwitchButtonTransitionMode.Slide,
            TransitionDuration = TimeSpan.FromMilliseconds(160),
            ToggleArea = SwitchButtonToggleArea.SwitchOnly
        };
        var bounceSwitch = new SwitchButton
        {
            Name = "modernBounceSwitch",
            Text = "Bounce",
            Location = new SKPoint(316, 4),
            Size = new SKSize(116, 30),
            OnColor = new SKColor(0x22, 0xC5, 0x5E),
            TransitionMode = SwitchButtonTransitionMode.Bounce,
            TransitionDuration = TimeSpan.FromMilliseconds(190)
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

        var inputCard = CreateCard("TrackBar and NumericUpDown", "NumericUpDown supports prefix/suffix formatting, wrapping, button visibility and animated value changes.");
        progressTrack = new TrackBar
        {
            Name = "modernProgressTrack",
            Dock = DockStyle.Top,
            Height = 48,
            Margin = new(0, 0, 0, 12),
            Value = 62,
            ShowValue = true
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
            AnimationMode = NumericUpDownAnimationMode.Slide
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
            ButtonVisibility = NumericUpDownButtonVisibility.HoverOrFocused
        };
        var advanceButton = new Button
        {
            Name = "modernAdvanceProgressButton",
            Text = "Advance",
            Location = new SKPoint(292, 0),
            Size = new SKSize(108, 38)
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
            ButtonVisibility = NumericUpDownButtonVisibility.HoverOrFocused
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
        progressTrack.ValueChanged += (_, _) => SetProgress(progressTrack.Value);
        progressNumeric.ValueChanged += (_, _) => SetProgress((float)progressNumeric.Value);
        advanceButton.Click += (_, _) => AdvanceProgress(17f);

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
            ShowDropDownArrow = true
        };
        AddCardContent(dropdownCard, dropdownButton);

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

        Controls.Add(dropdownCard);
        Controls.Add(separatorCard);
        Controls.Add(inputCard);
        Controls.Add(progressCard);
        Controls.Add(selectionCard);
        Controls.Add(header);
    }

    private static Element CreateCard(string title, string description)
    {
        var card = new Element
        {
            Text = string.Empty,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(12),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(90),
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = "theme-card"
        };

        var body = new Element
        {
            Text = $"{title}\n{description}",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(0, 0, 0, 14),
            Margin = new(0),
            BackColor = SKColors.Transparent,
            ForeColor = ColorScheme.ForeColor,
            Border = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = "theme-card-header"
        };
        card.Controls.Add(body);
        return card;
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

    private static void AddCardContent(Element card, ElementBase content)
    {
        var header = card.Controls.Count > 0 ? card.Controls[0] : null;
        if (header != null)
            card.Controls.Remove(header);

        card.Controls.Add(content);

        if (header != null)
            card.Controls.Add(header);
    }
}
