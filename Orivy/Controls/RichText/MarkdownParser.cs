// SPDX-License-Identifier: MIT
// Orivy RichText — Markdown Parser (CommonMark subset)
//
// Single-pass line-based block parser + recursive inline parser. Supports:
//   - Headings (ATX # style, levels 1-6)
//   - Paragraphs
//   - Fenced code blocks (``` and ~~~)
//   - Blockquotes (>)
//   - Unordered lists (-, *, +)
//   - Ordered lists (1., 2., ...)
//   - Task list items ([ ], [x])
//   - Horizontal rules (---, ***, ___)
//   - Tables (GFM style with | separators and --- alignment row)
//   - Inline: bold (**, __), italic (*, _), strikethrough (~~), code (`),
//             links [text](url), images ![alt](url), hard/soft breaks
//
// This is NOT a full CommonMark implementation — no setext headings, no
// reference links, no HTML inline, no link reference definitions. The goal
// is "good enough for rich text editing" with predictable behavior.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Orivy.Controls.RichText.Markdown;

public sealed class MarkdownParser
{
    private string _source = string.Empty;
    private string[] _lines = System.Array.Empty<string>();
    private int _lineIndex;

    /// <summary>Parse a markdown source string into an AST.</summary>
    public MarkdownDocument Parse(string source)
    {
        _source = source ?? string.Empty;
        _lines = SplitLines(_source);
        _lineIndex = 0;

        var doc = new MarkdownDocument();
        while (_lineIndex < _lines.Length)
        {
            var block = ParseBlock();
            if (block != null)
                doc.Blocks.Add(block);
        }
        return doc;
    }

    // ── Line splitting ─────────────────────────────────────────────────

    private static string[] SplitLines(string source)
    {
        // Normalize line endings, split on \n.
        var normalized = source.Replace("\r\n", "\n", System.StringComparison.Ordinal)
                               .Replace('\r', '\n');
        return normalized.Split('\n');
    }

    // ── Block parsing ──────────────────────────────────────────────────

    private MarkdownBlock? ParseBlock()
    {
        var line = CurrentLine();

        // Skip blank lines between blocks.
        if (string.IsNullOrWhiteSpace(line))
        {
            _lineIndex++;
            return null;
        }

        // Fenced code block.
        if (TryParseFencedCode(out var codeBlock))
            return codeBlock;

        // ATX heading.
        if (TryParseHeading(out var heading))
            return heading;

        // Horizontal rule.
        if (TryParseHorizontalRule())
            return new HorizontalRuleBlock();

        // Blockquote.
        if (TryParseBlockquote(out var bq))
            return bq;

        // Table.
        if (TryParseTable(out var table))
            return table;

        // List (ordered or unordered).
        if (TryParseList(out var list))
            return list;

        // Default: paragraph.
        return ParseParagraph();
    }

    private string CurrentLine() => _lineIndex < _lines.Length ? _lines[_lineIndex] : string.Empty;

    private bool TryParseFencedCode(out CodeBlock? block)
    {
        block = null;
        var line = CurrentLine();
        var trimmed = line.TrimStart();

        char fenceChar;
        if (trimmed.StartsWith("```", System.StringComparison.Ordinal))
            fenceChar = '`';
        else if (trimmed.StartsWith("~~~", System.StringComparison.Ordinal))
            fenceChar = '~';
        else
            return false;

        // Count fence length.
        var fenceLen = 0;
        while (fenceLen < trimmed.Length && trimmed[fenceLen] == fenceChar)
            fenceLen++;
        if (fenceLen < 3)
            return false;

        var language = trimmed[fenceLen..].Trim();
        _lineIndex++;

        var sb = new StringBuilder();
        while (_lineIndex < _lines.Length)
        {
            var cur = _lines[_lineIndex];
            var curTrim = cur.TrimStart();
            if (curTrim.StartsWith(new string(fenceChar, fenceLen), System.StringComparison.Ordinal)
                && curTrim.All(c => c == fenceChar))
            {
                _lineIndex++;
                break;
            }
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(cur);
            _lineIndex++;
        }

        block = new CodeBlock(language, sb.ToString());
        return true;
    }

