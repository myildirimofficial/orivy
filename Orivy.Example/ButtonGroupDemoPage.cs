using Orivy.Controls;

namespace Orivy.Example;

internal sealed partial class ButtonGroupDemoPage : Container
{
    public ButtonGroupDemoPage()
    {
        InitializeComponent();
    }

    internal readonly struct ScaleFactorOption(string label, float value)
    {
        public string Label { get; } = label;
        public float Value { get; } = value;
        public override string ToString() => Label;
    }

    internal sealed class FontPreset
    {
        public string Family { get; init; } = string.Empty;
        public int Size { get; init; }
    }
}
