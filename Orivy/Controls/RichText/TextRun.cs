// SPDX-License-Identifier: MIT
// Orivy RichText — TextRun
//
// A run is a contiguous text range with a single style. Runs are non-overlapping
// and cover the entire text of the document (gaps are filled with Default-styled
// runs). Runs are immutable; StyledTextDocument manages a sorted list of them.

using System;

namespace Orivy.Controls.RichText;

/// <summary>
/// A contiguous text range with a single TextStyle. Immutable.
/// </summary>
public readonly struct TextRun
{
    public TextRun(int start, int length, TextStyle style)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        Start = start;
        Length = length;
        Style = style;
    }

    /// <summary>Inclusive start index into the document text.</summary>
    public int Start { get; }

    /// <summary>Number of characters covered.</summary>
    public int Length { get; }

    /// <summary>Style applied to this range.</summary>
    public TextStyle Style { get; }

    /// <summary>Exclusive end index (= Start + Length).</summary>
    public int End => Start + Length;

    /// <summary>True if the run is empty (zero-length).</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>Returns a copy with a new start offset.</summary>
    public TextRun WithStart(int newStart) => new(newStart, Length, Style);

    /// <summary>Returns a copy with a new length.</summary>
    public TextRun WithLength(int newLength) => new(Start, newLength, Style);

    /// <summary>Returns a copy with a new style.</summary>
    public TextRun WithStyle(TextStyle newStyle) => new(Start, Length, newStyle);

    /// <summary>True if this run contains the given character index.</summary>
    public bool Contains(int charIndex) => charIndex >= Start && charIndex < End;

    /// <summary>True if this run overlaps the given range.</summary>
    public bool Overlaps(int rangeStart, int rangeLength)
    {
        var rangeEnd = rangeStart + rangeLength;
        return Start < rangeEnd && End > rangeStart;
    }

    public override string ToString() => $"[{Start}..{End}) {Style}";
}
