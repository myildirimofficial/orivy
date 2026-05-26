using Orivy;
using SkiaSharp;
using System;

namespace Orivy.SettingsPreview;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ColorScheme.SetThemeInstant(false);
        ColorScheme.SetPrimarySeedColor(new SKColor(0x00, 0x78, 0xD4));
        Application.Run(new SettingsPreviewWindow());
    }
}
