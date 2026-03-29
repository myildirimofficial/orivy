using SkiaSharp;
using System.Collections.Generic;

namespace Orivy.Helpers;

internal static class TextWrapper
{
    public static List<string> WrapText(string text, SKFont font, float maxWidth, TextWrap wrapMode)
    {
        var lines = new List<string>();
        var normalizedText = NormalizeLineBreaks(text);
        
        if (string.IsNullOrEmpty(normalizedText))
            return lines;

        if (wrapMode == TextWrap.None)
        {
            var paragraphsWithoutWrap = normalizedText.Split('\n');
            for (var i = 0; i < paragraphsWithoutWrap.Length; i++)
                lines.Add(paragraphsWithoutWrap[i]);
            return lines;
        }

        if (maxWidth <= 0)
            return lines;

        var paragraphs = normalizedText.Split('\n');
        
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            if (wrapMode == TextWrap.WordWrap)
                WrapByWords(paragraph, font, maxWidth, lines);
            else
                WrapByCharacters(paragraph, font, maxWidth, lines);
        }

        return lines;
    }

    private static string NormalizeLineBreaks(string text)
    {
        return string.IsNullOrEmpty(text)
            ? text
            : text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static void WrapByWords(string text, SKFont font, float maxWidth, List<string> lines)
    {
        var words = text.Split(' ');
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            var width = font.MeasureText(testLine);

            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);
    }

    private static void WrapByCharacters(string text, SKFont font, float maxWidth, List<string> lines)
    {
        var currentLine = string.Empty;

        foreach (var c in text)
        {
            var testLine = currentLine + c;
            var width = font.MeasureText(testLine);

            if (width > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = c.ToString();
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);
    }
}
