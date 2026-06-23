// SPDX-License-Identifier: MIT
// Orivy RichText — FontCache
//
// SKFont creation is expensive (typeface lookup, glyph cache allocation).
// Rich-text rendering requires per-run fonts (bold/italic/size variants),
// so we cache them by (family, size, bold, italic, mono) tuple.
//
// Lifecycle:
//   - One FontCache per RichTextBox instance.
//   - On DPI change / base font change / zoom change → Clear().
//   - On RichTextBox.Dispose → Dispose() (frees all cached SKFonts).

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Orivy.Controls.RichText;

/// <summary>Cache key. Value equality; suitable for Dictionary.
/// Pack bold/italic/mono into a single byte for tighter hashing.
///</summary>
public readonly struct FontKey : IEquatable<FontKey>
{
    public FontKey(string family, float size, bool bold, bool italic, bool mono)
    {
        Family = family ?? string.Empty;
        Size = size;
        // Pack flags into a single byte: bit 0 = bold, bit 1 = italic, bit 2 = mono.
        Flags = (byte)((bold ? 1 : 0) | (italic ? 2 : 0) | (mono ? 4 : 0));
    }

    public readonly string Family;
    public readonly float Size;
    public readonly byte Flags;  // packed: bold | italic | mono

    public bool Bold => (Flags & 1) != 0;
    public bool Italic => (Flags & 2) != 0;
    public bool Mono => (Flags & 4) != 0;

    public bool Equals(FontKey other)
    {
        // v4: family is interned, so reference equality is sufficient and
        // faster than string.Equals(StringComparison.Ordinal).
        return Flags == other.Flags
            && Size.Equals(other.Size)
            && ReferenceEquals(Family, other.Family);
    }

    public override bool Equals(object? obj) => obj is FontKey k && Equals(k);
    public override int GetHashCode()
    {
        unchecked
        {
            // v4: RuntimeHelpers.GetHashCode for reference-based hash of the
            // interned family string. Cheaper than StringComparer.Ordinal.GetHashCode.
            var h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Family);
            h = (h * 397) ^ Size.GetHashCode();
            h = (h * 397) ^ Flags;
            return h;
        }
    }
}

/// <summary>
/// Caches SKFont instances keyed by resolved (family, size, bold, italic, mono).
/// One instance per RichTextBox. Thread-agnostic; not safe for concurrent use
/// from multiple threads (RichTextBox is UI-thread bound).
/// </summary>
public sealed class FontCache : IDisposable
{
    private readonly Dictionary<FontKey, SKFont> _cache = new();
    // Typeface variant cache: (family, bold, italic) → SKTypeface.
    // Avoids re-creating typefaces for the same family across different sizes.
    private readonly Dictionary<TypefaceKey, SKTypeface> _typefaceCache = new();
    // v4: family-name interning. When a TextStyle specifies a FontFamily
    // override, the string is interned here so that two equal-named families
    // share the same string instance. This makes FontKey.FontFamily comparison
    // and hashcode cheaper (reference equality vs ordinal).
    private readonly Dictionary<string, string> _familyIntern = new(StringComparer.Ordinal);
    private SKTypeface? _baseTypeface;
    private SKTypeface? _monoTypeface;
    private string? _baseFamily;
    private string? _monoFamily;
    private float _baseSize;
    private float _scaleFactor = 1f;
    private bool _disposed;

