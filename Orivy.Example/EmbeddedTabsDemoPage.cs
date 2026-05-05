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

internal sealed partial class EmbeddedTabsDemoPage : Container
{
    private readonly List<SKImage> _embeddedImages = new();
    private TabView _embeddedTabView = null!;

    internal bool ShowTabStripResizer
    {
        get => _embeddedTabView?.ShowTabStripResizer ?? true;
        set
        {
            if (_embeddedTabView != null)
                _embeddedTabView.ShowTabStripResizer = value;
        }
    }

    public EmbeddedTabsDemoPage()
    {
        InitializeComponent();
    }

    private SKImage CreateEmbeddedIcon(SKColor accent, ExampleIconKind kind)
    {
        var image = ExampleHelper.CreateIcon(accent, kind);
        _embeddedImages.Add(image);
        return image;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            for (var i = 0; i < _embeddedImages.Count; i++)
                _embeddedImages[i].Dispose();
            _embeddedImages.Clear();
        }

        base.Dispose(disposing);
    }
}