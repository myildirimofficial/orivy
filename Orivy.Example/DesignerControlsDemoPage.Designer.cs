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

        Controls.Add(designerControlShell);
        Controls.Add(designerControlHeader);
    
    }
}