    private bool TryParseHeading(out HeadingBlock? heading)
    {
        heading = null;
        var line = CurrentLine();
        var trimmed = line.TrimStart();

        var level = 0;
        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
            level++;
        if (level == 0 || level >= trimmed.Length || trimmed[level] != ' ')
            return false;

        var content = trimmed[(level + 1)..].TrimEnd();
        // Strip trailing # sequence (closing ATX).
        if (content.Length > 0 && content[^1] == '#')
        {
            var i = content.Length - 1;
            while (i > 0 && content[i] == '#') i--;
            if (i >= 0 && content[i] == ' ')
                content = content[..i].TrimEnd();
        }

        _lineIndex++;
        var inlines = ParseInlines(content);
        heading = new HeadingBlock(level, inlines);
        return true;
    }

    private bool TryParseHorizontalRule()
    {
        var line = CurrentLine();
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
            return false;

        var ch = trimmed[0];
        if (ch != '-' && ch != '*' && ch != '_')
            return false;

        var count = 0;
        foreach (var c in trimmed)
        {
            if (c == ch) count++;
            else if (c != ' ' && c != '\t')
                return false;
        }

        if (count < 3)
            return false;

        _lineIndex++;
        return true;
    }

    private bool TryParseBlockquote(out BlockquoteBlock? block)
    {
        block = null;
        var line = CurrentLine();
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('>') || (trimmed.Length > 1 && trimmed[1] != ' ' && trimmed[1] != '\t' && trimmed.Length != 1))
            return false;
        if (!trimmed.StartsWith("> ", System.StringComparison.Ordinal) && trimmed != ">")
            return false;

        // Collect all consecutive blockquote lines, strip leading "> ".
        var content = new StringBuilder();
        while (_lineIndex < _lines.Length)
        {
            var cur = _lines[_lineIndex].TrimStart();
            if (cur == ">" || cur.StartsWith("> ", System.StringComparison.Ordinal))
            {
                if (content.Length > 0)
                    content.Append('\n');
                content.Append(cur == ">" ? string.Empty : cur[2..]);
                _lineIndex++;
            }
            else if (string.IsNullOrWhiteSpace(cur))
            {
                break;
            }
            else
            {
                break;
            }
        }