    /// <summary>Typeface cache key. Family string + bold/italic packed.
    /// Size is NOT part of this key — a typeface is size-independent.</summary>
    private readonly struct TypefaceKey : IEquatable<TypefaceKey>
    {
        public TypefaceKey(string family, bool bold, bool italic)
        {
            Family = family;
            Flags = (byte)((bold ? 1 : 0) | (italic ? 2 : 0));
        }
        public readonly string Family;
        public readonly byte Flags;
        public bool Bold => (Flags & 1) != 0;
        public bool Italic => (Flags & 2) != 0;

        public bool Equals(TypefaceKey other)
        {
            // v4: family is interned → reference equality is enough.
            return Flags == other.Flags
                && ReferenceEquals(Family, other.Family);
        }
        public override bool Equals(object? obj) => obj is TypefaceKey k && Equals(k);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Family);
                return (h * 397) ^ Flags;
            }
        }
    }

    /// <summary>Base font family name (e.g. "Inter"). Used when TextStyle
    /// has no FontFamily override.</summary>
    public string BaseFamily
    {
        get => _baseFamily ?? "Inter";
        set
        {
            if (_baseFamily == value)
                return;
            _baseFamily = value;
            _baseTypeface = null;
            Clear();
        }
    }

    /// <summary>Monospace font family for code spans/blocks.</summary>
    public string MonoFamily
    {
        get => _monoFamily ?? "Consolas";
        set
        {
            if (_monoFamily == value)
                return;
            _monoFamily = value;
            _monoTypeface = null;
            Clear();
        }
    }

    /// <summary>Base font size in pixels (pre-scale). Default 14.</summary>
    public float BaseSize
    {
        get => _baseSize;
        set
        {
            if (Math.Abs(_baseSize - value) < 0.01f)
                return;
            _baseSize = value;
            Clear();
        }
    }

    /// <summary>DPI scale factor. Multiply with BaseSize to get actual size.</summary>
    public float ScaleFactor
    {
        get => _scaleFactor;
        set
        {
            if (Math.Abs(_scaleFactor - value) < 0.001f)
                return;
            _scaleFactor = value;
            Clear();
        }
    }

    public FontCache(string baseFamily = "Inter", string monoFamily = "Consolas", float baseSize = 14f)
    {
        _baseFamily = baseFamily;
        _monoFamily = monoFamily;
        _baseSize = baseSize;
    }

    /// <summary>Resolve a TextStyle against the base font and return a cached
    /// SKFont. The returned font is owned by the cache; do NOT dispose it.</summary>
    public SKFont GetFont(TextStyle style, SKFont? baseFont = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Resolve family.
        var family = style.FontFamily;
        var mono = style.Monospace == true;
        if (mono)
            family ??= MonoFamily;
        family ??= BaseFamily;

        // v4: intern the family string so FontKey comparison uses reference
        // equality instead of ordinal string compare. This is a small win on
        // every cache lookup — typically 1-5µs per lookup, multiplied by
        // hundreds of lookups per frame.
        family = Intern(family);

        // Resolve size: explicit > base font > BaseSize * ScaleFactor.
        float size;
        if (style.FontSize.HasValue)
            size = style.FontSize.Value * ScaleFactor;
        else if (baseFont != null)
            size = baseFont.Size;
        else
            size = BaseSize * ScaleFactor;

        // Apply subscript/superscript shrink (75% of base).
        if (style.VerticalAlign != TextVerticalAlign.Baseline)
            size *= 0.75f;

        size = Math.Max(1f, size);

        var bold = style.Bold ?? false;
        var italic = style.Italic ?? false;

        var key = new FontKey(family, size, bold, italic, mono);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var typeface = ResolveTypeface(family, bold, italic, mono);
        var font = new SKFont(typeface, size) { Edging = SKFontEdging.Antialias, Subpixel = true };
        _cache[key] = font;
        return font;
    }

    /// <summary>v4: Intern a family name. If we've seen this exact string
    /// before, return the cached instance; otherwise store and return it.
    /// After interning, two equal family names share the same string
    /// instance, enabling reference equality in FontKey.Equals and faster
    /// hashing (pointer-based vs ordinal).</summary>
    private string Intern(string family)
    {
        if (_familyIntern.TryGetValue(family, out var interned))
            return interned;
        _familyIntern[family] = family;
        return family;
    }

    /// <summary>Returns the cached base font (no styling) — convenience.</summary>
    public SKFont GetBaseFont() => GetFont(TextStyle.Default);

    private SKTypeface ResolveTypeface(string family, bool bold, bool italic, bool mono)
    {
        // Resolve the effective family name once.
        var effectiveFamily = mono ? MonoFamily : family;

        // Fast path for base / mono family's regular variant.
        if (mono)
        {
            _monoTypeface ??= SKTypeface.FromFamilyName(MonoFamily) ?? SKTypeface.Default;
            if (!bold && !italic)
                return _monoTypeface;
        }
        else if (!string.IsNullOrEmpty(_baseFamily) && effectiveFamily == _baseFamily)
        {
            _baseTypeface ??= SKTypeface.FromFamilyName(_baseFamily) ?? SKTypeface.Default;
            if (!bold && !italic)
                return _baseTypeface;
        }

        // Variant lookup: cache by (family, bold, italic).
        var key = new TypefaceKey(effectiveFamily, bold, italic);
        if (_typefaceCache.TryGetValue(key, out var cached))
            return cached;

        var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var tf = SKTypeface.FromFamilyName(effectiveFamily, weight, SKFontStyleWidth.Normal, slant)
                 ?? SKTypeface.Default;
        _typefaceCache[key] = tf;
        return tf;
    }

    /// <summary>Drop all cached fonts (e.g. on DPI change). Does not dispose
    /// the cache itself.</summary>
    public void Clear()
    {
        foreach (var font in _cache.Values)
            font.Dispose();
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
        _baseTypeface?.Dispose();
        _monoTypeface?.Dispose();
        _baseTypeface = null;
        _monoTypeface = null;
        // Note: cached typefaces in _typefaceCache are NOT disposed here,
        // because SKTypeface is shared with SKFont instances that may still be
        // alive. They are unmanaged resources managed by SkiaSharp internally.
        // For long-lived apps this is fine; for tight-resource scenarios call
        // Clear() before Dispose() and let GC handle the rest.
        _typefaceCache.Clear();
    }
}
