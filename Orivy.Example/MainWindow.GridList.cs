using SkiaSharp;
using System.Collections.Generic;

namespace Orivy.Example;

internal partial class MainWindow
{
    private readonly List<SKImage> _exampleImages = new();

    private SKImage CreateExampleIcon(SKColor accent, ExampleIconKind kind)
    {
        var image = ExampleHelper.CreateIcon(accent, kind);
        _exampleImages.Add(image);
        return image;
    }
}