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

#nullable disable

namespace Orivy.Example;

internal sealed partial class BindingDemoPage : Container
{
    private readonly BindingDemoViewModel _bindingDemoViewModel = new();
    private SKImage _tabIcon;
    private Container _bindingPanel = null!;
    private ComboBox bindingValidationTeamCombo = null!;
    private ComboBox bindingValidationPresetCombo = null!;
    private Element bindingValidationRegionError = null!;
    private Element bindingValidationPresetError = null!;
    private Element bindingValidationStatusCard = null!;
    private Button bindingValidationSubmitButton = null!;

    public BindingDemoPage()
    {
        InitializeComponent();
    }

    public override void  Dispose(bool disposing)
    {
        if (disposing)
        {
            _tabIcon?.Dispose();
            _tabIcon = null;
        }

        base.Dispose(disposing);
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

        private void BindingValidationSubmitButton_Click(object sender, EventArgs e)
        {
            var isTeamValid = bindingValidationTeamCombo.ValidateNow();
            var isPresetValid = bindingValidationPresetCombo.ValidateNow();

            bindingValidationStatusCard.Text = isTeamValid && isPresetValid
                ? "Validation Submit\nAll rules passed. Existing ValidationRule infrastructure is now gating this small workflow."
                : "Validation Submit\nFix the highlighted rule breaches before continuing. The cards above are bound directly to ValidationText and HasValidationError.";

            bindingValidationStatusCard.BackColor = isTeamValid && isPresetValid
                ? ColorScheme.Primary.WithAlpha(196)
                : new SKColor(185, 28, 28);
            bindingValidationStatusCard.ForeColor = SKColors.White;
        }
}
