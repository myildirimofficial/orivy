using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Orivy.Controls.Markdown;

/// <summary>
/// A pragmatic block-level markdown parser covering the CommonMark core plus the GFM
/// extensions people actually rely on day to day: fenced code blocks with a language tag,
/// tables, task lists, strikethrough (handled in <see cref="MarkdownInlineParser"/>),
/// autolinks, GitHub-style alert blockquotes ("&gt; [!NOTE]") and a small, safe
/// "&lt;details&gt;&lt;summary&gt;" passthrough.
///
/// It is deliberately forgiving: malformed input never throws, it just degrades to a
/// reasonable best-effort rendering, since this is consumed directly by a UI control that
/// must not crash a host application over a bad paste.
///
/// Known simplifications vs. the CommonMark spec (documented so they're easy to find/fix):
///  - Link reference definitions are only recognized at "block-start" positions (first line,
///    or right after a blank line / another reference definition) -- definitions nested deep
///    inside list items or blockquotes are not extracted.
///  - List item lazy-continuation / loose-vs-tight detection is heuristic, not a full
///    implementation of the CommonMark "list matching" algorithm.
///  - Raw HTML blocks are never executed; only &lt;details&gt;/&lt;summary&gt; is understood
///    structurally, everything else is shown as inert text by <see cref="MarkdownLayoutBuilder"/>.
/// </summary>
public static class MarkdownParser
{
    public static MarkdownDocument Parse(string? source)
    {
        var doc = new MarkdownDocument();
        try
        {
            var lines = Preprocess(source ?? "");
            lines = ExtractLinkReferenceDefinitions(lines, doc);
            var usedSlugs = new HashSet<string>();
            doc.Blocks.AddRange(ParseBlockSequence(lines, doc, usedSlugs));
        }
        catch
        {
            // Never let a parser bug surface as a crash in the host application.
            doc.Blocks.Clear();
            doc.LinkReferences.Clear();
            doc.Outline.Clear();
            doc.Blocks.Add(new ParagraphBlock
            {
                Inlines = new List<MarkdownInline> { new TextInline { Text = source ?? "" } }
            });
        }
        return doc;
    }

    public static string PlainText(List<MarkdownInline> inlines)
    {
        var sb = new StringBuilder();
        foreach (var inline in inlines) AppendPlainText(inline, sb);
        return sb.ToString();
    }

