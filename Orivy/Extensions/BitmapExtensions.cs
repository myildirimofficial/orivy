using SkiaSharp;
using System;
using System.Drawing;

namespace Orivy.Extensions;

public static class BitmapExtensions
{
    /// <summary>
    /// Converts a System.Drawing.Bitmap to a SkiaSharp.SKBitmap.
    /// The caller is responsible for disposing the returned SKBitmap.
    /// </summary>
    public unsafe static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        // Lock the bitmap's bits
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        try
        {
            // Create SKBitmap with the same dimensions
            var skBitmap = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            // Copy pixel data
            System.Buffer.MemoryCopy(
                source: (void*)bmpData.Scan0,
                destination: (void*)skBitmap.GetPixels(),
                destinationSizeInBytes: skBitmap.ByteCount,
                sourceBytesToCopy: skBitmap.ByteCount);

            return skBitmap;
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }/// <summary>
     /// Converts a System.Drawing.Bitmap to a SkiaSharp.SKImage.
     /// The caller is responsible for disposing the returned SKImage.
     /// </summary>
    public unsafe static SKImage ToSKImage(this Bitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        // Lock the bitmap's bits
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        try
        {
            // Create SKImage with the same dimensions
            var skImage = SKImage.FromPixels(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul), bmpData.Scan0, bmpData.Stride);

            return skImage;
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }
}
