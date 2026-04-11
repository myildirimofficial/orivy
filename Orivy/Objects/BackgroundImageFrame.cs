using SkiaSharp;
using System;

namespace Orivy;

public sealed class BackgroundImageFrame
{
    public BackgroundImageFrame(
        SKImage image,
        BackgroundImageCaption? caption = null,
        ContentAlignment captionLayout = ContentAlignment.MiddleLeft,
        BackgroundImageCaptionDesignMode captionDesignMode = BackgroundImageCaptionDesignMode.Overlay)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        Caption = caption ?? BackgroundImageCaption.Empty;
        CaptionLayout = captionLayout;
        CaptionDesignMode = captionDesignMode;
    }

    public SKImage Image { get; }

    public BackgroundImageCaption Caption { get; }

    public ContentAlignment CaptionLayout { get; }

    public BackgroundImageCaptionDesignMode CaptionDesignMode { get; }
}