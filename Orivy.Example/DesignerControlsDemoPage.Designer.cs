using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Linq;

namespace Orivy.Example;

internal sealed partial class DesignerControlsDemoPage
{
    private void InitializeComponent()
    {
        Text = "Designer";
        Name = "panel3";
        Padding = new(24);
        Dock = Orivy.DockStyle.Fill;
        Radius = new(0);
        Border = new(0);
        AutoScroll = true;
        AutoScrollMargin = new(0, 24);

        var designerControlHeader = new Element
        {
            Name = "designerControlHeader",
            Text = "Designer Controls\nComboBox, ColorPicker, multiline notes and a single-line profile form live together on this page. Use the motion preset, edit the notes field and then move into the profile inputs to compare behaviors.",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceVariant,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(8),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var designerInspectorSection = new Element
        {
            Name = "designerInspectorSection",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerInspectorHeader = new Element
        {
            Name = "designerInspectorHeader",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Padding = new(14),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainerHigh,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(96),
            Text = "Inspector Surface\nSingle-select, multi-select, popup motion, multiline editing, single-line profile editing and the inline color picker live together here.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerNotesSection = new Element
        {
            Name = "designerNotesSection",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerAccentSection = new Element
        {
            Name = "designerAccentSection",
            Dock = Orivy.DockStyle.Top,
            Height = 436,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerAccentHeader = new Element
        {
            Name = "designerAccentHeader",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Margin = new(0, 0, 0, 14),
            Padding = new(12),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(92),
            Text = "Accent and Theme Seed\nAdjust the global primary seed color and compare the focused outline with the menu toggle for the focus path effect.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerControlStatus = new Element
        {
            Name = "designerControlStatus",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Padding = new(14),
            Margin = new(0, 14, 0, 0),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(86),
            Text = "Designer Status\nDropdown motion, notes content and color picker values will report here.",
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

        var designerCaretModeCombo = new ComboBox
        {
            Name = "designerCaretModeCombo",
            Dock = Orivy.DockStyle.Top,
            Height = 42,
            Margin = new(0, 0, 0, 12),
            PlaceholderText = "Caret mode",
            MaxDropDownItems = 4,
            DropDownItemHeight = 34,
            DropDownOpeningEffect = OpeningEffectType.PopFade,
        };
        designerCaretModeCombo.Items.AddRange(new object[]
        {
            new ComboBoxItem("Bar caret", TextBoxCaretMode.Bar),
            new ComboBoxItem("Block caret", TextBoxCaretMode.Block),
            new ComboBoxItem("Underline caret", TextBoxCaretMode.Underline),
            new ComboBoxItem("Double bar caret", TextBoxCaretMode.DoubleBar),
            new ComboBoxItem("Hollow block caret", TextBoxCaretMode.HollowBlock),
            new ComboBoxItem("Dot caret", TextBoxCaretMode.Dot),
        });
        designerCaretModeCombo.SelectedValue = TextBoxCaretMode.Bar;

        var designerAccentPicker = new ColorPicker
        {
            Name = "designerAccentPicker",
            Dock = Orivy.DockStyle.Fill,
            Margin = new(0),
            ReferenceColor = ColorScheme.Primary,
            SelectedColor = ColorScheme.Primary,
            ShowAlphaChannel = true,
            ShowReferenceSwatch = true
        };

        var designerNotesHeader = new Element
        {
            Name = "designerNotesHeader",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Margin = new(0, 0, 0, 10),
            Padding = new(12),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(92),
            Text = "Interaction Notes\nThis TextBox is multiline, wraps with the viewport and uses ElementBase scrollbars. Use Ctrl+Plus, Ctrl+Minus or Ctrl+Wheel to zoom the text while editing.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerNotesTextBox = new TextBox
        {
            Name = "designerNotesTextBox",
            Dock = Orivy.DockStyle.Top,
            Height = 198,
            Margin = new(0, 0, 0, 14),
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            CaretMode = TextBoxCaretMode.Bar,
            WrapMode = TextWrap.WordWrap,
            PlaceholderText = "Write prototype notes, then use Ctrl+Plus, Ctrl+Minus or Ctrl+Wheel to zoom while the text wraps.",
            Text = @"
                Frame 01 - Hero card enters from the right with a 180ms cubic easing curve while keeping a strict 24px gutter across compact breakpoints.
                Frame 02 - The inspector keeps a persistent event log so reviewers can see focus, hover and selection changes without leaving the canvas.
                Frame 03 - Long export payload: token.layer.background.surface/primary/interactive/pressed/outline/subtle/debug/contrast/high-emphasis/review-pass-2026-05-08.
                Frame 04 - Add more notes here, use Enter for new lines, drag to select text, then keep typing to verify caret visibility and internal scrolling."
        };

        var designerProfileHeader = new Element
        {
            Name = "designerProfileHeader",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Margin = new(0, 0, 0, 10),
            Padding = new(12),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(92),
            Text = "User Profile Update\nThese TextBox controls stay in one-line mode. The password field uses PasswordMode with a custom PasswordChar so the new masking API is visible in the example.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var designerProfileDisplayNameTextBox = new TextBox
        {
            Name = "designerProfileDisplayNameTextBox",
            Dock = Orivy.DockStyle.Top,
            Height = 40,
            Margin = new(0, 0, 0, 10),
            PlaceholderText = "Display name",
            Text = "Mahmut Yildirim",
            CaretMode = TextBoxCaretMode.Bar,
        };

        var designerProfileHandleTextBox = new TextBox
        {
            Name = "designerProfileHandleTextBox",
            Dock = Orivy.DockStyle.Top,
            Height = 40,
            Margin = new(0, 0, 0, 10),
            PlaceholderText = "Public handle",
            Text = "@mahmut.design",
            CaretMode = TextBoxCaretMode.DoubleBar,
        };

        var designerProfileEmailTextBox = new TextBox
        {
            Name = "designerProfileEmailTextBox",
            Dock = Orivy.DockStyle.Top,
            Height = 40,
            Margin = new(0, 0, 0, 10),
            PlaceholderText = "Work email",
            Text = "mahmut.yildirim@orivy.dev",
            CaretMode = TextBoxCaretMode.Bar,
        };

        var designerProfilePasswordTextBox = new TextBox
        {
            Name = "designerProfilePasswordTextBox",
            Dock = Orivy.DockStyle.Top,
            Height = 40,
            Margin = new(0, 0, 0, 14),
            PlaceholderText = "Password",
            Text = "alpha-release-2026",
            PasswordMode = true,
            PasswordChar = '#',
            CaretMode = TextBoxCaretMode.HollowBlock,
        };

        var designerProfileForm = new Element
        {
            Name = "designerProfileForm",
            Dock = Orivy.DockStyle.Top,
            AutoSize = true,
            Padding = new(14),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceVariant,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(104),
        };
        designerProfileForm.Controls.Add(designerProfilePasswordTextBox);
        designerProfileForm.Controls.Add(designerProfileEmailTextBox);
        designerProfileForm.Controls.Add(designerProfileHandleTextBox);
        designerProfileForm.Controls.Add(designerProfileDisplayNameTextBox);
        designerProfileForm.Controls.Add(designerProfileHeader);

        void UpdateDesignerControlStatus()
        {
            var surfaceText = designerSurfaceCombo.SelectedItem is ComboBoxItem surfaceItem ? surfaceItem.Text : designerSurfaceCombo.Text;
            var motionText = designerMotionCombo.SelectedItem is ComboBoxItem motionItem ? motionItem.Text : designerMotionCombo.Text;
            var modulesText = string.IsNullOrWhiteSpace(designerModulesCombo.Text) ? "None" : designerModulesCombo.Text;
            var caretModeText = designerCaretModeCombo.SelectedItem is ComboBoxItem caretItem ? caretItem.Text : designerNotesTextBox.CaretMode.ToString();
            var profileSummary =
                $"Profile: {designerProfileDisplayNameTextBox.Text.Length} name chars, {designerProfileHandleTextBox.Text.Length} handle chars, {designerProfileEmailTextBox.Text.Length} mail chars, password mask {designerProfilePasswordTextBox.PasswordChar} x {designerProfilePasswordTextBox.Text.Length}.";
            designerControlStatus.Text =
                $"Designer Status\nSurface: {surfaceText}. Motion: {motionText}. Modules: {modulesText}. Caret: {caretModeText}. Accent: {designerAccentPicker.HexValue}. Notes: {designerNotesTextBox.Text.Length} chars across {designerNotesTextBox.Lines.Length} lines at {designerNotesTextBox.TextZoomPercent}% zoom. {profileSummary}";
        }

        designerMotionCombo.SelectionChangeCommitted += (_, _) =>
        {
            if (designerMotionCombo.SelectedValue is not OpeningEffectType effect)
                return;

            designerSurfaceCombo.DropDownOpeningEffect = effect;
            designerModulesCombo.DropDownOpeningEffect = effect;
            designerCaretModeCombo.DropDownOpeningEffect = effect;
            UpdateDesignerControlStatus();
        };

        designerSurfaceCombo.SelectionChangeCommitted += (_, _) => UpdateDesignerControlStatus();
        designerModulesCombo.SelectionChangeCommitted += (_, _) => UpdateDesignerControlStatus();
        designerCaretModeCombo.SelectionChangeCommitted += (_, _) =>
        {
            if (designerCaretModeCombo.SelectedValue is TextBoxCaretMode caretMode)
                designerNotesTextBox.CaretMode = caretMode;

            UpdateDesignerControlStatus();
        };
        designerNotesTextBox.TextChanged += (_, _) => UpdateDesignerControlStatus();
        designerNotesTextBox.SelectionChanged += (_, _) => UpdateDesignerControlStatus();
        designerNotesTextBox.TextZoomFactorChanged += (_, _) => UpdateDesignerControlStatus();
        designerProfileDisplayNameTextBox.TextChanged += (_, _) => UpdateDesignerControlStatus();
        designerProfileHandleTextBox.TextChanged += (_, _) => UpdateDesignerControlStatus();
        designerProfileEmailTextBox.TextChanged += (_, _) => UpdateDesignerControlStatus();
        designerProfilePasswordTextBox.TextChanged += (_, _) => UpdateDesignerControlStatus();
        designerAccentPicker.SelectedColorCommitted += (_, _) =>
        {
            var accent = designerAccentPicker.SelectedColor;
            if (accent != SKColors.Transparent)
                ColorScheme.Primary = accent;

            UpdateDesignerControlStatus();
        };
        designerAccentPicker.SelectedColorChanged += (_, _) => UpdateDesignerControlStatus();

        UpdateDesignerControlStatus();

        designerInspectorSection.Controls.Add(designerMotionCombo);
        designerInspectorSection.Controls.Add(designerSurfaceCombo);
        designerInspectorSection.Controls.Add(designerModulesCombo);
        designerInspectorSection.Controls.Add(designerInspectorHeader);

        designerNotesSection.Controls.Add(designerNotesTextBox);
        designerNotesSection.Controls.Add(designerCaretModeCombo);
        designerNotesSection.Controls.Add(designerNotesHeader);

        designerAccentSection.Controls.Add(designerAccentPicker);
        designerAccentSection.Controls.Add(designerAccentHeader);

        Controls.Add(designerControlStatus);
        Controls.Add(designerAccentSection);
        Controls.Add(designerProfileForm);
        Controls.Add(designerNotesSection);
        Controls.Add(designerInspectorSection);
        Controls.Add(designerControlHeader);

    }
}