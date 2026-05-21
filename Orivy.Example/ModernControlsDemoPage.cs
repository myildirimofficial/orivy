using Orivy.Controls;
using System;

namespace Orivy.Example;

internal sealed partial class ModernControlsDemoPage : Container
{
    private ProgressBar linearProgress = null!;
    private ProgressBar segmentedProgress = null!;
    private ProgressBar gradientProgress = null!;
    private ProgressBar stripedProgress = null!;
    private ProgressBar dotsProgress = null!;
    private ProgressBar blocksProgress = null!;
    private ProgressBar circularProgress = null!;
    private ProgressBar ringProgress = null!;
    private ToggleButton motionToggle = null!;
    private TrackBar progressTrack = null!;
    private NumericUpDown progressNumeric = null!;

    public ModernControlsDemoPage()
    {
        InitializeComponent();
    }

    private void AdvanceProgress(float delta)
    {
        linearProgress.Value = WrapProgress(linearProgress.Value + delta);
        segmentedProgress.Value = WrapProgress(segmentedProgress.Value + delta * 0.8f);
        gradientProgress.Value = WrapProgress(gradientProgress.Value + delta * 0.95f);
        stripedProgress.Value = WrapProgress(stripedProgress.Value + delta * 0.7f);
        dotsProgress.Value = WrapProgress(dotsProgress.Value + delta * 0.9f);
        blocksProgress.Value = WrapProgress(blocksProgress.Value + delta * 1.05f);
        circularProgress.Value = WrapProgress(circularProgress.Value + delta * 1.15f);
        ringProgress.Value = WrapProgress(ringProgress.Value + delta * 0.55f);
        progressTrack.Value = linearProgress.Value;
        progressNumeric.Value = (decimal)MathF.Round(linearProgress.Value);
    }

    private void ToggleMotion()
    {
        var enabled = motionToggle.Checked;
        linearProgress.Visible = enabled;
        segmentedProgress.Visible = enabled;
        gradientProgress.Visible = enabled;
        stripedProgress.Visible = enabled;
        dotsProgress.Visible = enabled;
        blocksProgress.Visible = enabled;
        circularProgress.Visible = enabled;
        ringProgress.Visible = enabled;
    }

    private void SetProgress(float value)
    {
        var normalized = Math.Clamp(value, 0f, 100f);
        linearProgress.Value = normalized;
        segmentedProgress.Value = normalized;
        gradientProgress.Value = normalized;
        stripedProgress.Value = normalized;
        dotsProgress.Value = normalized;
        blocksProgress.Value = normalized;
        circularProgress.Value = normalized;
        ringProgress.Value = normalized;
        progressTrack.Value = normalized;
        progressNumeric.Value = (decimal)MathF.Round(normalized);
    }

    private static float WrapProgress(float value)
    {
        return value > 100f ? value - 100f : value;
    }
}
