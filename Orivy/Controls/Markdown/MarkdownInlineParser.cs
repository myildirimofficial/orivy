using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Orivy.Controls.Markdown;

/// <summary>
/// Parses the inline content of a single block's raw text (already line-joined by the
/// block parser, with soft/hard break markers preserved as "\n"/"\\\n" respectively)
/// into a tree of <see cref="MarkdownInline"/> nodes.
///
/// This implements a simplified-but-careful version of the CommonMark inline algorithm:
/// code spans / autolinks / raw safelisted HTML are resolved first (highest precedence,
/// cannot be split by emphasis), then links/images, then a delimiter-stack pass resolves
/// '*'/'_'/'~~' emphasis runs (including the "left/right-flanking" and "multiple-of-3"
/// rules that matter for correctly nesting "**bold *and italic***" style text). It is not
/// a byte-for-byte CommonMark implementation, but it is correct for the overwhelming
/// majority of real-world documents.
/// </summary>
public static class MarkdownInlineParser
{
    private static readonly Regex EntityRegex = new(@"&(#x[0-9a-fA-F]+|#\d+|[a-zA-Z][a-zA-Z0-9]*);", RegexOptions.Compiled);
    private static readonly Regex BareUrlRegex = new(@"\G(https?://|www\.)[^\s<>""')\]]+", RegexOptions.Compiled);
    private static readonly Regex BareEmailRegex = new(@"\G[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    private static readonly Dictionary<string, char> NamedEntities = new(StringComparer.Ordinal)
    {
        ["amp"] = '&', ["lt"] = '<', ["gt"] = '>', ["quot"] = '"', ["apos"] = '\'',
        ["nbsp"] = '\u00A0', ["copy"] = '\u00A9', ["reg"] = '\u00AE', ["trade"] = '\u2122',
        ["mdash"] = '\u2014', ["ndash"] = '\u2013', ["hellip"] = '\u2026',
        ["lsquo"] = '\u2018', ["rsquo"] = '\u2019', ["ldquo"] = '\u201C', ["rdquo"] = '\u201D',
    };

    private sealed class Delimiter
    {
        public int NodeIndex;     // index into the working node list (a TextInline holding the literal marker chars)
        public char Marker;       // '*', '_' or '~'
        public int Length;        // remaining un-matched marker length
        public bool CanOpen;
        public bool CanClose;
        public bool Active = true;
    }

    // ------------------------------------------------------------------
    // Typographic replacements (smartypants-style)
    // ------------------------------------------------------------------
    private static string ApplyTypography(string text)
    {
        if (!ContainsTypographyTrigger(text)) return text;
        text = text.Replace("---", "\u2014"); // em dash
        text = text.Replace("--", "\u2013");  // en dash
        text = text.Replace("...", "\u2026"); // ellipsis
        return SmartDoubleQuotes(SmartSingleQuotes(text));
    }

    private static bool ContainsTypographyTrigger(string text) =>
        text.Contains('-') || text.Contains('.') || text.Contains('"') || text.Contains('\'');

    private static string SmartDoubleQuotes(string text)
    {
        if (text.IndexOf('"') < 0) return text;
        var sb = new StringBuilder(text.Length);
        bool afterOpenContext = true;
        foreach (char c in text)
        {
            if (c == '"') { sb.Append(afterOpenContext ? '\u201C' : '\u201D'); afterOpenContext = false; }
            else { sb.Append(c); afterOpenContext = char.IsWhiteSpace(c) || c is '(' or '[' or '\u201C'; }
        }
        return sb.ToString();
    }

    private static string SmartSingleQuotes(string text)
    {
        if (text.IndexOf('\'') < 0) return text;
        var sb = new StringBuilder(text.Length);
        bool afterOpenContext = true;
        foreach (char c in text)
        {
            if (c == '\'') { sb.Append(afterOpenContext ? '\u2018' : '\u2019'); afterOpenContext = false; }
            else { sb.Append(c); afterOpenContext = char.IsWhiteSpace(c) || c is '(' or '[' or '\u2018'; }
        }
        return sb.ToString();
    }

    public static List<MarkdownInline> Parse(string raw, Dictionary<string, LinkReferenceDefinition> refs)
    {
        var nodes = new List<MarkdownInline>();
        var delimiters = new List<Delimiter>();

        int i = 0;
        int n = raw.Length;
        var plain = new StringBuilder();

        void FlushPlain()
        {
            if (plain.Length == 0) return;
            string text = ApplyTypography(plain.ToString());
            plain.Clear();
            if (text.Length > 0) nodes.Add(new TextInline { Text = text });
        }

        while (i < n)
        {
            char c = raw[i];

            // Hard / soft line breaks: a literal '\n' was inserted by the block parser between
            // source lines. A preceding "  " (2+ spaces) or "\\" makes it hard.
            if (c == '\n')
            {
                bool hard = false;
                if (plain.Length > 0 && plain[^1] == '\\') { plain.Length -= 1; hard = true; }
                else
                {
                    int trail = 0;
                    while (plain.Length - trail > 0 && plain[plain.Length - trail - 1] == ' ') trail++;
                    if (trail >= 2) { plain.Length -= trail; hard = true; }
                }
                FlushPlain();
                nodes.Add(new LineBreakInline { Hard = hard });
                i++;
                continue;
            }

            if (c == '\\' && i + 1 < n && IsAsciiPunctuation(raw[i + 1]))
            {
                plain.Append(raw[i + 1]);
                i += 2;
                continue;
            }

            if (c == '&')
            {
                var m = EntityRegex.Match(raw, i);
                if (m.Success && m.Index == i)
                {
                    plain.Append(DecodeEntity(m.Groups[1].Value));
                    i += m.Length;
                    continue;
                }
            }

            if (c == '`')
            {
                int runStart = i;
                int runLen = 0;
                while (i < n && raw[i] == '`') { i++; runLen++; }
                int closeStart = raw.IndexOf(new string('`', runLen), i, StringComparison.Ordinal);
                // make sure it's an exact-length run, not a longer one
                while (closeStart >= 0)
                {
                    int after = closeStart + runLen;
                    if (after >= n || raw[after] != '`') break;
                    closeStart = raw.IndexOf(new string('`', runLen), after, StringComparison.Ordinal);
                }
                if (closeStart < 0)
                {
                    plain.Append(raw, runStart, i - runStart);
                    continue;
                }
                FlushPlain();
                string code = raw.Substring(i, closeStart - i).Replace('\n', ' ');
                if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                    code = code[1..^1];
                nodes.Add(new CodeSpanInline { Code = code });
                i = closeStart + runLen;
                continue;
            }

            if (c == '<')
            {
                var consumed = TryParseAngleConstruct(raw, i, nodes, plain, FlushPlain);
                if (consumed > 0) { i += consumed; continue; }
            }

            if ((c == 'h' && BareUrlRegex.IsMatch(raw[i..])) || (c == 'w' && raw.AsSpan(i).StartsWith("www.")))
            {
                var m = BareUrlRegex.Match(raw, i);
                if (m.Success && m.Index == i)
                {
                    string url = TrimTrailingAutolinkPunctuation(m.Value);
                    FlushPlain();
                    string display = url;
                    string href = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + url : url;
                    nodes.Add(new AutoLinkInline { Url = href, DisplayText = display });
                    i += url.Length;
                    continue;
                }
            }

            if (c == '!' && i + 1 < n && raw[i + 1] == '[')
            {
                int consumed = TryParseImage(raw, i, refs, nodes, FlushPlain);
                if (consumed > 0) { i += consumed; continue; }
            }

            if (c == '[')
            {
                int consumed = TryParseLink(raw, i, refs, nodes, delimiters, FlushPlain);
                if (consumed > 0) { i += consumed; continue; }
            }

            if (c is '*' or '_' or '~')
            {
                int start = i;
                char marker = c;
                int runLen = 0;
                while (i < n && raw[i] == marker) { i++; runLen++; }

                if (marker == '~' && runLen != 2)
                {
                    // Only "~~" is a strikethrough delimiter; lone '~' is literal.
                    plain.Append('~', runLen);
                    continue;
                }

                char before = start > 0 ? raw[start - 1] : ' ';
                char after = i < n ? raw[i] : ' ';
                bool beforeWhitespace = char.IsWhiteSpace(before);
                bool afterWhitespace = char.IsWhiteSpace(after);
                bool beforePunct = IsAsciiPunctuation(before);
                bool afterPunct = IsAsciiPunctuation(after);

                bool leftFlanking = !afterWhitespace && (!afterPunct || beforeWhitespace || beforePunct);
                bool rightFlanking = !beforeWhitespace && (!beforePunct || afterWhitespace || afterPunct);

                bool canOpen = leftFlanking;
                bool canClose = rightFlanking;
                if (marker == '_')
                {
                    // CommonMark intraword underscore restriction.
                    canOpen = leftFlanking && (!rightFlanking || beforePunct);
                    canClose = rightFlanking && (!leftFlanking || afterPunct);
                }

                FlushPlain();
                nodes.Add(new TextInline { Text = new string(marker, runLen) });
                delimiters.Add(new Delimiter
                {
                    NodeIndex = nodes.Count - 1,
                    Marker = marker,
                    Length = runLen,
                    CanOpen = canOpen,
                    CanClose = canClose,
                });
                continue;
            }

            // Emoji shortcode: :code:
            if (c == ':')
            {
                int end = raw.IndexOf(':', i + 1);
                if (end > i + 1 && end - i - 1 <= 40)
                {
                    string code = raw.Substring(i + 1, end - i - 1);
                    if (IsValidEmojiCodeName(code))
                    {
                        string? emoji = MarkdownEmojiTable.Lookup(code);
                        if (emoji != null)
                        {
                            FlushPlain();
                            nodes.Add(new TextInline { Text = emoji });
                            i = end + 1;
                            continue;
                        }
                    }
                }
            }

            plain.Append(c);
            i++;
        }

        FlushPlain();
        ResolveEmphasis(nodes, delimiters);
        return nodes;
    }

    private static bool IsValidEmojiCodeName(string code)
    {
        if (code.Length == 0) return false;
        foreach (char ch in code)
            if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '+' && ch != '-') return false;
        return true;
    }

