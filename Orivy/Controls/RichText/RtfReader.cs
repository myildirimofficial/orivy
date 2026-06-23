// SPDX-License-Identifier: MIT
// Orivy RichText — RTF Reader
//
// Parses an RTF string into a StyledTextDocument. Supports:
//   - \rtf1 \ansi \deff0 (header; validated minimally)
//   - \fonttbl groups (font table; fonts referenced by \fN)
//   - \colortbl groups (color table; colors referenced by \cfN \cbN)
//   - \b \b0 \i \i0 \ul \ul0 \strike \strike0 \sub \super \nosupersub
//   - \fsN (font size in half-points)
//   - \fN (font index into font table)
//   - \cfN \cbN (foreground/background color index)
//   - \par (paragraph break = newline)
//   - \line (line break = newline)
//   - \tab (tab character)
//   - \\ \{ \} (literal escapes)
//   - \uN (Unicode escape; N is signed 16-bit)
//   - \'HH (hex byte escape; interpreted as ANSI/Latin-1)
//
// Unknown control words are silently skipped (with their optional parameter).
// Unknown groups are skipped entirely (we track nesting and discard until
// matching close brace).

using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Orivy.Controls.RichText.Rtf;

/// <summary>Parses RTF into a StyledTextDocument.</summary>
public sealed class RtfReader
{
    private RtfTokenReader? _tokens;
    private readonly List<string> _fontTable = new();
    private readonly List<SKColor> _colorTable = new();
    private readonly StringBuilder _text = new();
    private readonly List<TextRun> _runs = new();

    // Current style state (mutated as we encounter control words).
    private TextStyle _current = TextStyle.Default;
    private int _currentFontIndex = -1;
    private int _currentColorIndex = -1;
    private int _currentBgColorIndex = -1;
    private int _currentStart;  // start position of current run in _text

    /// <summary>Parse an RTF string into a styled document.</summary>
    public StyledTextDocument Parse(string rtf)
    {
        Reset();
        _tokens = new RtfTokenReader(rtf ?? string.Empty);

        ParseDocument();

        // Flush any pending run.
        FlushRun();

        var doc = new StyledTextDocument();
        doc.Load(_text.ToString(), _runs);
        return doc;
    }

    private void Reset()
    {
        _fontTable.Clear();
        _colorTable.Clear();
        _text.Clear();
        _runs.Clear();
        _current = TextStyle.Default;
        _currentFontIndex = -1;
        _currentColorIndex = -1;
        _currentBgColorIndex = -1;
        _currentStart = 0;
    }

    private void ParseDocument()
    {
        // Expect: {\rtf1 ... }
        var tok = _tokens!.Read();
        if (tok.Kind != RtfTokenKind.GroupStart)
            return;  // not valid RTF

        tok = _tokens.Read();
        if (tok.Kind != RtfTokenKind.ControlWord || tok.Control != "rtf")
        {
            _tokens.UnGet(tok);
            ParseGroup();
            return;
        }

        // Consume the rest of the top-level group.
        ParseGroupBody();
    }

    /// <summary>Parse a group's body until the matching closing brace.</summary>
    private void ParseGroup()
    {
        ParseGroupBody();
    }

    private void ParseGroupBody()
    {
        while (true)
        {
            var tok = _tokens!.Read();
            switch (tok.Kind)
            {
                case RtfTokenKind.Eof:
                    return;
                case RtfTokenKind.GroupEnd:
                    return;
                case RtfTokenKind.GroupStart:
                    ParseGroup();
                    break;
                case RtfTokenKind.ControlWord:
                    HandleControlWord(tok);
                    break;
                case RtfTokenKind.ControlSymbol:
                    HandleControlSymbol(tok);
                    break;
                case RtfTokenKind.Text:
                    HandleText(tok.Text!);
                    break;
            }
        }
    }

