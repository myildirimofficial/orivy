// SPDX-License-Identifier: MIT
// Orivy RichText — TextStyle
// Cross-platform SkiaSharp rich text support for Orivy.Controls.TextBox
//
// Immutable text style. Nullable fields mean "inherit from base font".
// Two TextStyles are equal if all fields match; equality is used by
// StyledTextDocument to merge adjacent runs and by FontCache to key fonts.

using System;
using SkiaSharp;

namespace Orivy.Controls.RichText;

/// <summary>
/// Vertical alignment for subscript/superscript runs.
/// </summary>
public enum TextVerticalAlign
{
    /// <summary>Normal baseline.</summary>
    Baseline = 0,

    /// <summary>Subscript (lowered, smaller).</summary>
    Subscript = 1,

    /// <summary>Superscript (raised, smaller).</summary>
    Superscript = 2,
}

/// <summary>
/// Immutable text style. All nullable fields mean "inherit from base font";
/// when null, the run uses whatever the owning RichTextBox's base Font provides.
/// </summary>
public readonly struct TextStyle : IEquatable<TextStyle>
{
    /// <summary>Font family override. Null = inherit base.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Font size in pixels (already DPI-scaled). Null = inherit base.</summary>
    public float? FontSize { get; init; }

    /// <summary>Bold. Null = inherit base.</summary>
    public bool? Bold { get; init; }

    /// <summary>Italic. Null = inherit base.</summary>
    public bool? Italic { get; init; }

    /// <summary>Underline. Null = inherit base.</summary>
    public bool? Underline { get; init; }

    /// <summary>Strikethrough. Null = inherit base.</summary>
    public bool? Strikethrough { get; init; }

    /// <summary>Superscript flag (legacy convenience; prefer VerticalAlign).</summary>
    public bool? Superscript { get; init; }

    /// <summary>Subscript flag (legacy convenience; prefer VerticalAlign).</summary>
    public bool? Subscript { get; init; }

    /// <summary>Vertical alignment (sub/super). Defaults to Baseline.</summary>
    public TextVerticalAlign VerticalAlign { get; init; }

    /// <summary>Foreground color. Null = inherit base ForeColor.</summary>
    public SKColor? ForeColor { get; init; }

    /// <summary>Background color (highlight). Null = transparent.</summary>
    public SKColor? BackColor { get; init; }

    /// <summary>Hyperlink URL. Null = not a link.</summary>
    public string? Hyperlink { get; init; }

    /// <summary>Monospace flag (convenience for code spans). When true,
    /// FontFamily is treated as the RichTextBox's configured monospace family.</summary>
    public bool? Monospace { get; init; }

    /// <summary>Default style — all-null, inherits base.</summary>
    public static TextStyle Default => default;

    /// <summary>True if every field is at its default (i.e. inherits base).</summary>
    public bool IsDefault
    {
        get
        {
            return FontFamily is null
                && FontSize is null
                && Bold is null
                && Italic is null
                && Underline is null
                && Strikethrough is null
                && Superscript is null
                && Subscript is null
                && VerticalAlign == TextVerticalAlign.Baseline
                && ForeColor is null
                && BackColor is null
                && Hyperlink is null
                && Monospace is null;
        }
    }

    /// <summary>
    /// Merge: non-null values from <paramref name="other"/> override this.
    /// Used when nesting styles (e.g. bold link inside italic paragraph).
    /// </summary>
    public TextStyle Merge(TextStyle other)
    {
        return new TextStyle
        {
            FontFamily = other.FontFamily ?? FontFamily,
            FontSize = other.FontSize ?? FontSize,
            Bold = other.Bold ?? Bold,
            Italic = other.Italic ?? Italic,
            Underline = other.Underline ?? Underline,
            Strikethrough = other.Strikethrough ?? Strikethrough,
            Superscript = other.Superscript ?? Superscript,
            Subscript = other.Subscript ?? Subscript,
            VerticalAlign = other.VerticalAlign != TextVerticalAlign.Baseline
                ? other.VerticalAlign
                : VerticalAlign,
            ForeColor = other.ForeColor ?? ForeColor,
            BackColor = other.BackColor ?? BackColor,
            Hyperlink = other.Hyperlink ?? Hyperlink,
            Monospace = other.Monospace ?? Monospace,
        };
    }

    /// <summary>Returns a copy with specified fields overridden.</summary>
    public TextStyle With(
        string? fontFamily = null,
        float? fontSize = null,
        bool? bold = null,
        bool? italic = null,
        bool? underline = null,
        bool? strikethrough = null,
        string? hyperlink = null,
        SKColor? foreColor = null,
        SKColor? backColor = null,
        bool? monospace = null,
        TextVerticalAlign? verticalAlign = null)
    {
        return new TextStyle
        {
            FontFamily = fontFamily ?? FontFamily,
            FontSize = fontSize ?? FontSize,
            Bold = bold ?? Bold,
            Italic = italic ?? Italic,
            Underline = underline ?? Underline,
            Strikethrough = strikethrough ?? Strikethrough,
            Superscript = Superscript,
            Subscript = Subscript,
            VerticalAlign = verticalAlign ?? VerticalAlign,
            ForeColor = foreColor ?? ForeColor,
            BackColor = backColor ?? BackColor,
            Hyperlink = hyperlink ?? Hyperlink,
            Monospace = monospace ?? Monospace,
        };
    }

    public bool Equals(TextStyle other)
    {
        return string.Equals(FontFamily, other.FontFamily, StringComparison.Ordinal)
            && Nullable.Equals(FontSize, other.FontSize)
            && Nullable.Equals(Bold, other.Bold)
            && Nullable.Equals(Italic, other.Italic)
            && Nullable.Equals(Underline, other.Underline)
            && Nullable.Equals(Strikethrough, other.Strikethrough)
            && Nullable.Equals(Superscript, other.Superscript)
            && Nullable.Equals(Subscript, other.Subscript)
            && VerticalAlign == other.VerticalAlign
            && Nullable.Equals(ForeColor, other.ForeColor)
            && Nullable.Equals(BackColor, other.BackColor)
            && string.Equals(Hyperlink, other.Hyperlink, StringComparison.Ordinal)
            && Nullable.Equals(Monospace, other.Monospace);
    }

    public override bool Equals(object? obj) => obj is TextStyle other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (FontFamily?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = hash * 31 + FontSize.GetHashCode();
            hash = hash * 31 + Bold.GetHashCode();
            hash = hash * 31 + Italic.GetHashCode();
            hash = hash * 31 + Underline.GetHashCode();
            hash = hash * 31 + Strikethrough.GetHashCode();
            hash = hash * 31 + Superscript.GetHashCode();
            hash = hash * 31 + Subscript.GetHashCode();
            hash = hash * 31 + (int)VerticalAlign;
            hash = hash * 31 + ForeColor.GetHashCode();
            hash = hash * 31 + BackColor.GetHashCode();
            hash = hash * 31 + (Hyperlink?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = hash * 31 + Monospace.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(TextStyle left, TextStyle right) => left.Equals(right);
    public static bool operator !=(TextStyle left, TextStyle right) => !left.Equals(right);

    // ── Convenience presets ─────────────────────────────────────────────

    public static TextStyle BoldStyle => new() { Bold = true };
    public static TextStyle ItalicStyle => new() { Italic = true };
    public static TextStyle UnderlineStyle => new() { Underline = true };
    public static TextStyle StrikethroughStyle => new() { Strikethrough = true };
    public static TextStyle CodeStyle => new() { Monospace = true };
    public static TextStyle SubscriptStyle => new() { VerticalAlign = TextVerticalAlign.Subscript };
    public static TextStyle SuperscriptStyle => new() { VerticalAlign = TextVerticalAlign.Superscript };
}