    // ------------------------------------------------------------------
    // Emphasis / strong / strikethrough resolution (delimiter-stack algorithm)
    // ------------------------------------------------------------------
    private static void ResolveEmphasis(List<MarkdownInline> nodes, List<Delimiter> delimiters)
    {
        var openersBottom = new Dictionary<char, int>();

        for (int closeIdx = 0; closeIdx < delimiters.Count; closeIdx++)
        {
            var closer = delimiters[closeIdx];
            if (!closer.Active || !closer.CanClose || closer.Length <= 0) continue;

            int bottom = openersBottom.TryGetValue(closer.Marker, out var b) ? b : 0;
            int openIdx = -1;
            for (int k = closeIdx - 1; k >= bottom; k--)
            {
                var cand = delimiters[k];
                if (!cand.Active || cand.Marker != closer.Marker || !cand.CanOpen || cand.Length <= 0) continue;

                bool oddRule = (cand.CanOpen && cand.CanClose) || (closer.CanOpen && closer.CanClose);
                if (oddRule && (cand.Length + closer.Length) % 3 == 0 && cand.Length % 3 != 0 && closer.Length % 3 != 0)
                    continue;

                openIdx = k;
                break;
            }

            if (openIdx < 0)
            {
                openersBottom[closer.Marker] = closeIdx;
                if (!closer.CanOpen) closer.Active = false;
                continue;
            }

            var opener = delimiters[openIdx];

            // Delimiters strictly inside the span we are about to wrap become part of its
            // (literal) content; they must not be reachable as openers for closers further right.
            for (int k = openIdx + 1; k < closeIdx; k++)
                delimiters[k].Active = false;

            int use = (closer.Marker == '~') ? 2 : Math.Min(2, Math.Min(opener.Length, closer.Length));
            use = Math.Min(use, Math.Min(opener.Length, closer.Length));

            int openNodeIndex = opener.NodeIndex;
            int closeNodeIndex = closer.NodeIndex;

            var children = new List<MarkdownInline>();
            for (int idx = openNodeIndex + 1; idx < closeNodeIndex; idx++)
                if (nodes[idx] is not null)
                    children.Add(nodes[idx]);

            MarkdownInline wrapper = (use >= 2)
                ? (closer.Marker == '~' ? new StrikethroughInline { Children = children } : new StrongInline { Children = children })
                : new EmphasisInline { Children = children };

            for (int idx = openNodeIndex + 1; idx < closeNodeIndex; idx++)
                nodes[idx] = null!;

            opener.Length -= use;
            closer.Length -= use;

            var openerText = (TextInline)nodes[openNodeIndex]!;
            openerText.Text = new string(opener.Marker, opener.Length);
            var closerText = (TextInline)nodes[closeNodeIndex]!;
            closerText.Text = new string(closer.Marker, closer.Length);

            nodes[closeNodeIndex] = closerText;
            nodes.Insert(closeNodeIndex, wrapper);
            // shift node indices of all delimiters positioned at/after closeNodeIndex by +1 (we inserted a node)
            foreach (var d in delimiters)
                if (d.NodeIndex >= closeNodeIndex) d.NodeIndex++;

            if (opener.Length == 0) opener.Active = false;
            if (closer.Length == 0) { closer.Active = false; }
            else { closeIdx--; } // re-test this closer against remaining openers
        }

        // Strip emptied delimiter placeholder text nodes & null holes.
        for (int idx = nodes.Count - 1; idx >= 0; idx--)
        {
            if (nodes[idx] is null) { nodes.RemoveAt(idx); continue; }
            if (nodes[idx] is TextInline t && t.Text.Length == 0) nodes.RemoveAt(idx);
        }
    }

