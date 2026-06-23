// SPDX-License-Identifier: MIT
// Orivy RichText — RTF Writer
//
// Serializes a StyledTextDocument to an RTF string. Produces output that
// WordPad, LibreOffice, and Word can open. Supports the same subset as
// RtfReader: bold, italic, underline, strikethrough, sub/super, font size,
// font family, foreground/background colors, paragraphs (\par), tabs.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Orivy.Controls.RichText.Rtf;

/// <summary>Serializes a StyledTextDocument to RTF.</summary>
public sealed class RtfWriter
{
    private readonly StringBuilder _sb = new();
    private readonly List<string> _fontTable = new();
    private readonly List<SKColor> _colorTable = new();
    private readonly Dictionary<string, int> _fontIndex = new();
    private readonly Dictionary<SKColor, int> _colorIndex = new();

    /// <summary>Render the document as an RTF string.</summary>
    public string Write(StyledTextDocument document, string defaultFontFamily = "Inter")
    {
        _sb.Clear();
        _fontTable.Clear();
        _colorTable.Clear();
        _fontIndex.Clear();
        _colorIndex.Clear();

        // Phase 1: walk runs to collect font & color tables.
        CollectTables(document, defaultFontFamily);

        // Phase 2: emit header.
        EmitHeader();

        // Phase 3: emit font table.
        EmitFontTable();

        // Phase 4: emit color table.
        EmitColorTable();

        // Phase 5: emit body.
        EmitBody(document);

        return _sb.ToString();
    }

    // ── Phase 1: collect tables ────────────────────────────────────────

    private void CollectTables(StyledTextDocument doc, string defaultFamily)
    {
        // Always include the default font.
        GetOrCreateFontIndex(defaultFamily);

        // Auto color (index 0).
        GetOrCreateColorIndex(SKColor.Empty);

        foreach (var run in doc.Runs)
        {
            var family = ResolveFontFamily(run.Style, defaultFamily);
            GetOrCreateFontIndex(family);

            if (run.Style.ForeColor.HasValue)
                GetOrCreateColorIndex(run.Style.ForeColor.Value);
            if (run.Style.BackColor.HasValue)
                GetOrCreateColorIndex(run.Style.BackColor.Value);
        }
    }

    private static string ResolveFontFamily(TextStyle style, string defaultFamily)
    {
        if (style.Monospace == true)
            return "Consolas";
        return style.FontFamily ?? defaultFamily;
    }

    private int GetOrCreateFontIndex(string family)
    {
        if (_fontIndex.TryGetValue(family, out var idx))
            return idx;
        idx = _fontTable.Count;
        _fontTable.Add(family);
        _fontIndex[family] = idx;
        return idx;
    }

    private int GetOrCreateColorIndex(SKColor color)
    {
        if (_colorIndex.TryGetValue(color, out var idx))
            return idx;
        idx = _colorTable.Count;
        _colorTable.Add(color);
        _colorIndex[color] = idx;
        return idx;
    }

    // ── Phase 2: header ────────────────────────────────────────────────

    private void EmitHeader()
    {
        _sb.Append("{\\rtf1\\ansi\\deff0");
    }

    // ── Phase 3: font table ────────────────────────────────────────────

    private void EmitFontTable()
    {
        _sb.Append("\n{\\fonttbl");
        for (var i = 0; i < _fontTable.Count; i++)
        {
            var family = _fontTable[i];
            _sb.Append($"{{\\f{i}\\fnil\\fcharset0 {EscapeRtfText(family)};}}");
        }
        _sb.Append("}\n");
    }

    // ── Phase 4: color table ───────────────────────────────────────────

    private void EmitColorTable()
    {
        _sb.Append("{\\colortbl");
        foreach (var c in _colorTable)
        {
            if (c == SKColor.Empty)
                _sb.Append(';');
            else
                _sb.Append($"\\red{c.Red}\\green{c.Green}\\blue{c.Blue};");
        }
        _sb.Append("}\n");
    }

    // ── Phase 5: body ──────────────────────────────────────────────────

    private void EmitBody(StyledTextDocument doc)
    {
        // Emit each run, applying its style. Style changes are emitted inline
        // (RTF supports cumulative control words; we use \plain to reset before
        // each run for cleanliness).
        var text = doc.Text;

        foreach (var run in doc.Runs)
        {
            // Reset formatting then apply.
            _sb.Append("\\plain");

            // Font family.
            var family = ResolveFontFamily(run.Style, _fontTable[0]);
            var fontIdx = GetOrCreateFontIndex(family);
            _sb.Append($"\\f{fontIdx}");

            // Font size (half-points). Default 24 (= 12pt).
            float points;
            if (run.Style.FontSize.HasValue)
                points = run.Style.FontSize.Value / 1.33f;  // px → pt
            else
                points = 12f;
            _sb.Append($"\\fs{(int)(points * 2)}");

            // Bold / italic / underline / strike.
            if (run.Style.Bold == true) _sb.Append("\\b");
            if (run.Style.Italic == true) _sb.Append("\\i");
            if (run.Style.Underline == true) _sb.Append("\\ul");
            if (run.Style.Strikethrough == true) _sb.Append("\\strike");
            if (run.Style.VerticalAlign == TextVerticalAlign.Subscript) _sb.Append("\\sub");
            if (run.Style.VerticalAlign == TextVerticalAlign.Superscript) _sb.Append("\\super");

            // Colors.
            if (run.Style.ForeColor.HasValue)
            {
                var ci = GetOrCreateColorIndex(run.Style.ForeColor.Value);
                _sb.Append($"\\cf{ci}");
            }
            if (run.Style.BackColor.HasValue)
            {
                var ci = GetOrCreateColorIndex(run.Style.BackColor.Value);
                _sb.Append($"\\cb{ci}");
            }

            // Text content — escape and translate newlines.
            _sb.Append(' ');
            EmitRunText(text, run.Start, run.Length);
        }

        _sb.Append('}');
    }

    private void EmitRunText(string text, int start, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var c = text[start + i];
            switch (c)
            {
                case '\\': _sb.Append("\\\\"); break;
                case '{': _sb.Append("\\{"); break;
                case '}': _sb.Append("\\}"); break;
                case '\n': _sb.Append("\\par\n"); break;
                case '\r': break;  // skip CR
                case '\t': _sb.Append("\\tab "); break;
                default:
                    if (c < 0x80)
                    {
                        _sb.Append(c);
                    }
                    else
                    {
                        // Emit as \uN escape. Negative values for chars > 32767.
                        var code = (int)c;
                        if (code > 32767) code -= 65536;
                        _sb.Append($"\\u{code}?");  // ? is the ANSI fallback.
                    }
                    break;
            }
        }
    }

    private static string EscapeRtfText(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '{': sb.Append("\\{"); break;
                case '}': sb.Append("\\}"); break;
                case ';': sb.Append("\\;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
