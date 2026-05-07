using Orivy.Controls;
using System;

namespace Orivy.Example;

internal sealed partial class VisualStylesDemoPage : Container
{
    public VisualStylesDemoPage()
    {
        InitializeComponent();
    }

    private bool _dangerModeEnabled;

    private void VisualStyleDangerToggle_Click(object? sender, EventArgs e)
    {
        _dangerModeEnabled = !_dangerModeEnabled;
        visualStyleDangerCard.Tag = _dangerModeEnabled ? "danger" : "normal";
        visualStyleDangerCard.Text = _dangerModeEnabled
            ? "Predicate Card\nDanger mode active. Click again to revert."
            : "Predicate Card\nClick to toggle a custom predicate state.";
        visualStyleDangerCard.ReevaluateVisualStyles();
    }

    private void VisualStyleEnableDisabled_Click(object? sender, EventArgs e)
    {
        visualStyleDisabledCard.Enabled = !visualStyleDisabledCard.Enabled;
        visualStyleDisabledCard.Text = visualStyleDisabledCard.Enabled
            ? "Disabled State Card\nEnabled again. Click the footer action to disable it."
            : "Disabled State Card\nThis card is disabled and styled by OnDisabled.";
    }

    private void VisualStylePrimaryButton_Click(object? sender, EventArgs e)
    {
        visualStyleGhostButton.Text = "Secondary Button - Ready";
        visualStyleScrollProbe.Text = "Scroll Probe\nPrimary button clicked. If you can reach this block, AutoScroll and the Button visual style states are both working together.";
    }
}