    // ------------------------------------------------------------------
    // Links / images
    // ------------------------------------------------------------------
    private static int TryParseLink(string raw, int start, Dictionary<string, LinkReferenceDefinition> refs,
        List<MarkdownInline> nodes, List<Delimiter> delimiters, Action flush)
    {
        int closeBracket = FindMatchingBracket(raw, start);
        if (closeBracket < 0) return 0;

        string label = raw.Substring(start + 1, closeBracket - start - 1);
        int after = closeBracket + 1;

        if (after < raw.Length && raw[after] == '(')
        {
            if (TryParseInlineDestination(raw, after, out string url, out string? title, out int consumedLen))
            {
                flush();
                nodes.Add(new LinkInline { Url = url, Title = title, Children = Parse(label, refs) });
                return after + consumedLen - start;
            }
        }

        string refLabel = label;
        int refConsumed = closeBracket + 1 - start;
        if (after < raw.Length && raw[after] == '[')
        {
            int closeRef = FindMatchingBracket(raw, after);
            if (closeRef > after)
            {
                string explicitLabel = raw.Substring(after + 1, closeRef - after - 1);
                if (explicitLabel.Length > 0) refLabel = explicitLabel;
                refConsumed = closeRef + 1 - start;
            }
        }

        if (refs.TryGetValue(NormalizeLabel(refLabel), out var def))
        {
            flush();
            nodes.Add(new LinkInline { Url = def.Url, Title = def.Title, Children = Parse(label, refs) });
            return refConsumed;
        }

        return 0;
    }

