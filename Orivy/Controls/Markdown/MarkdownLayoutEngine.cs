using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

// ============================================================================
// Box model
// ============================================================================

internal abstract class MdBox
{
    public SKRect Bounds;
}

internal sealed class TextRunBox : MdBox
{
    public string Text = "";
    public SKFont Font = null!;
    public SKColor Color;
    public SKPoint Baseline;
    public LinkInline? Link;
    public bool Underline;
    public bool Strike;
    public bool IsEmoji;          // use emoji fallback typeface

    // ---- selection helpers (called by renderer, no allocation) ----

    /// <summary>Character offset (0-based, ≤ Text.Length) at the given local X within this run.</summary>
    public int GetCharOffsetAt(float localX)
    {
        if (string.IsNullOrEmpty(Text) || localX <= 0f) return 0;
        float cx = 0f;
        for (int i = 0; i < Text.Length; )
        {
            int len = char.IsSurrogatePair(Text, i) ? 2 : 1;
            float w = Font.MeasureText(Text.AsSpan(i, len));
            if (localX < cx + w * 0.5f) return i;
            cx += w;
            i += len;
        }
        return Text.Length;
    }

    /// <summary>Pixel X offset (from Bounds.Left) of the given character offset.</summary>
    public float GetXAtOffset(int offset)
    {
        if (offset <= 0 || string.IsNullOrEmpty(Text)) return 0f;
        int clamped = Math.Min(offset, Text.Length);
        return Font.MeasureText(Text.AsSpan(0, clamped));
    }
}

internal sealed class RectBox : MdBox
{
    public SKColor? Fill;
    public SKColor? Stroke;
    public float StrokeWidth;
    public float CornerRadius;
}

internal sealed class ImageBox : MdBox
{
    public ImageInline Source = null!;
    public LinkInline? Link;
}

internal sealed class CheckboxBox : MdBox
{
    public bool Checked;
    public ListItemBlock Item = null!;
}

internal sealed class AlertHeaderBox : MdBox
{
    public AlertKind Kind;
}

internal sealed class CodeBlockBox : MdBox
{
    public CodeBlockBlock Source = null!;
    public List<List<TextRunBox>> Lines = new();
    public float ContentWidth;
    public string? Language;
    public SKRect HeaderRect;
    public SKRect BodyRect;
    public SKRect CopyButtonRect;
    public SKPoint BodyOrigin;
    public float LineHeight;
    public bool NeedsHorizontalScroll;
    public CodeBlockScrollState Scroll = null!;
    public float ViewportWidth;
}

internal sealed class DetailsHeaderBox : MdBox
{
    public DetailsBlock Source = null!;
    public bool Expanded;
}

// ============================================================================
// Selection state  (owned by MarkdownViewer, invalidated on reflow)
// ============================================================================

internal struct TextPosition
{
    /// <summary>Index into the flat MdBox list (-1 = no position).</summary>
    public int BoxIndex;
    /// <summary>Character offset within the TextRunBox.Text string.</summary>
    public int CharOffset;

    public bool IsValid => BoxIndex >= 0;
    public static TextPosition Invalid => new() { BoxIndex = -1 };

    public bool IsBefore(TextPosition other) =>
        BoxIndex < other.BoxIndex || (BoxIndex == other.BoxIndex && CharOffset < other.CharOffset);
}

internal sealed class MarkdownSelectionState
{
    public TextPosition Start = TextPosition.Invalid;
    public TextPosition End   = TextPosition.Invalid;
    public bool IsSelecting;

    public bool HasSelection => Start.IsValid && End.IsValid &&
        (Start.BoxIndex != End.BoxIndex || Start.CharOffset != End.CharOffset);

    public (TextPosition from, TextPosition to) Ordered() =>
        Start.IsBefore(End) ? (Start, End) : (End, Start);

    public void Clear() { Start = TextPosition.Invalid; End = TextPosition.Invalid; IsSelecting = false; }
}

// ============================================================================
// Interaction state (persists across reflows)
// ============================================================================

internal sealed class CodeBlockScrollState
{
    public float ScrollX;
}