        // Parse the inner content recursively.
        var innerParser = new MarkdownParser();
        var innerDoc = innerParser.Parse(content.ToString());
        block = new BlockquoteBlock(innerDoc.Blocks);
        return true;
    }

    private bool TryParseList(out MarkdownBlock? list)
    {
        list = null;
        var line = CurrentLine();
        var trimmed = line.TrimStart();

        // Unordered?
        if (trimmed.Length >= 2 && IsBulletChar(trimmed[0]) && trimmed[1] == ' ')
        {
            return TryParseUnorderedList(out list);
        }

        // Ordered?
        var i = 0;
        while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
        if (i > 0 && i + 1 < trimmed.Length && (trimmed[i] == '.' || trimmed[i] == ')') && trimmed[i + 1] == ' ')
        {
            var start = int.Parse(trimmed[..i], System.Globalization.CultureInfo.InvariantCulture);
            return TryParseOrderedList(start, out list);
        }

        return false;
    }

    private static bool IsBulletChar(char c) => c == '-' || c == '*' || c == '+';

    private bool TryParseUnorderedList(out MarkdownBlock? list)
    {
        list = null;
        var items = new List<ListItem>();

        while (_lineIndex < _lines.Length)
        {
            var line = _lines[_lineIndex];
            var trimmed = line.TrimStart();
            if (trimmed.Length < 2 || !IsBulletChar(trimmed[0]) || trimmed[1] != ' ')
                break;

            // Task list?
            var rest = trimmed[2..];
            bool isTask = false, taskChecked = false;
            if (rest.StartsWith("[ ] ", System.StringComparison.Ordinal))
            {
                isTask = true; taskChecked = false; rest = rest[4..];
            }
            else if (rest.StartsWith("[x] ", System.StringComparison.OrdinalIgnoreCase)
                     || rest.StartsWith("[X] ", System.StringComparison.Ordinal))
            {
                isTask = true; taskChecked = true; rest = rest[4..];
            }

            _lineIndex++;
            var itemLines = new List<string> { rest };

            // Continuation: indented lines (4 spaces or tab) belong to this item.
            while (_lineIndex < _lines.Length)
            {
                var cont = _lines[_lineIndex];
                if (cont.StartsWith("    ", System.StringComparison.Ordinal) || cont.StartsWith('\t'))
                {
                    itemLines.Add(cont.TrimStart(' ', '\t'));
                    _lineIndex++;
                }
                else break;
            }

            var innerParser = new MarkdownParser();
            var innerDoc = innerParser.Parse(string.Join('\n', itemLines));
            items.Add(new ListItem(innerDoc.Blocks, isTask, taskChecked));
        }

        if (items.Count == 0)
            return false;

        list = new UnorderedListBlock(items);
        return true;
    }

    private bool TryParseOrderedList(int start, out MarkdownBlock? list)
    {
        list = null;
        var items = new List<ListItem>();

        while (_lineIndex < _lines.Length)
        {
            var line = _lines[_lineIndex];
            var trimmed = line.TrimStart();

            var i = 0;
            while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
            if (i == 0 || i + 1 >= trimmed.Length || (trimmed[i] != '.' && trimmed[i] != ')') || trimmed[i + 1] != ' ')
                break;

            var rest = trimmed[(i + 2)..];
            _lineIndex++;
            var itemLines = new List<string> { rest };

            while (_lineIndex < _lines.Length)
            {
                var cont = _lines[_lineIndex];
                if (cont.StartsWith("    ", System.StringComparison.Ordinal) || cont.StartsWith('\t'))
                {
                    itemLines.Add(cont.TrimStart(' ', '\t'));
                    _lineIndex++;
                }
                else break;
            }

            var innerParser = new MarkdownParser();
            var innerDoc = innerParser.Parse(string.Join('\n', itemLines));
            items.Add(new ListItem(innerDoc.Blocks));
        }

        if (items.Count == 0)
            return false;

        list = new OrderedListBlock(items, start);
        return true;
    }

    private bool TryParseTable(out TableBlock? table)
    {
        table = null;
        if (_lineIndex + 1 >= _lines.Length)
            return false;

        var headerLine = _lines[_lineIndex];
        var alignLine = _lines[_lineIndex + 1];

        if (!headerLine.Contains('|') || !alignLine.Contains('|'))
            return false;
        if (!IsTableAlignmentRow(alignLine, out var alignments))
            return false;

        var headerCells = SplitTableRow(headerLine);
        if (headerCells.Count != alignments.Count)
            return false;

        _lineIndex += 2;

        var headerInlines = new List<List<MarkdownInline>>();
        foreach (var cell in headerCells)
            headerInlines.Add(ParseInlines(cell.Trim()));

        var body = new List<List<List<MarkdownInline>>>();
        while (_lineIndex < _lines.Length)
        {
            var line = _lines[_lineIndex];
            if (string.IsNullOrWhiteSpace(line) || !line.Contains('|'))
                break;

            var cells = SplitTableRow(line);
            var row = new List<List<MarkdownInline>>();
            for (var i = 0; i < alignments.Count; i++)
                row.Add(ParseInlines(i < cells.Count ? cells[i].Trim() : string.Empty));
            body.Add(row);
            _lineIndex++;
        }

        table = new TableBlock(headerInlines, body, alignments);
        return true;
    }

    private static bool IsTableAlignmentRow(string line, out List<TextAlign> alignments)
    {
        alignments = new List<TextAlign>();
        var cells = SplitTableRow(line);
        foreach (var cell in cells)
        {
            var c = cell.Trim();
            if (string.IsNullOrEmpty(c))
                return false;
            if (!c.All(ch => ch == '-' || ch == ':'))
                return false;
            if (c.StartsWith(':') && c.EndsWith(':'))
                alignments.Add(TextAlign.Center);
            else if (c.EndsWith(':'))
                alignments.Add(TextAlign.Right);
            else
                alignments.Add(TextAlign.Left);
        }
        return alignments.Count > 0;
    }

    private static List<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return new List<string>(trimmed.Split('|'));
    }

    private ParagraphBlock ParseParagraph()
    {
        var sb = new StringBuilder();
        var first = true;
        while (_lineIndex < _lines.Length)
        {
            var line = _lines[_lineIndex];
            if (string.IsNullOrWhiteSpace(line))
                break;
            // Stop if the line starts a new block.
            if (!first && StartsWithBlockMarker(line))
                break;
            if (!first)
                sb.Append('\n');
            sb.Append(line.TrimEnd());
            first = false;
            _lineIndex++;
        }
        var inlines = ParseInlines(sb.ToString());
        return new ParagraphBlock(inlines);
    }

    private static bool StartsWithBlockMarker(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#')) return true;
        if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~")) return true;
        if (trimmed.StartsWith('>') && (trimmed.Length == 1 || trimmed[1] == ' ')) return true;
        if (IsBulletChar(trimmed.Length > 0 ? trimmed[0] : '\0') && trimmed.Length > 1 && trimmed[1] == ' ') return true;
        if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
        {
            var i = 0;
            while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
            if (i + 1 < trimmed.Length && (trimmed[i] == '.' || trimmed[i] == ')') && trimmed[i + 1] == ' ')
                return true;
        }
        return false;
    }

    // ── Inline parsing ─────────────────────────────────────────────────

    private List<MarkdownInline> ParseInlines(string text)
    {
        var result = new List<MarkdownInline>();
        var pos = 0;
        var sb = new StringBuilder();

        while (pos < text.Length)
        {
            var ch = text[pos];

            // Hard break: backslash + newline.
            if (ch == '\\' && pos + 1 < text.Length && text[pos + 1] == '\n')
            {
                FlushText(result, sb);
                result.Add(new LineBreakInline());
                pos += 2;
                continue;
            }

            // Soft break: newline.
            if (ch == '\n')
            {
                FlushText(result, sb);
                result.Add(new SoftBreakInline());
                pos++;
                continue;
            }

            // Escape: backslash + punctuation → literal char.
            if (ch == '\\' && pos + 1 < text.Length && IsPunctuation(text[pos + 1]))
            {
                sb.Append(text[pos + 1]);
                pos += 2;
                continue;
            }

            // Image: ![alt](url)
            if (ch == '!' && pos + 1 < text.Length && text[pos + 1] == '[')
            {
                if (TryParseLink(text, pos + 1, out var linkText, out var url, out var consumed))
                {
                    FlushText(result, sb);
                    result.Add(new ImageInline(linkText, url));
                    pos += consumed + 1;  // +1 for the leading '!'
                    continue;
                }
            }

            // Link: [text](url)
            if (ch == '[')
            {
                if (TryParseLink(text, pos, out var linkText, out var url, out var consumed))
                {
                    FlushText(result, sb);
                    var linkInlines = ParseInlines(linkText);
                    result.Add(new LinkInline(linkInlines, url));
                    pos += consumed;
                    continue;
                }
            }

            // Inline code: `code` (single backtick) or ``code`` (double).
            if (ch == '`')
            {
                if (TryParseInlineCode(text, pos, out var code, out var consumed))
                {
                    FlushText(result, sb);
                    result.Add(new CodeInline(code));
                    pos += consumed;
                    continue;
                }
            }

            // Bold: **text** or __text__
            if ((ch == '*' || ch == '_') && pos + 1 < text.Length && text[pos + 1] == ch)
            {
                if (TryParseEnclosed(text, pos, ch, 2, out var inner, out var consumed))
                {
                    FlushText(result, sb);
                    result.Add(new BoldInline(ParseInlines(inner)));
                    pos += consumed;
                    continue;
                }
            }

            // Italic: *text* or _text_ (single).
            if (ch == '*' || ch == '_')
            {
                if (TryParseEnclosed(text, pos, ch, 1, out var inner, out var consumed))
                {
                    FlushText(result, sb);
                    result.Add(new ItalicInline(ParseInlines(inner)));
                    pos += consumed;
                    continue;
                }
            }

            // Strikethrough: ~~text~~
            if (ch == '~' && pos + 1 < text.Length && text[pos + 1] == '~')
            {
                if (TryParseEnclosed(text, pos, '~', 2, out var inner, out var consumed))
                {
                    FlushText(result, sb);
                    result.Add(new StrikethroughInline(ParseInlines(inner)));
                    pos += consumed;
                    continue;
                }
            }

            sb.Append(ch);
            pos++;
        }

        FlushText(result, sb);
        return result;
    }

    private static void FlushText(List<MarkdownInline> result, StringBuilder sb)
    {
        if (sb.Length > 0)
        {
            result.Add(new TextInline(sb.ToString()));
            sb.Clear();
        }
    }

    private static bool IsPunctuation(char c)
    {
        return "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".IndexOf(c) >= 0;
    }

    /// <summary>Parse **inner** or __inner__ etc. markerLen = 1 (italic) or 2 (bold/strike).</summary>
    private static bool TryParseEnclosed(string text, int pos, char marker, int markerLen,
                                          out string inner, out int consumed)
    {
        inner = string.Empty;
        consumed = 0;

        // Need opening marker + content + closing marker.
        if (pos + markerLen >= text.Length)
            return false;

        // Skip the opening marker.
        var start = pos + markerLen;
        var endPos = -1;
        for (var i = start; i + markerLen <= text.Length; i++)
        {
            if (text[i] != marker) continue;
            // Check that all markerLen chars are the marker.
            var ok = true;
            for (var j = 1; j < markerLen; j++)
            {
                if (text[i + j] != marker) { ok = false; break; }
            }
            if (!ok) continue;
            // Inner must not be empty.
            if (i == start) return false;
            endPos = i;
            break;
        }

        if (endPos < 0)
            return false;

        inner = text[start..endPos];
        consumed = (endPos + markerLen) - pos;
        return true;
    }

    /// <summary>Parse [text](url) starting at pos. Returns consumed chars including all.</summary>
    private static bool TryParseLink(string text, int pos, out string linkText, out string url, out int consumed)
    {
        linkText = string.Empty;
        url = string.Empty;
        consumed = 0;

        if (pos >= text.Length || text[pos] != '[')
            return false;

        // Find matching ']'.
        var depth = 1;
        var i = pos + 1;
        while (i < text.Length && depth > 0)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']') depth--;
            if (depth == 0) break;
            i++;
        }
        if (depth != 0 || i + 1 >= text.Length || text[i + 1] != '(')
            return false;

        linkText = text[(pos + 1)..i];

        // Find closing ')'.
        var urlStart = i + 2;
        var urlEnd = text.IndexOf(')', urlStart);
        if (urlEnd < 0)
            return false;

        url = text[urlStart..urlEnd];
        consumed = (urlEnd + 1) - pos;
        return true;
    }

    /// <summary>Parse `code` starting at pos.</summary>
    private static bool TryParseInlineCode(string text, int pos, out string code, out int consumed)
    {
        code = string.Empty;
        consumed = 0;

        // Count opening backticks.
        var tickCount = 0;
        while (pos + tickCount < text.Length && text[pos + tickCount] == '`')
            tickCount++;
        if (tickCount == 0)
            return false;

        var start = pos + tickCount;
        // Find a matching run of the same length.
        for (var i = start; i + tickCount <= text.Length; i++)
        {
            if (text[i] != '`') continue;
            var ok = true;
            for (var j = 1; j < tickCount; j++)
            {
                if (text[i + j] != '`') { ok = false; break; }
            }
            if (!ok) continue;
            if (i == start) return false;
            code = text[start..i];
            consumed = (i + tickCount) - pos;
            return true;
        }
        return false;
    }
}