    private static int TryParseImage(string raw, int start, Dictionary<string, LinkReferenceDefinition> refs,
        List<MarkdownInline> nodes, Action flush)
    {
        int bracketStart = start + 1;      // skip '!'
        int closeBracket = FindMatchingBracket(raw, bracketStart);
        if (closeBracket < 0) return 0;

        string alt = raw.Substring(bracketStart + 1, closeBracket - bracketStart - 1);
        string cleanAlt = StripInlineMarkup(alt);
        int after = closeBracket + 1;

        // Inline: ![alt](url "title")
        if (after < raw.Length && raw[after] == '(')
        {
            if (TryParseInlineDestination(raw, after, out string url, out string? title, out int consumedLen))
            {
                flush();
                nodes.Add(new ImageInline { Url = url, Title = title, AltText = cleanAlt });
                return after + consumedLen - start;
            }
        }

        // Full reference: ![alt][id]  or collapsed: ![alt][]  or shortcut: ![alt]
        string refLabel = alt;
        int refConsumed = closeBracket + 1 - start;

        if (after < raw.Length && raw[after] == '[')
        {
            int closeRef = FindMatchingBracket(raw, after);
            if (closeRef > after)
            {
                string explicitLabel = raw.Substring(after + 1, closeRef - after - 1);
                if (explicitLabel.Length > 0) refLabel = explicitLabel;   // ![alt][id] → look up "id"
                refConsumed = closeRef + 1 - start;
            }
        }

        if (refs.TryGetValue(NormalizeLabel(refLabel), out var def))
        {
            flush();
            nodes.Add(new ImageInline { Url = def.Url, Title = def.Title, AltText = cleanAlt });
            return refConsumed;
        }

        return 0;
    }