internal sealed class MarkdownInteractionState
{
    public IMarkdownImageProvider? ImageProvider;
    public Action<string, SKImage?> OnImageLoaded = (_, _) => { };
    public Dictionary<CodeBlockBlock, CodeBlockScrollState> CodeScroll = new();
    public Dictionary<DetailsBlock, bool> DetailsExpanded = new();
}

internal sealed class MarkdownHoverState
{
    public LinkInline? HoveredLink;
    public CodeBlockBox? HoveredCodeBlock;
    public bool HoveredCopyButton;
    public bool HoveredText;   // true when cursor is over selectable text
}

// ============================================================================
// Font cache
// ============================================================================

internal sealed class MarkdownFontCache : IDisposable
{
    private readonly Dictionary<(bool Mono, bool Bold, bool Italic), SKTypeface> _typefaces = new();
    private readonly Dictionary<(bool Mono, bool Bold, bool Italic, int SizeTenths), SKFont> _fonts = new();
    private SKTypeface? _hostBodyTypeface;
    private SKTypeface? _emojiTypeface;
    private bool _emojiFontSearched;

    public void SetHostBodyTypeface(SKTypeface? typeface) => _hostBodyTypeface = typeface;

    public SKTypeface? GetEmojiTypeface()
    {
        if (_emojiFontSearched) return _emojiTypeface;
        _emojiFontSearched = true;
        string[] candidates = { "Segoe UI Emoji", "Apple Color Emoji", "Noto Color Emoji",
                                 "Noto Emoji", "Twitter Color Emoji", "EmojiOne Color" };
        foreach (var name in candidates)
        {
            var tf = SKFontManager.Default.MatchFamily(name);
            if (tf != null) { _emojiTypeface = tf; break; }
        }
        return _emojiTypeface;
    }

    public SKFont GetFont(MarkdownTheme theme, bool mono, float sizePx, bool bold, bool italic)
    {
        int sizeTenths = (int)MathF.Round(Math.Max(1f, sizePx) * 10f);
        var key = (mono, bold, italic, sizeTenths);
        if (_fonts.TryGetValue(key, out var existing)) return existing;

        var typeface = GetTypeface(theme, mono, bold, italic);
        float size = sizeTenths / 10f;
        var font = new SKFont(typeface, size)
        {
            Subpixel = true,
            Edging = SKFontEdging.SubpixelAntialias,
            Embolden = bold && typeface.FontStyle.Weight < (int)SKFontStyleWeight.SemiBold,
        };
        _fonts[key] = font;
        return font;
    }

    private SKTypeface GetTypeface(MarkdownTheme theme, bool mono, bool bold, bool italic)
    {
        var key = (mono, bold, italic);
        if (_typefaces.TryGetValue(key, out var existing)) return existing;

        var weight = bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal;
        var slant  = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var style  = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

        SKTypeface? typeface = null;
        if (mono)
        {
            foreach (var family in theme.MonospaceFontFamilies)
            {
                typeface = SKFontManager.Default.MatchFamily(family, style);
                if (typeface != null) break;
            }
        }
        else
        {
            if (theme.UseHostDefaultFontForBody && !bold && !italic && _hostBodyTypeface != null)
                typeface = _hostBodyTypeface;
            if (typeface == null && theme.UseHostDefaultFontForBody && _hostBodyTypeface != null)
                typeface = SKFontManager.Default.MatchFamily(_hostBodyTypeface.FamilyName, style);
            if (typeface == null)
                foreach (var family in theme.BodyFontFamilies)
                {
                    typeface = SKFontManager.Default.MatchFamily(family, style);
                    if (typeface != null) break;
                }
        }

        typeface ??= SKTypeface.FromFamilyName(null, style) ?? SKTypeface.Default;
        _typefaces[key] = typeface;
        return typeface;
    }

    public void Dispose()
    {
        foreach (var f in _fonts.Values) f.Dispose();
        _fonts.Clear();
        foreach (var t in _typefaces.Values) t.Dispose();
        _typefaces.Clear();
    }
}