    private void HandleControlWord(RtfToken tok)
    {
        var name = tok.Control!;
        switch (name)
        {
            // Header — ignore for parsing.
            case "rtf":
            case "ansi":
            case "mac":
            case "pc":
            case "pca":
            case "deff":
                return;

            // Font table group: \fonttbl is followed by sub-groups describing
            // each font. We'll detect it and switch to a special parse mode.
            case "fonttbl":
                ParseFontTableGroup();
                return;

            // Color table group: \colortbl followed by a sequence of color
            // entries separated by semicolons.
            case "colortbl":
                ParseColorTableGroup();
                return;

            // Style sheet, info, etc. — skip the whole group.
            case "stylesheet":
            case "info":
            case "header":
            case "footer":
                SkipCurrentGroupContinuation();
                return;

            // Character formatting.
            case "b":
                _current = _current.With(bold: tok.HasParam ? tok.Param == 0 ? false : true : true);
                FlushRun();
                return;
            case "i":
                _current = _current.With(italic: tok.HasParam ? tok.Param == 0 ? false : true : true);
                FlushRun();
                return;
            case "ul":
            case "ulw":
            case "uld":
            case "uldb":
                _current = _current.With(underline: !tok.HasParam || tok.Param != 0);
                FlushRun();
                return;
            case "ulnone":
                _current = _current.With(underline: false);
                FlushRun();
                return;
            case "strike":
                _current = _current.With(strikethrough: !tok.HasParam || tok.Param != 0);
                FlushRun();
                return;
            case "sub":
                _current = _current.With(verticalAlign: TextVerticalAlign.Subscript);
                FlushRun();
                return;
            case "super":
                _current = _current.With(verticalAlign: TextVerticalAlign.Superscript);
                FlushRun();
                return;
            case "nosupersub":
                _current = _current.With(verticalAlign: TextVerticalAlign.Baseline);
                FlushRun();
                return;

            case "fs":
                // Half-points → points → pixels (assume 1pt ≈ 1.33px @ 96dpi).
                if (tok.HasParam && tok.Param.HasValue)
                {
                    var points = tok.Param.Value / 2f;
                    _current = _current.With(fontSize: points * 1.33f);
                    FlushRun();
                }
                return;

            case "f":
                if (tok.HasParam && tok.Param.HasValue)
                    _currentFontIndex = tok.Param.Value;
                return;

            case "cf":
                if (tok.HasParam && tok.Param.HasValue)
                {
                    _currentColorIndex = tok.Param.Value;
                    UpdateCurrentColor();
                }
                return;

            case "cb":
            case "highlight":
                if (tok.HasParam && tok.Param.HasValue)
                {
                    _currentBgColorIndex = tok.Param.Value;
                    UpdateCurrentColor();
                }
                return;

            case "par":
            case "line":
                HandleText("\n");
                return;

            case "tab":
                HandleText("\t");
                return;

            case "page":
                HandleText("\n\n");
                return;

            case "u":
                // \uN — Unicode character. N is signed 16-bit.
                if (tok.HasParam && tok.Param.HasValue)
                {
                    var code = (ushort)tok.Param.Value;
                    HandleText(new string((char)code, 1));
                    // Skip the next alternate char(s) — usually a '?' or \'HH.
                    // RTF spec: \ucN controls how many alternate chars follow.
                    // For simplicity we skip a single following text token if it's "?"
                    // (most generators emit a single '?' as fallback).
                    var next = _tokens!.Peek();
                    if (next.Kind == RtfTokenKind.Text && next.Text == "?")
                        _tokens.Read();
                }
                return;

            case "uc":
                // Unicode count — ignored, we just skip 1 alt char.
                return;

            case "plain":
                // Reset character formatting to defaults.
                _current = TextStyle.Default;
                _currentFontIndex = -1;
                _currentColorIndex = -1;
                _currentBgColorIndex = -1;
                FlushRun();
                return;

            case "pard":
                // Reset paragraph formatting — we don't track paragraph formatting,
                // so this is a no-op.
                return;

            // Ignored paragraph / section properties.
            case "ql":
            case "qr":
            case "qc":
            case "qj":
            case "fi":
            case "li":
            case "ri":
            case "sl":
            case "slmult":
            case "sa":
            case "sb":
            case "brdr":
            case "intbl":
            case "trowd":
            case "cell":
            case "row":
            case "trql":
            case "trqr":
            case "trqc":
                return;

            default:
                // Unknown control word — skip (parameter already consumed).
                return;
        }
    }

    private void HandleControlSymbol(RtfToken tok)
    {
        var sym = tok.Control!;
        switch (sym)
        {
            case "\\":
                HandleText("\\");
                return;
            case "{":
                HandleText("{");
                return;
            case "}":
                HandleText("}");
                return;
            case "_":
                HandleText("-");  // non-breaking hyphen → simple hyphen
                return;
            case "-":
                HandleText("-");  // optional hyphen
                return;
            case "~":
                HandleText(" ");  // non-breaking space → regular space
                return;
            case "*":
                // Ignorable group destination — skip the rest of the group.
                SkipCurrentGroupContinuation();
                return;
            case "'":
                // \'HH — hex byte. We've already consumed the backslash and the
                // apostrophe; now read two hex digits.
                ReadHexByteAndAppend();
                return;
            default:
                return;
        }
    }