    private static bool TryParseInlineDestination(string raw, int parenStart, out string url, out string? title, out int consumedLength)
    {
        url = ""; title = null; consumedLength = 0;
        int i = parenStart + 1;
        int n = raw.Length;
        while (i < n && raw[i] == ' ') i++;

        var urlBuilder = new StringBuilder();
        if (i < n && raw[i] == '<')
        {
            i++;
            while (i < n && raw[i] != '>') { urlBuilder.Append(raw[i]); i++; }
            if (i >= n) return false;
            i++;
        }
        else
        {
            int depth = 0;
            while (i < n)
            {
                char c = raw[i];
                if (c == '(' ) depth++;
                else if (c == ')') { if (depth == 0) break; depth--; }
                else if (char.IsWhiteSpace(c)) break;
                urlBuilder.Append(c);
                i++;
            }
        }
        url = urlBuilder.ToString();

        while (i < n && raw[i] == ' ') i++;
        if (i < n && (raw[i] == '"' || raw[i] == '\''))
        {
            char q = raw[i];
            i++;
            var t = new StringBuilder();
            while (i < n && raw[i] != q) { t.Append(raw[i]); i++; }
            if (i >= n) return false;
            i++;
            title = t.ToString();
            while (i < n && raw[i] == ' ') i++;
        }

        if (i >= n || raw[i] != ')') return false;
        i++;
        consumedLength = i - parenStart;
        return true;
    }