    private static void AppendPlainText(MarkdownInline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case TextInline t: sb.Append(t.Text); break;
            case CodeSpanInline c: sb.Append(c.Code); break;
            case AutoLinkInline a: sb.Append(a.DisplayText); break;
            case ImageInline img: sb.Append(img.AltText); break;
            case LineBreakInline: sb.Append(' '); break;
            case EmphasisInline e: foreach (var ch in e.Children) AppendPlainText(ch, sb); break;
            case StrongInline s: foreach (var ch in s.Children) AppendPlainText(ch, sb); break;
            case StrikethroughInline st: foreach (var ch in st.Children) AppendPlainText(ch, sb); break;
            case LinkInline l: foreach (var ch in l.Children) AppendPlainText(ch, sb); break;
            case InlineHtmlInline ih: foreach (var ch in ih.Children) AppendPlainText(ch, sb); break;
        }
    }

    // ------------------------------------------------------------------
    // Preprocessing
    // ------------------------------------------------------------------
    private static List<string> Preprocess(string source)
    {
        source = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var rawLines = source.Split('\n');
        var result = new List<string>(rawLines.Length);
        foreach (var line in rawLines) result.Add(ExpandTabs(line));
        return result;
    }

    private static string ExpandTabs(string line)
    {
        if (line.IndexOf('\t') < 0) return line;
        var sb = new StringBuilder(line.Length + 8);
        int col = 0;
        foreach (char c in line)
        {
            if (c == '\t')
            {
                int spaces = 4 - (col % 4);
                sb.Append(' ', spaces);
                col += spaces;
            }
            else { sb.Append(c); col++; }
        }
        return sb.ToString();
    }

    private static readonly Regex RefDefRegex = new(
        @"^[ ]{0,3}\[([^\]]+)\]:\s*(?:<([^>]*)>|(\S+))\s*(?:""([^""]*)""|'([^']*)'|\(([^)]*)\))?\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Two-pass strategy: first scan ALL non-code-block lines for ref definitions
    /// (CommonMark spec allows them anywhere in the document, not just at boundaries),
    /// then rebuild the line list with those lines removed.
    /// </summary>
    private static List<string> ExtractLinkReferenceDefinitions(List<string> lines, MarkdownDocument doc)
    {
        bool inFence = false;
        string fenceMarker = "";

        // Pass 1: collect ALL link reference definitions
        foreach (var line in lines)
        {
            string trimmedStart = line.TrimStart();

            if (!inFence && (trimmedStart.StartsWith("```") || trimmedStart.StartsWith("~~~")))
            {
                inFence = true;
                fenceMarker = trimmedStart[..3];
                continue;
            }
            if (inFence)
            {
                if (trimmedStart.StartsWith(fenceMarker, StringComparison.Ordinal)) inFence = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) continue;

            var m = RefDefRegex.Match(line);
            if (m.Success)
            {
                string label = m.Groups[1].Value.Trim();
                string url   = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                string? title = m.Groups[4].Success ? m.Groups[4].Value
                    : m.Groups[5].Success ? m.Groups[5].Value
                    : m.Groups[6].Success ? m.Groups[6].Value : null;
                string key = NormalizeRefLabel(label);
                if (!doc.LinkReferences.ContainsKey(key))
                    doc.LinkReferences[key] = new LinkReferenceDefinition { Label = label, Url = url, Title = title };
            }
        }

        // Pass 2: rebuild line stream without the ref-def lines (reset fence tracking)
        inFence = false; fenceMarker = "";
        var result = new List<string>(lines.Count);

        foreach (var line in lines)
        {
            string trimmedStart = line.TrimStart();

            if (!inFence && (trimmedStart.StartsWith("```") || trimmedStart.StartsWith("~~~")))
            {
                inFence = true;
                fenceMarker = trimmedStart[..3];
                result.Add(line);
                continue;
            }
            if (inFence)
            {
                result.Add(line);
                if (trimmedStart.StartsWith(fenceMarker, StringComparison.Ordinal)) inFence = false;
                continue;
            }

            // Drop standalone ref-def lines from the block stream
            if (!string.IsNullOrWhiteSpace(line) && RefDefRegex.IsMatch(line))
                continue;

            result.Add(line);
        }

        return result;
    }

    private static string NormalizeRefLabel(string label) => Regex.Replace(label.Trim(), @"\s+", " ").ToLowerInvariant();

    // ------------------------------------------------------------------
    // Block sequence (recursive: top level, blockquote contents, list item contents, details contents)
    // ------------------------------------------------------------------
    private static List<MarkdownBlock> ParseBlockSequence(List<string> lines, MarkdownDocument doc, HashSet<string> usedSlugs)
    {
        var blocks = new List<MarkdownBlock>();
        int i = 0;
        int n = lines.Count;

        while (i < n)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            int indent = LeadingSpaces(line);
            string trimmed = line.TrimStart();

            if (indent < 4 && IsThematicBreak(trimmed))
            {
                blocks.Add(new ThematicBreakBlock { SourceLine = i + 1 });
                i++;
                continue;
            }

            var atx = TryMatchAtxHeading(trimmed);
            if (indent < 4 && atx.HasValue)
            {
                var (level, text) = atx.Value;
                AddHeading(blocks, doc, usedSlugs, level, text, i + 1);
                i++;
                continue;
            }

            if (indent < 4 && TryMatchFenceStart(trimmed, out string fenceChar, out int fenceLen, out string? lang))
            {
                i++;
                var codeLines = new List<string>();
                while (i < n)
                {
                    string lt = lines[i].TrimStart();
                    if (LeadingSpaces(lines[i]) < 4 && lt.Length >= fenceLen &&
                        IsAllChar(lt.TrimEnd(), fenceChar[0]) && lt.TrimEnd().Length >= fenceLen)
                    {
                        i++;
                        break;
                    }
                    codeLines.Add(StripIndent(lines[i], indent));
                    i++;
                }
                blocks.Add(new CodeBlockBlock { Code = string.Join("\n", codeLines), Language = lang, Fenced = true, SourceLine = i });
                continue;
            }

            if (indent < 4 && trimmed.StartsWith("<details", StringComparison.OrdinalIgnoreCase))
            {
                int endIdx = FindClosingTagLine(lines, i, "details");
                if (endIdx >= i)
                {
                    var inner = lines.GetRange(i + 1, Math.Max(0, endIdx - i - 1));
                    blocks.Add(ParseDetailsBlock(trimmed, inner, doc, usedSlugs));
                    i = endIdx + 1;
                    continue;
                }
            }

            if (indent < 4 && LooksLikeHtmlBlockStart(trimmed))
            {
                var htmlLines = new List<string> { line };
                i++;
                while (i < n && !string.IsNullOrWhiteSpace(lines[i])) { htmlLines.Add(lines[i]); i++; }
                blocks.Add(new RawHtmlBlock { Html = string.Join("\n", htmlLines), SourceLine = i });
                continue;
            }

            if (indent < 4 && trimmed.StartsWith(">"))
            {
                var (quoteLines, consumed) = CollectBlockquoteLines(lines, i);
                var quoteBlocks = ParseBlockSequence(quoteLines, doc, usedSlugs);
                var bq = new BlockQuoteBlock { Blocks = quoteBlocks, SourceLine = i + 1 };
                ApplyAlertKind(bq);
                blocks.Add(bq);
                i += Math.Max(1, consumed);
                continue;
            }

            if (indent < 4 && i + 1 < n && LooksLikeTableRow(trimmed) && IsTableDelimiterRow(lines[i + 1].Trim()))
            {
                int consumed = ParseTable(lines, i, doc, out var table);
                blocks.Add(table);
                i += consumed;
                continue;
            }

            if (indent < 4 && TryMatchListMarker(trimmed, out _, out _, out _, out _))
            {
                int consumed = ParseList(lines, i, doc, usedSlugs, out var list);
                blocks.Add(list);
                i += Math.Max(1, consumed);
                continue;
            }

            if (indent >= 4)
            {
                var codeLines = new List<string>();
                while (i < n)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        if (i + 1 < n && LeadingSpaces(lines[i + 1]) >= 4) { codeLines.Add(""); i++; continue; }
                        break;
                    }
                    if (LeadingSpaces(lines[i]) < 4) break;
                    codeLines.Add(lines[i].Length >= 4 ? lines[i][4..] : "");
                    i++;
                }
                while (codeLines.Count > 0 && string.IsNullOrWhiteSpace(codeLines[^1])) codeLines.RemoveAt(codeLines.Count - 1);
                blocks.Add(new CodeBlockBlock { Code = string.Join("\n", codeLines), Fenced = false, SourceLine = i });
                continue;
            }

            // Paragraph, with lazy continuation + setext heading upgrade.
            {
                var paraLines = new List<string> { trimmed };
                int start = i;
                i++;
                bool convertedToHeading = false;

                while (i < n)
                {
                    string l = lines[i];
                    if (string.IsNullOrWhiteSpace(l)) break;
                    string lt = l.TrimStart();
                    int li = LeadingSpaces(l);

                    if (li < 4 && IsSetextUnderline(lt, out int setextLevel))
                    {
                        string headingText = string.Join("\n", paraLines);
                        AddHeading(blocks, doc, usedSlugs, setextLevel, headingText, start + 1);
                        i++;
                        convertedToHeading = true;
                        break;
                    }

                    bool interrupts = li < 4 && (
                        TryMatchAtxHeading(lt).HasValue ||
                        IsThematicBreak(lt) ||
                        TryMatchFenceStart(lt, out _, out _, out _) ||
                        lt.StartsWith(">") ||
                        lt.StartsWith("<details", StringComparison.OrdinalIgnoreCase) ||
                        (TryMatchListMarker(lt, out bool ord, out int num, out _, out _) && (!ord || num == 1)));
                    if (interrupts) break;

                    paraLines.Add(lt);
                    i++;
                }

                if (!convertedToHeading)
                {
                    string text = string.Join("\n", paraLines);
                    blocks.Add(new ParagraphBlock
                    {
                        Inlines = MarkdownInlineParser.Parse(text, doc.LinkReferences),
                        SourceLine = start + 1
                    });
                }
            }
        }

        return blocks;
    }

    private static void AddHeading(List<MarkdownBlock> blocks, MarkdownDocument doc, HashSet<string> usedSlugs,
        int level, string text, int sourceLine)
    {
        var inlines = MarkdownInlineParser.Parse(text, doc.LinkReferences);
        string slug = MakeSlug(text, usedSlugs);
        blocks.Add(new HeadingBlock { Level = Math.Clamp(level, 1, 6), Inlines = inlines, SourceLine = sourceLine, Slug = slug });
        doc.Outline.Add((Math.Clamp(level, 1, 6), PlainText(inlines), slug));
    }

    // ------------------------------------------------------------------
    // Line classification helpers
    // ------------------------------------------------------------------
    private static int LeadingSpaces(string line)
    {
        int n = 0;
        while (n < line.Length && line[n] == ' ') n++;
        return n;
    }

    private static bool IsAllChar(string s, char c)
    {
        if (s.Length == 0) return false;
        foreach (var ch in s) if (ch != c) return false;
        return true;
    }

    private static bool IsThematicBreak(string trimmed)
    {
        if (trimmed.Length < 3) return false;
        char c = trimmed[0];
        if (c != '-' && c != '*' && c != '_') return false;
        int count = 0;
        foreach (char ch in trimmed)
        {
            if (ch == c) count++;
            else if (ch != ' ' && ch != '\t') return false;
        }
        return count >= 3;
    }

    private static readonly Regex AtxRegex = new(@"^(#{1,6})(?:\s+(.*?))?\s*#*\s*$", RegexOptions.Compiled);

    private static (int Level, string Text)? TryMatchAtxHeading(string trimmed)
    {
        if (trimmed.Length == 0 || trimmed[0] != '#') return null;
        var m = AtxRegex.Match(trimmed);
        if (!m.Success) return null;
        int level = m.Groups[1].Length;
        if (trimmed.Length > level && trimmed[level] != ' ' && trimmed[level] != '\t') return null;
        return (level, m.Groups[2].Value);
    }

    private static bool TryMatchFenceStart(string trimmed, out string fenceChar, out int fenceLen, out string? lang)
    {
        fenceChar = ""; fenceLen = 0; lang = null;
        if (trimmed.Length < 3) return false;
        char c = trimmed[0];
        if (c != '`' && c != '~') return false;
        int len = 0;
        while (len < trimmed.Length && trimmed[len] == c) len++;
        if (len < 3) return false;
        string rest = trimmed[len..].Trim();
        if (c == '`' && rest.Contains('`')) return false;
        fenceChar = c.ToString();
        fenceLen = len;
        lang = rest.Length > 0 ? rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0] : null;
        return true;
    }

    private static bool IsSetextUnderline(string trimmed, out int level)
    {
        level = 0;
        if (trimmed.Length == 0) return false;
        char c = trimmed[0];
        if (c != '=' && c != '-') return false;
        bool sawMarker = false;
        foreach (char ch in trimmed)
        {
            if (ch == c) sawMarker = true;
            else if (ch != ' ') return false;
        }
        if (!sawMarker) return false;
        level = c == '=' ? 1 : 2;
        return true;
    }

    private static readonly Regex OrderedMarkerRegex = new(@"^(\d{1,9})([.)])(?:\s+(.*)|\s*)$", RegexOptions.Compiled);

    private static bool TryMatchListMarker(string trimmed, out bool ordered, out int number, out char bulletOrDelim, out string content)
    {
        ordered = false; number = 1; bulletOrDelim = '-'; content = "";

        if (trimmed.Length >= 1 && (trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+'))
        {
            if (trimmed.Length == 1 || trimmed[1] == ' ' || trimmed[1] == '\t')
            {
                if (IsThematicBreak(trimmed)) return false;
                bulletOrDelim = trimmed[0];
                content = trimmed.Length > 1 ? trimmed[1..].TrimStart(' ', '\t') : "";
                return true;
            }
            return false;
        }

        var m = OrderedMarkerRegex.Match(trimmed);
        if (m.Success)
        {
            ordered = true;
            number = int.Parse(m.Groups[1].Value);
            bulletOrDelim = m.Groups[2].Value[0];
            content = m.Groups[3].Success ? m.Groups[3].Value : "";
            return true;
        }

        return false;
    }

    private static string StripIndent(string line, int amount)
    {
        int strip = 0;
        while (strip < amount && strip < line.Length && line[strip] == ' ') strip++;
        return line[strip..];
    }

    // ------------------------------------------------------------------
    // Blockquote
    // ------------------------------------------------------------------
    private static (List<string> Lines, int Consumed) CollectBlockquoteLines(List<string> lines, int start)
    {
        var result = new List<string>();
        int i = start;
        int n = lines.Count;
        bool lastLineHadMarker = true;

        while (i < n)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();
            int indent = LeadingSpaces(line);

            if (indent < 4 && trimmed.StartsWith(">"))
            {
                string rest = trimmed[1..];
                if (rest.StartsWith(" ")) rest = rest[1..];
                result.Add(rest);
                lastLineHadMarker = true;
                i++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) break;

            if (lastLineHadMarker && indent < 4 &&
                !IsThematicBreak(trimmed) && !TryMatchAtxHeading(trimmed).HasValue &&
                !TryMatchFenceStart(trimmed, out _, out _, out _) &&
                !TryMatchListMarker(trimmed, out _, out _, out _, out _))
            {
                result.Add(trimmed);
                i++;
                continue;
            }

            break;
        }

        return (result, Math.Max(1, i - start));
    }

    private static readonly Regex AlertRegex = new(@"^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\](?:\s+(.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static void ApplyAlertKind(BlockQuoteBlock bq)
    {
        if (bq.Blocks.Count == 0 || bq.Blocks[0] is not ParagraphBlock p || p.Inlines.Count == 0) return;
        if (p.Inlines[0] is not TextInline t) return;

        var m = AlertRegex.Match(t.Text);
        if (!m.Success) return;

        bq.AlertKind = m.Groups[1].Value.ToUpperInvariant() switch
        {
            "NOTE" => AlertKind.Note,
            "TIP" => AlertKind.Tip,
            "IMPORTANT" => AlertKind.Important,
            "WARNING" => AlertKind.Warning,
            "CAUTION" => AlertKind.Caution,
            _ => AlertKind.None
        };

        string remainder = m.Groups[2].Success ? m.Groups[2].Value : "";
        if (remainder.Length == 0)
        {
            p.Inlines.RemoveAt(0);
            while (p.Inlines.Count > 0 && p.Inlines[0] is LineBreakInline) p.Inlines.RemoveAt(0);
        }
        else
        {
            t.Text = remainder;
        }
    }

    // ------------------------------------------------------------------
    // Tables
    // ------------------------------------------------------------------
    private static bool LooksLikeTableRow(string trimmed) => trimmed.Contains('|');

    private static readonly Regex TableDelimCellRegex = new(@"^:?-+:?$", RegexOptions.Compiled);

    private static bool IsTableDelimiterRow(string trimmed)
    {
        if (!trimmed.Contains('-')) return false;
        var cells = SplitTableRow(trimmed);
        if (cells.Count == 0) return false;
        foreach (var c in cells)
            if (!TableDelimCellRegex.IsMatch(c.Trim())) return false;
        return true;
    }

    private static List<string> SplitTableRow(string row)
    {
        row = row.Trim();
        if (row.StartsWith("|")) row = row[1..];
        if (row.EndsWith("|") && !row.EndsWith("\\|")) row = row[..^1];

        var cells = new List<string>();
        var sb = new StringBuilder();
        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];
            if (c == '\\' && i + 1 < row.Length && row[i + 1] == '|') { sb.Append('|'); i++; continue; }
            if (c == '|') { cells.Add(sb.ToString().Trim()); sb.Clear(); continue; }
            sb.Append(c);
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }

    private static int ParseTable(List<string> lines, int start, MarkdownDocument doc, out TableBlock table)
    {
        table = new TableBlock { SourceLine = start + 1 };
        var headerCells = SplitTableRow(lines[start].Trim());
        var delimCells = SplitTableRow(lines[start + 1].Trim());

        foreach (var d in delimCells)
        {
            string dd = d.Trim();
            bool left = dd.StartsWith(":");
            bool right = dd.EndsWith(":");
            table.Alignments.Add(left && right ? ColumnAlignment.Center : right ? ColumnAlignment.Right : left ? ColumnAlignment.Left : ColumnAlignment.None);
        }

        foreach (var h in headerCells)
            table.HeaderCells.Add(MarkdownInlineParser.Parse(h, doc.LinkReferences));

        int i = start + 2;
        while (i < lines.Count && !string.IsNullOrWhiteSpace(lines[i]) && lines[i].Contains('|'))
        {
            var raw = SplitTableRow(lines[i].Trim());
            var row = new List<List<MarkdownInline>>();
            for (int c = 0; c < table.Alignments.Count; c++)
                row.Add(MarkdownInlineParser.Parse(c < raw.Count ? raw[c] : "", doc.LinkReferences));
            table.Rows.Add(row);
            i++;
        }

        return i - start;
    }

    // ------------------------------------------------------------------
    // Lists
    // ------------------------------------------------------------------
    private static readonly Regex TaskMarkerRegex = new(@"^\[([ xX])\]\s+(.*)$", RegexOptions.Compiled);

    private static int ParseList(List<string> lines, int start, MarkdownDocument doc, HashSet<string> usedSlugs, out ListBlock list)
    {
        TryMatchListMarker(lines[start].TrimStart(), out bool ordered, out int firstNumber, out char bulletDelim, out _);
        list = new ListBlock
        {
            Kind = ordered ? ListKind.Ordered : ListKind.Unordered,
            StartNumber = firstNumber,
            BulletChar = bulletDelim,
            SourceLine = start + 1
        };

        int i = start;
        int n = lines.Count;
        bool sawBlankBetweenItems = false;

        while (i < n)
        {
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                int j = i;
                while (j < n && string.IsNullOrWhiteSpace(lines[j])) j++;
                if (j >= n) { i = j; break; }

                int jIndent = LeadingSpaces(lines[j]);
                if (jIndent < 4 && TryMatchListMarker(lines[j].TrimStart(), out bool jOrdered, out _, out char jDelim, out _) &&
                    jOrdered == ordered && (jOrdered || jDelim == bulletDelim))
                {
                    sawBlankBetweenItems = true;
                    i = j;
                    continue;
                }
                break;
            }

            int indent = LeadingSpaces(line);
            string trimmed = line.TrimStart();
            if (indent >= 4) break;

            if (!TryMatchListMarker(trimmed, out bool itemOrdered, out int itemNumber, out char itemDelim, out string firstContent) ||
                itemOrdered != ordered || (!ordered && itemDelim != bulletDelim))
                break;

            int contentColumn = string.IsNullOrEmpty(firstContent)
                ? indent + (ordered ? itemNumber.ToString().Length + 2 : 2)
                : line.Length - firstContent.Length;

            var itemLines = new List<string>();
            if (!string.IsNullOrEmpty(firstContent)) itemLines.Add(firstContent);
            i++;

            while (i < n)
            {
                string l = lines[i];
                if (string.IsNullOrWhiteSpace(l))
                {
                    int j = i;
                    while (j < n && string.IsNullOrWhiteSpace(lines[j])) j++;
                    if (j < n && LeadingSpaces(lines[j]) >= contentColumn)
                    {
                        for (int k = i; k < j; k++) itemLines.Add("");
                        i = j;
                        continue;
                    }
                    break;
                }

                if (LeadingSpaces(l) >= contentColumn)
                {
                    itemLines.Add(l[Math.Min(contentColumn, l.Length)..]);
                    i++;
                    continue;
                }

                break;
            }

            bool? taskChecked = null;
            if (itemLines.Count > 0)
            {
                var tm = TaskMarkerRegex.Match(itemLines[0]);
                if (tm.Success)
                {
                    taskChecked = !string.Equals(tm.Groups[1].Value, " ", StringComparison.Ordinal);
                    itemLines[0] = tm.Groups[2].Value;
                }
            }

            var itemBlocks = ParseBlockSequence(itemLines, doc, usedSlugs);
            list.Items.Add(new ListItemBlock { Blocks = itemBlocks, TaskChecked = taskChecked, SourceLine = i });
        }

        bool anyItemHasMultipleBlocks = false;
        foreach (var item in list.Items)
            if (item.Blocks.Count > 1) { anyItemHasMultipleBlocks = true; break; }

        list.Tight = !sawBlankBetweenItems && !anyItemHasMultipleBlocks;
        return i - start;
    }

    // ------------------------------------------------------------------
    // <details>/<summary>
    // ------------------------------------------------------------------
    private static int FindClosingTagLine(List<string> lines, int start, string tagName)
    {
        string closeTag = "</" + tagName + ">";
        for (int i = start; i < lines.Count; i++)
            if (lines[i].Contains(closeTag, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static DetailsBlock ParseDetailsBlock(string openTagLine, List<string> innerLines, MarkdownDocument doc, HashSet<string> usedSlugs)
    {
        var details = new DetailsBlock { DefaultOpen = openTagLine.Contains("open", StringComparison.OrdinalIgnoreCase) };
        var content = new List<string>(innerLines);

        if (content.Count > 0)
        {
            int summaryIdx = content.FindIndex(l => l.TrimStart().StartsWith("<summary", StringComparison.OrdinalIgnoreCase));
            if (summaryIdx >= 0)
            {
                var sb = new StringBuilder();
                int j = summaryIdx;
                while (j < content.Count)
                {
                    sb.Append(content[j]).Append(' ');
                    bool closed = content[j].Contains("</summary>", StringComparison.OrdinalIgnoreCase);
                    j++;
                    if (closed) break;
                }
                string raw = Regex.Replace(sb.ToString(), @"</?summary[^>]*>", "", RegexOptions.IgnoreCase);
                details.Summary = Regex.Replace(raw, @"<[^>]+>", "").Trim();
                content.RemoveRange(summaryIdx, Math.Min(j - summaryIdx, content.Count - summaryIdx));
            }
        }

        details.Blocks = ParseBlockSequence(content, doc, usedSlugs);
        if (string.IsNullOrWhiteSpace(details.Summary)) details.Summary = "Details";
        return details;
    }

    private static bool LooksLikeHtmlBlockStart(string trimmed) =>
        Regex.IsMatch(trimmed, @"^</?(div|p|table|ul|ol|li|h[1-6]|blockquote|pre|hr|img|a|span|section|article)\b",
            RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------
    // Slugs (heading anchors, used by MarkdownViewer's outline / ScrollToHeading API)
    // ------------------------------------------------------------------
    private static string MakeSlug(string headingText, HashSet<string> usedSlugs)
    {
        string plain = Regex.Replace(headingText, @"[*_`~\[\]()#\n]", " ");
        plain = plain.ToLowerInvariant().Trim();
        plain = Regex.Replace(plain, @"[^a-z0-9\s-]", "");
        plain = Regex.Replace(plain, @"\s+", "-").Trim('-');
        if (plain.Length == 0) plain = "section";

        string candidate = plain;
        int suffix = 1;
        while (usedSlugs.Contains(candidate))
            candidate = $"{plain}-{suffix++}";
        usedSlugs.Add(candidate);
        return candidate;
    }
}
