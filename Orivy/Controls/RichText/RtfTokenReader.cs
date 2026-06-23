// SPDX-License-Identifier: MIT
// Orivy RichText — RTF Token Reader
//
// RTF (Rich Text Format) is a token-based format. Tokens are:
//   - Control words: \word   (optionally followed by a numeric parameter)
//   - Control symbols: \X    (single non-letter char after backslash)
//   - Groups: { ... }
//   - Text: anything else, with \\ \{ \} as escapes
//
// This is a low-level tokenizer; RtfReader consumes it to build a
// StyledTextDocument. Designed to be resilient — unknown control words are
// skipped (with optional parameter) per the RTF spec.

using System;
using System.Collections.Generic;

namespace Orivy.Controls.RichText.Rtf;

/// <summary>An RTF token: either a group start/end, a control word/symbol
/// (with optional numeric param), or a text fragment.</summary>
public readonly struct RtfToken
{
    public RtfToken(RtfTokenKind kind, string? text = null, string? control = null, int? param = null, bool hasParam = false)
    {
        Kind = kind;
        Text = text;
        Control = control;
        Param = param;
        HasParam = hasParam;
    }

    public RtfTokenKind Kind { get; }
    public string? Text { get; }         // for Text tokens
    public string? Control { get; }      // for ControlWord / ControlSymbol
    public int? Param { get; }           // optional numeric parameter
    public bool HasParam { get; }

    public override string ToString()
    {
        return Kind switch
        {
            RtfTokenKind.GroupStart => "{",
            RtfTokenKind.GroupEnd => "}",
            RtfTokenKind.ControlWord => $"\\{Control}{(HasParam ? Param.ToString() : "")}",
            RtfTokenKind.ControlSymbol => $"\\{Control}",
            RtfTokenKind.Text => $"\"{Text}\"",
            RtfTokenKind.Eof => "<eof>",
            _ => $"<{Kind}>",
        };
    }
}

public enum RtfTokenKind
{
    GroupStart,
    GroupEnd,
    ControlWord,
    ControlSymbol,
    Text,
    Eof,
}

/// <summary>Low-level RTF tokenizer. Streams tokens one at a time.</summary>
public sealed class RtfTokenReader
{
    private readonly string _source;
    private int _pos;
    private readonly List<RtfToken> _pending = new();  // for UnGet

    public RtfTokenReader(string source)
    {
        _source = source ?? string.Empty;
        _pos = 0;
    }

    /// <summary>Read the next token, or return Eof.</summary>
    public RtfToken Read()
    {
        if (_pending.Count > 0)
        {
            var t = _pending[^1];
            _pending.RemoveAt(_pending.Count - 1);
            return t;
        }

        SkipWhitespace();

        if (_pos >= _source.Length)
            return new RtfToken(RtfTokenKind.Eof);

        var ch = _source[_pos];
        switch (ch)
        {
            case '{':
                _pos++;
                return new RtfToken(RtfTokenKind.GroupStart);
            case '}':
                _pos++;
                return new RtfToken(RtfTokenKind.GroupEnd);
            case '\\':
                return ReadControl();
            default:
                return ReadText();
        }
    }

    /// <summary>Push a token back; it will be returned by the next Read().</summary>
    public void UnGet(RtfToken t) => _pending.Add(t);

    /// <summary>Peek the next token without consuming it.</summary>
    public RtfToken Peek()
    {
        var t = Read();
        UnGet(t);
        return t;
    }

    private void SkipWhitespace()
    {
        // RTF treats spaces/tabs after control words as separators (consumed by
        // ReadControl). Whitespace OUTSIDE control words is significant as text.
        // Here we only skip leading whitespace if we're at the start of a token.
        // We don't actually skip — text reader will handle spaces. But CR/LF
        // outside text are insignificant in RTF.
        while (_pos < _source.Length && (_source[_pos] == '\r' || _source[_pos] == '\n'))
            _pos++;
    }

    private RtfToken ReadControl()
    {
        // We're at '\'. Consume it.
        _pos++;
        if (_pos >= _source.Length)
            return new RtfToken(RtfTokenKind.ControlSymbol, control: "\\");

        var ch = _source[_pos];
        if (char.IsLetter(ch))
        {
            // Control word: read letters, optional digits, optional space.
            var start = _pos;
            while (_pos < _source.Length && char.IsLetter(_source[_pos]))
                _pos++;
            var word = _source[start.._pos];

            // Optional numeric parameter.
            int? param = null;
            bool hasParam = false;
            if (_pos < _source.Length && (_source[_pos] == '-' || char.IsDigit(_source[_pos])))
            {
                hasParam = true;
                var neg = _source[_pos] == '-';
                if (neg) _pos++;
                var numStart = _pos;
                while (_pos < _source.Length && char.IsDigit(_source[_pos]))
                    _pos++;
                if (int.TryParse(_source.AsSpan(numStart, _pos - numStart), out var p))
                    param = neg ? -p : p;
            }

            // Optional single space terminator (consumed).
            if (_pos < _source.Length && _source[_pos] == ' ')
                _pos++;

            return new RtfToken(RtfTokenKind.ControlWord, control: word, param: param, hasParam: hasParam);
        }
        else
        {
            // Control symbol: single char.
            _pos++;

            // Special case: \uN for Unicode character (followed by digits).
            if (ch == 'u')
            {
                // Actually \u is a control word but we already handled letters above.
                // Should not reach here.
            }

            return new RtfToken(RtfTokenKind.ControlSymbol, control: ch.ToString());
        }
    }

    private RtfToken ReadText()
    {
        // Read until we hit a control char or group delimiter.
        var start = _pos;
        var sb = new System.Text.StringBuilder();
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '{' || c == '}' || c == '\\')
                break;
            if (c == '\r' || c == '\n')
            {
                _pos++;
                continue;
            }
            sb.Append(c);
            _pos++;
        }
        return new RtfToken(RtfTokenKind.Text, text: sb.ToString());
    }
}