    private static int FindMatchingBracket(string raw, int openBracketIndex)
    {
        int depth = 0;
        for (int i = openBracketIndex; i < raw.Length; i++)
        {
            if (raw[i] == '\\') { i++; continue; }
            if (raw[i] == '[') depth++;
            else if (raw[i] == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static string NormalizeLabel(string label) =>
        Regex.Replace(label.Trim(), @"\s+", " ").ToLowerInvariant();

    private static string StripInlineMarkup(string text) => Regex.Replace(text, @"[\[\]*_`~]", "");

    // ------------------------------------------------------------------
    // <...> constructs: autolinks and a small inline-HTML safelist
    // ------------------------------------------------------------------
    private static readonly Regex AutoLinkAngle = new(@"\G<([a-zA-Z][a-zA-Z0-9+.-]{1,31}:[^\s<>]*)>", RegexOptions.Compiled);
    private static readonly Regex AutoLinkEmailAngle = new(@"\G<([A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,})>", RegexOptions.Compiled);

    private static int TryParseAngleConstruct(string raw, int i, List<MarkdownInline> nodes, StringBuilder plain, Action flush)
    {
        var m = AutoLinkAngle.Match(raw, i);
        if (m.Success && m.Index == i)
        {
            flush();
            nodes.Add(new AutoLinkInline { Url = m.Groups[1].Value, DisplayText = m.Groups[1].Value });
            return m.Length;
        }
        m = AutoLinkEmailAngle.Match(raw, i);
        if (m.Success && m.Index == i)
        {
            flush();
            nodes.Add(new AutoLinkInline { Url = "mailto:" + m.Groups[1].Value, DisplayText = m.Groups[1].Value });
            return m.Length;
        }

        if (TryMatchTag(raw, i, "br")) { flush(); nodes.Add(new LineBreakInline { Hard = true }); return TagLength(raw, i); }

        if (TryMatchOpenTag(raw, i, "sub", out int subLen))
        {
            int close = raw.IndexOf("</sub>", i + subLen, StringComparison.OrdinalIgnoreCase);
            if (close > 0)
            {
                flush();
                string inner = raw.Substring(i + subLen, close - (i + subLen));
                nodes.Add(new InlineHtmlInline { Kind = InlineHtmlKind.Subscript, Children = Parse(inner, new()) });
                return close + 6 - i;
            }
        }
        if (TryMatchOpenTag(raw, i, "sup", out int supLen))
        {
            int close = raw.IndexOf("</sup>", i + supLen, StringComparison.OrdinalIgnoreCase);
            if (close > 0)
            {
                flush();
                string inner = raw.Substring(i + supLen, close - (i + supLen));
                nodes.Add(new InlineHtmlInline { Kind = InlineHtmlKind.Superscript, Children = Parse(inner, new()) });
                return close + 6 - i;
            }
        }
        if (TryMatchOpenTag(raw, i, "kbd", out int kbdLen))
        {
            int close = raw.IndexOf("</kbd>", i + kbdLen, StringComparison.OrdinalIgnoreCase);
            if (close > 0)
            {
                flush();
                string inner = raw.Substring(i + kbdLen, close - (i + kbdLen));
                nodes.Add(new InlineHtmlInline { Kind = InlineHtmlKind.KeyboardKey, Children = Parse(inner, new()) });
                return close + 6 - i;
            }
        }

        foreach (var (tag, factory) in new (string, Func<List<MarkdownInline>, MarkdownInline>)[]
                 {
                     ("strong", c => new StrongInline { Children = c }),
                     ("b", c => new StrongInline { Children = c }),
                     ("em", c => new EmphasisInline { Children = c }),
                     ("i", c => new EmphasisInline { Children = c }),
                     ("del", c => new StrikethroughInline { Children = c }),
                     ("s", c => new StrikethroughInline { Children = c }),
                     ("code", c => null!), // handled specially below
                 })
        {
            if (tag == "code" && TryMatchOpenTag(raw, i, "code", out int codeLen))
            {
                int close = raw.IndexOf("</code>", i + codeLen, StringComparison.OrdinalIgnoreCase);
                if (close > 0)
                {
                    flush();
                    nodes.Add(new CodeSpanInline { Code = raw.Substring(i + codeLen, close - (i + codeLen)) });
                    return close + 7 - i;
                }
                continue;
            }
            if (TryMatchOpenTag(raw, i, tag, out int len))
            {
                string closeTag = "</" + tag + ">";
                int close = raw.IndexOf(closeTag, i + len, StringComparison.OrdinalIgnoreCase);
                if (close > 0)
                {
                    flush();
                    string inner = raw.Substring(i + len, close - (i + len));
                    nodes.Add(factory(Parse(inner, new())));
                    return close + closeTag.Length - i;
                }
            }
        }

        // Unknown / unsupported tag: drop it silently rather than rendering raw angle brackets
        // that would otherwise look like a parsing glitch.
        if (TryMatchAnyTag(raw, i, out int anyLen)) return anyLen;

        return 0;
    }

    private static bool TryMatchTag(string raw, int i, string name)
    {
        string pattern = "<" + name;
        if (string.CompareOrdinal(raw, i, pattern, 0, pattern.Length) != 0) return false;
        int j = i + pattern.Length;
        while (j < raw.Length && raw[j] != '>') j++;
        return j < raw.Length;
    }

    private static int TagLength(string raw, int i)
    {
        int j = i;
        while (j < raw.Length && raw[j] != '>') j++;
        return j < raw.Length ? j - i + 1 : raw.Length - i;
    }

    private static bool TryMatchOpenTag(string raw, int i, string name, out int length)
    {
        length = 0;
        string pattern = "<" + name;
        if (i + pattern.Length > raw.Length) return false;
        if (string.CompareOrdinal(raw, i, pattern, 0, pattern.Length) != 0) return false;
        int j = i + pattern.Length;
        if (j < raw.Length && raw[j] != '>' && raw[j] != ' ') return false; // e.g. "<sub2" should not match "sub"
        while (j < raw.Length && raw[j] != '>') j++;
        if (j >= raw.Length) return false;
        length = j - i + 1;
        return true;
    }

    private static bool TryMatchAnyTag(string raw, int i, out int length)
    {
        length = 0;
        var m = Regex.Match(raw[i..], @"^</?[a-zA-Z][a-zA-Z0-9-]*(\s+[^<>]*)?/?>");
        if (!m.Success) return false;
        length = m.Length;
        return true;
    }

    private static string TrimTrailingAutolinkPunctuation(string url)
    {
        while (url.Length > 0 && ".,;:!?".IndexOf(url[^1]) >= 0) url = url[..^1];
        if (url.EndsWith(')') && url.Count(ch => ch == '(') < url.Count(ch => ch == ')'))
            url = url[..^1];
        return url;
    }

    private static bool IsAsciiPunctuation(char c) =>
        (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');

    private static string DecodeEntity(string body)
    {
        if (body.StartsWith("#x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(body[2..], System.Globalization.NumberStyles.HexNumber, null, out int code))
                return char.ConvertFromUtf32(code);
        }
        else if (body.StartsWith('#'))
        {
            if (int.TryParse(body[1..], out int code))
                return char.ConvertFromUtf32(code);
        }
        else if (NamedEntities.TryGetValue(body, out char ch))
        {
            return ch.ToString();
        }
        return "&" + body + ";";
    }
}
