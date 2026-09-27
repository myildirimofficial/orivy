using Orivy;
using System;
using System.Globalization;
using System.Linq;

namespace Orivy.Studio;

/// <summary>Literal text for Designer C# so merged/generated files compile without extra usings or float/double ambiguity.</summary>
internal static class DesignSourceFormat
{
    public static string Dock(DockStyle dock) => $"Orivy.DockStyle.{dock}";

    public static string Anchor(AnchorStyles anchor)
    {
        if (anchor == AnchorStyles.None)
            return "Orivy.AnchorStyles.None";

        var flags = Enum.GetValues<AnchorStyles>()
            .Where(flag => flag != AnchorStyles.None && anchor.HasFlag(flag))
            .Select(flag => $"Orivy.AnchorStyles.{flag}");
        return string.Join(" | ", flags);
    }

    /// <summary>Rounded integer — SKPoint/SKSize literals must not be bare decimals (inferred as double).</summary>
    public static string Pixel(float value) =>
        ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture);
}