    private void ReadHexByteAndAppend()
    {
        // Read two hex digits from the source. The tokenizer already consumed
        // \\' — we need to peek into the raw source. Since RtfTokenReader
        // doesn't expose raw pos, we approximate by reading one Text token
        // and taking its first two chars.
        // WORKAROUND: Re-read using a new approach — read next token, expect
        // a Text token starting with two hex digits.
        var tok = _tokens!.Read();
        if (tok.Kind == RtfTokenKind.Text && tok.Text!.Length >= 2)
        {
            var hex = tok.Text.Substring(0, 2);
            if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture, out var b))
            {
                // Decode as Latin-1 (RTF default code page; \ansicpg handled by Unicode chars normally).
                HandleText(((char)b).ToString());
            }
            // Re-inject the remainder of the text token.
            if (tok.Text.Length > 2)
            {
                _tokens.UnGet(new RtfToken(RtfTokenKind.Text, text: tok.Text[2..]));
            }
        }
    }

    private void HandleText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        _text.Append(text);
    }

    private void FlushRun()
    {
        if (_text.Length > _currentStart)
        {
            var len = _text.Length - _currentStart;
            _runs.Add(new TextRun(_currentStart, len, _current));
        }
        _currentStart = _text.Length;
    }

    private void UpdateCurrentColor()
    {
        SKColor? fore = null;
        SKColor? back = null;
        if (_currentColorIndex >= 0 && _currentColorIndex < _colorTable.Count)
            fore = _colorTable[_currentColorIndex];
        if (_currentBgColorIndex >= 0 && _currentBgColorIndex < _colorTable.Count)
            back = _colorTable[_currentBgColorIndex];
        _current = _current.With(foreColor: fore, backColor: back);
        FlushRun();
    }

    // ── Font / color table parsing ─────────────────────────────────────

    private void ParseFontTableGroup()
    {
        // We're inside the fonttbl group. Read tokens until the matching '}'.
        // Each font entry is a sub-group like {\f0\fnil\fcharset0 Times New Roman;}
        var depth = 1;
        var currentFontIdx = -1;
        var currentFontName = new StringBuilder();

        while (depth > 0)
        {
            var tok = _tokens!.Read();
            switch (tok.Kind)
            {
                case RtfTokenKind.Eof:
                    return;
                case RtfTokenKind.GroupStart:
                    depth++;
                    currentFontName.Clear();
                    currentFontIdx = -1;
                    break;
                case RtfTokenKind.GroupEnd:
                    depth--;
                    if (depth == 0) return;
                    // End of a font entry sub-group: record it.
                    if (currentFontIdx >= 0 && currentFontName.Length > 0)
                    {
                        while (_fontTable.Count <= currentFontIdx)
                            _fontTable.Add(string.Empty);
                        var name = currentFontName.ToString().TrimEnd(';', ' ');
                        _fontTable[currentFontIdx] = name;
                    }
                    break;
                case RtfTokenKind.ControlWord when tok.Control == "f":
                    if (tok.HasParam) currentFontIdx = tok.Param!.Value;
                    break;
                case RtfTokenKind.Text:
                    currentFontName.Append(tok.Text);
                    break;
                case RtfTokenKind.ControlSymbol when tok.Control == ";":
                    // Font entry separator.
                    if (currentFontIdx >= 0 && currentFontName.Length > 0)
                    {
                        while (_fontTable.Count <= currentFontIdx)
                            _fontTable.Add(string.Empty);
                        var name = currentFontName.ToString().TrimEnd(';', ' ');
                        _fontTable[currentFontIdx] = name;
                        currentFontName.Clear();
                    }
                    break;
            }
        }
    }

    private void ParseColorTableGroup()
    {
        // Each color entry is "\red255\green0\blue0;" — terminated by ';'.
        // The first entry (index 0) is "auto" (no colors specified, just ';').
        byte r = 0, g = 0, b = 0;
        var hasColor = false;

        while (true)
        {
            var tok = _tokens!.Read();
            switch (tok.Kind)
            {
                case RtfTokenKind.Eof:
                    return;
                case RtfTokenKind.GroupEnd:
                    return;
                case RtfTokenKind.ControlWord:
                    switch (tok.Control)
                    {
                        case "red" when tok.HasParam: r = (byte)tok.Param!.Value; hasColor = true; break;
                        case "green" when tok.HasParam: g = (byte)tok.Param!.Value; hasColor = true; break;
                        case "blue" when tok.HasParam: b = (byte)tok.Param!.Value; hasColor = true; break;
                    }
                    break;
                case RtfTokenKind.ControlSymbol when tok.Control == ";":
                    if (hasColor)
                        _colorTable.Add(new SKColor(r, g, b));
                    else
                        _colorTable.Add(SKColor.Empty);  // "auto" color
                    hasColor = false;
                    r = g = b = 0;
                    break;
            }
        }
    }

    private void SkipCurrentGroupContinuation()
    {
        // We've just read a control word like \info that introduces a group.
        // Skip until matching '}'.
        var depth = 1;
        while (depth > 0)
        {
            var tok = _tokens!.Read();
            switch (tok.Kind)
            {
                case RtfTokenKind.Eof:
                    return;
                case RtfTokenKind.GroupStart:
                    depth++;
                    break;
                case RtfTokenKind.GroupEnd:
                    depth--;
                    break;
            }
        }
    }
}
