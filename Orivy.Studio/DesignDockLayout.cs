using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System.ComponentModel;

namespace Orivy.Studio;

/// <summary>
/// Which layout axes the designer may edit while <see cref="ElementBase.Dock"/> is set.
/// Matches <see cref="Orivy.Layout.DefaultLayout"/> (Top/Bottom → height, Left/Right → width, Fill → neither).
/// </summary>
internal static class DesignDockLayout
{
    public static bool AllowsWidth(DockStyle dock) =>
        dock is DockStyle.None or DockStyle.Left or DockStyle.Right;

    public static bool AllowsHeight(DockStyle dock) =>
        dock is DockStyle.None or DockStyle.Top or DockStyle.Bottom;

    public static bool AllowsLocation(DockStyle dock) => dock == DockStyle.None;

    public static bool BlocksPropertyEdit(ElementBase control, PropertyDescriptor descriptor)
    {
        if (descriptor.Name == "Location")
            return !AllowsLocation(control.Dock);

        if (descriptor.Name == "Size")
            return control.Dock == DockStyle.Fill;

        return false;
    }

    public static SKSize ClampSize(DockStyle dock, SKSize value, SKSize fallback)
    {
        var width = AllowsWidth(dock) ? value.Width : fallback.Width;
        var height = AllowsHeight(dock) ? value.Height : fallback.Height;
        return new SKSize(width, height);
    }
}
