using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

/// <summary>
/// Inline content parser. Handles:
///  - CommonMark: code spans, emphasis/strong (*/_), strikethrough (~~),
///    links, images, autolinks, escaped chars, HTML entities
///  - GFM extensions: strikethrough, autolink literals
///  - Extended: superscript (^text^), subscript (~text~, single tilde),
///    underline (++text++ or &lt;ins&gt;), highlight (==text== or &lt;mark&gt;),
///    &lt;kbd&gt;, emoji shortcodes (:smile:), typographic replacements
/// </summary>
public static class MarkdownInlineParser
{
    private static readonly Regex EntityRegex     = new(@"&(#x[0-9a-fA-F]+|#\d+|[a-zA-Z][a-zA-Z0-9]*);", RegexOptions.Compiled);
    private static readonly Regex BareUrlRegex    = new(@"\G(https?://|www\.)[^\s<>""')\]]+",              RegexOptions.Compiled);

    private static readonly Dictionary<string, char> NamedEntities = new(StringComparer.Ordinal)
    {
        ["amp"]=  '&', ["lt"]= '<', ["gt"]= '>',  ["quot"]= '"', ["apos"]= '\'',
        ["nbsp"]= '\u00A0', ["copy"]= '\u00A9', ["reg"]= '\u00AE', ["trade"]= '\u2122',
        ["mdash"]= '\u2014', ["ndash"]= '\u2013', ["hellip"]= '\u2026',
        ["lsquo"]= '\u2018', ["rsquo"]= '\u2019', ["ldquo"]= '\u201C', ["rdquo"]= '\u201D',
        ["alpha"]= '\u03B1', ["beta"]= '\u03B2', ["gamma"]= '\u03B3', ["delta"]= '\u03B4',
        ["epsilon"]= '\u03B5', ["zeta"]= '\u03B6', ["eta"]= '\u03B7', ["theta"]= '\u03B8',
        ["iota"]= '\u03B9', ["kappa"]= '\u03BA', ["lambda"]= '\u03BB', ["mu"]= '\u03BC',
        ["nu"]= '\u03BD', ["xi"]= '\u03BE', ["omicron"]= '\u03BF', ["pi"]= '\u03C0',
        ["rho"]= '\u03C1', ["sigma"]= '\u03C3', ["tau"]= '\u03C4', ["upsilon"]= '\u03C5',
        ["phi"]= '\u03D5', ["chi"]= '\u03C7', ["psi"]= '\u03C8', ["omega"]= '\u03C9',
        ["Alpha"]= '\u0391', ["Beta"]= '\u0392', ["Gamma"]= '\u0393', ["Delta"]= '\u0394',
        ["Epsilon"]= '\u0395', ["Zeta"]= '\u0396', ["Eta"]= '\u0397', ["Theta"]= '\u0398',
        ["Iota"]= '\u0399', ["Kappa"]= '\u039A', ["Lambda"]= '\u039B', ["Mu"]= '\u039C',
        ["Nu"]= '\u039D', ["Xi"]= '\u039E', ["Omicron"]= '\u039F', ["Pi"]= '\u03A0',
        ["Rho"]= '\u03A1', ["Sigma"]= '\u03A3', ["Tau"]= '\u03A4', ["Upsilon"]= '\u03A5',
        ["Phi"]= '\u03D5', ["Chi"]= '\u03A7', ["Psi"]= '\u03A8', ["Omega"]= '\u03A9',
    };

    // ── Typographic replacement table ──────────────────────────────────
    private static readonly (string From, string To)[] TypoReplacements =
    {
        ("(c)",  "\u00A9"), ("(C)",  "\u00A9"),  // ©
        ("(r)",  "\u00AE"), ("(R)",  "\u00AE"),  // ®
        ("(tm)", "\u2122"), ("(TM)", "\u2122"),  // ™
        ("(p)",  "\u00A7"), ("(P)",  "\u00A7"),  // §
        ("+-",   "\u00B1"), ("-+",   "\u00B1"),  // ±
        ("<<",   "\u00AB"),                       // «
        (">>",   "\u00BB"),                       // »
        ("...",  "\u2026"),                       // … (before -- replacements)
        ("---",  "\u2014"),                       // — em dash
        ("--",   "\u2013"),                       // – en dash
    };

    // ── Shortcut emoticon table ───────────────────────────────────────────────
    // Ordered longest-first so :-) matches before :-)  etc.
    private static readonly (string From, string To)[] EmoticonReplacements =
    {
        (":-)",  "😊"), (":)",   "😊"),
        (":-D",  "😄"), (":D",   "😄"),
        (":-(",  "😞"), (":(",   "😞"),
        (";-)",  "😉"), (";)",   "😉"),
        (":-P",  "😛"), (":P",   "😛"), (":-p", "😛"), (":p", "😛"),
        (":-|",  "😐"), (":|",   "😐"),
        (":-O",  "😮"), (":O",   "😮"), (":-o", "😮"), (":o", "😮"),
        (":'(", "😢"),
        (":-*",  "😘"), (":*",   "😘"),
        ("B-)",  "😎"), ("B)",   "😎"),
        ("8-)",  "😎"), ("8)",   "😎"),
        ("O:-)", "😇"), ("O:)",  "😇"),
        (":-/",  "😕"), (":/",   "😕"),
        ("<3",   "❤️"),
        ("</3",  "💔"),
        ("XD",   "😆"),
    };

    private static string ApplyTypography(string text)
    {
        if (text.Length < 2) return text;
        // Emoticons before typo replacements (longest match order in table handles priority)
        text = ApplyEmoticons(text);
        foreach (var (from, to) in TypoReplacements)
            if (text.Contains(from, StringComparison.Ordinal))
                text = text.Replace(from, to);
        if (text.Contains('"') || text.Contains('\''))
            text = SmartQuotes(SmartSingleQuotes(text));
        return text;
    }

    private static string ApplyEmoticons(string text)
    {
        // Fast path: skip if no emoticon trigger chars present
        if (!text.Contains(':') && !text.Contains(';') && !text.Contains('8') &&
            !text.Contains('B') && !text.Contains('O') && !text.Contains('X') &&
            !text.Contains('<'))
            return text;

        // Use StringBuilder to avoid O(n²) string allocations from repeated Remove+Insert
        var sb = new StringBuilder(text.Length + 16);
        int pos = 0;

        // Single pass: try each position for any emoticon match
        while (pos < text.Length)
        {
            bool matched = false;
            foreach (var (from, to) in EmoticonReplacements)
            {
                if (pos + from.Length > text.Length) continue;
                if (!text.AsSpan(pos).StartsWith(from.AsSpan(), StringComparison.Ordinal)) continue;

                // Boundary check: char before must not be letter/digit
                bool okBefore = pos == 0 || !char.IsLetterOrDigit(text[pos - 1]);
                int  afterIdx = pos + from.Length;
                bool okAfter  = afterIdx >= text.Length || !char.IsLetterOrDigit(text[afterIdx]);

                if (okBefore && okAfter)
                {
                    sb.Append(to);
                    pos += from.Length;
                    matched = true;
                    break;  // longest match wins (table is ordered longest-first per trigger)
                }
            }
            if (!matched)
                sb.Append(text[pos++]);
        }

        return sb.Length == text.Length && sb.ToString() == text ? text : sb.ToString();
    }

    private static string SmartQuotes(string text)
    {
        if (!text.Contains('"')) return text;
        var sb = new StringBuilder(text.Length);
        bool afterOpen = true;
        foreach (char c in text)
        {
            if (c == '"') { sb.Append(afterOpen ? '\u201C' : '\u201D'); afterOpen = false; }
            else { sb.Append(c); afterOpen = char.IsWhiteSpace(c) || c is '(' or '[' or '\u201C'; }
        }
        return sb.ToString();
    }

    private static string SmartSingleQuotes(string text)
    {
        if (!text.Contains('\'')) return text;
        var sb = new StringBuilder(text.Length);
        bool afterOpen = true;
        foreach (char c in text)
        {
            if (c == '\'') { sb.Append(afterOpen ? '\u2018' : '\u2019'); afterOpen = false; }
            else { sb.Append(c); afterOpen = char.IsWhiteSpace(c) || c is '(' or '[' or '\u2018'; }
        }
        return sb.ToString();
    }

    // ── Delimiter stack entry (for */_/~~ emphasis) ────────────────────
    private sealed class Delimiter
    {
        public int NodeIndex; public char Marker; public int Length;
        public bool CanOpen; public bool CanClose; public bool Active = true;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Main entry point
    // ══════════════════════════════════════════════════════════════════════

    public static List<MarkdownInline> Parse(string raw, Dictionary<string, LinkReferenceDefinition> refs)
    {
        var nodes      = new List<MarkdownInline>();
        var delimiters = new List<Delimiter>();
        int i = 0, n = raw.Length;
        var plain = new StringBuilder();

        void FlushPlain()
        {
            if (plain.Length == 0) return;
            string t = ApplyTypography(plain.ToString());
            plain.Clear();
            if (t.Length > 0) nodes.Add(new TextInline { Text = t });
        }

        while (i < n)
        {
            char c = raw[i];

            // ── Hard/soft line breaks ──
            if (c == '\n')
            {
                bool hard = false;
                if (plain.Length > 0 && plain[^1] == '\\') { plain.Length--; hard = true; }
                else { int t2 = 0; while (t2 < plain.Length && plain[plain.Length - t2 - 1] == ' ') t2++; if (t2 >= 2) { plain.Length -= t2; hard = true; } }
                FlushPlain();
                nodes.Add(new LineBreakInline { Hard = hard });
                i++; continue;
            }

            // ── Escape ──
            if (c == '\\' && i + 1 < n && IsAsciiPunct(raw[i + 1]))
            { plain.Append(raw[i + 1]); i += 2; continue; }

            // ── HTML entity ──
            if (c == '&')
            {
                var m = EntityRegex.Match(raw, i);
                if (m.Success && m.Index == i) { plain.Append(DecodeEntity(m.Groups[1].Value)); i += m.Length; continue; }
            }

            // ── Code span ──
            if (c == '`')
            {
                int runStart = i, runLen = 0;
                while (i < n && raw[i] == '`') { i++; runLen++; }
                int closeAt = FindExactBacktickRun(raw, i, runLen);
                if (closeAt < 0) { plain.Append(raw, runStart, i - runStart); continue; }
                FlushPlain();
                string code = raw.Substring(i, closeAt - i).Replace('\n', ' ');
                if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim().Length > 0)
                    code = code[1..^1];
                nodes.Add(new CodeSpanInline { Code = code });
                i = closeAt + runLen; continue;
            }

            // ── Angle constructs: autolinks + safelisted HTML ──
            if (c == '<')
            {
                int consumed = TryParseAngleConstruct(raw, i, nodes, plain, refs, FlushPlain);
                if (consumed > 0) { i += consumed; continue; }
            }

            // ── Bare URLs ──
            if (c is 'h' or 'w')
            {
                var m = BareUrlRegex.Match(raw, i);
                if (m.Success && m.Index == i)
                {
                    string url = TrimTrailingPunct(m.Value);
                    FlushPlain();
                    string href = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + url : url;
                    nodes.Add(new AutoLinkInline { Url = href, DisplayText = url });
                    i += url.Length; continue;
                }
            }

            // ── Emoji shortcode :name: ──
            if (c == ':' && i + 2 < n)
            {
                int end = raw.IndexOf(':', i + 1);
                if (end > i + 1 && end - i - 1 <= 40)
                {
                    string code = raw.Substring(i + 1, end - i - 1);
                    if (IsValidEmojiName(code))
                    {
                        string? emoji = MarkdownEmojiTable.Lookup(code);
                        if (emoji != null)
                        { FlushPlain(); nodes.Add(new TextInline { Text = emoji }); i = end + 1; continue; }
                    }
                }
            }

            // ── Image ──
            if (c == '!' && i + 1 < n && raw[i + 1] == '[')
            {
                int consumed = TryParseImage(raw, i, refs, nodes, FlushPlain);
                if (consumed > 0) { i += consumed; continue; }
            }

            // ── Footnote reference [^label] ──
            if (c == '[' && i + 2 < n && raw[i + 1] == '^')
            {
                int close = raw.IndexOf(']', i + 2);
                if (close > i + 2)
                {
                    string label = raw.Substring(i + 2, close - i - 2).Trim();
                    if (label.Length > 0 && !label.Contains('[') && !label.Contains('\n'))
                    {
                        FlushPlain();
                        // Look up ordinal from refs (registered by parser as "__fnref__label" → number)
                        int num = refs != null && refs.TryGetValue($"__fnref__{label}", out var refDef)
                            ? (int.TryParse(refDef.Title, out int n2) ? n2 : 0) : 0;
                        nodes.Add(new FootnoteRefInline { Label = label, Number = num });
                        i = close + 1; continue;
                    }
                }
            }

            // ── Link ──
            if (c == '[')
            {
                int consumed = TryParseLink(raw, i, refs, nodes, delimiters, FlushPlain);
                if (consumed > 0) { i += consumed; continue; }
            }

            // ── Superscript ^text^ ──
            if (c == '^')
            {
                int close = FindPairedChar(raw, i + 1, '^');
                if (close > i + 1)
                {
                    FlushPlain();
                    string inner = raw.Substring(i + 1, close - i - 1);
                    nodes.Add(new SuperscriptInline { Children = Parse(inner, refs) });
                    i = close + 1; continue;
                }
            }

            // ── Highlight ==text== ──
            if (c == '=' && i + 1 < n && raw[i + 1] == '=')
            {
                int close = raw.IndexOf("==", i + 2, StringComparison.Ordinal);
                if (close > i + 1)
                {
                    FlushPlain();
                    string inner = raw.Substring(i + 2, close - i - 2);
                    nodes.Add(new MarkInline { Children = Parse(inner, refs) });
                    i = close + 2; continue;
                }
            }

            // ── Underline ++text++ ──
            if (c == '+' && i + 1 < n && raw[i + 1] == '+')
            {
                int close = raw.IndexOf("++", i + 2, StringComparison.Ordinal);
                if (close > i + 1)
                {
                    FlushPlain();
                    string inner = raw.Substring(i + 2, close - i - 2);
                    nodes.Add(new InsertInline { Children = Parse(inner, refs) });
                    i = close + 2; continue;
                }
            }

            // ── Math $$text$$ / $text$ ──
            if (c == '$' && i + 1 < n && raw[i + 1] == '$')
            {
                int close = FindDisplayMathClose(raw, i + 2);
                if (close > i + 2)
                {
                    FlushPlain();
                    string inner = raw.Substring(i + 2, close - i - 2);
                    nodes.Add(new MathInline { Latex = inner });
                    i = close + 2; continue;
                }
            }

            // ── Math $text$ (single dollar) ──
            if (c == '$' && (i + 1 >= n || raw[i + 1] != '$'))
            {
                int close = FindMathClose(raw, i + 1);
                if (close > i + 1)
                {
                    FlushPlain();
                    string inner = raw.Substring(i + 1, close - i - 1);
                    nodes.Add(new MathInline { Latex = inner });
                    i = close + 1; continue;
                }
            }

            // ── Emphasis / Strikethrough / Subscript (*/_/~/~~) ──
            if (c is '*' or '_' or '~')
            {
                int start = i;
                int runLen = 0;
                while (i < n && raw[i] == c) { i++; runLen++; }

                // Single tilde → subscript (scan-based, not delimiter stack)
                if (c == '~' && runLen == 1)
                {
                    int close = FindPairedChar(raw, i, '~');
                    // Make sure it's truly a single closing tilde (not ~~)
                    if (close > start + 1 && (close + 1 >= raw.Length || raw[close + 1] != '~'))
                    {
                        FlushPlain();
                        string inner = raw.Substring(i, close - i);
                        nodes.Add(new SubscriptInline { Children = Parse(inner, refs) });
                        i = close + 1; continue;
                    }
                    // No valid closing single tilde → treat as literal
                    plain.Append('~'); continue;
                }

                // Double+ tilde → strikethrough delimiter
                if (c == '~' && runLen < 2) { plain.Append(c, runLen); continue; }

                char before = start > 0 ? raw[start - 1] : ' ';
                char after  = i < n ? raw[i] : ' ';
                bool bWs = char.IsWhiteSpace(before), aWs = char.IsWhiteSpace(after);
                bool bPt = IsAsciiPunct(before),       aPt = IsAsciiPunct(after);

                bool leftFlanking  = !aWs && (!aPt || bWs || bPt);
                bool rightFlanking = !bWs && (!bPt || aWs || aPt);

                bool canOpen  = leftFlanking;
                bool canClose = rightFlanking;
                if (c == '_')
                { canOpen = leftFlanking && (!rightFlanking || bPt); canClose = rightFlanking && (!leftFlanking || aPt); }

                FlushPlain();
                nodes.Add(new TextInline { Text = new string(c, runLen) });
                delimiters.Add(new Delimiter { NodeIndex = nodes.Count - 1, Marker = c, Length = runLen, CanOpen = canOpen, CanClose = canClose });
                continue;
            }

            plain.Append(c); i++;
        }

        FlushPlain();
        ResolveEmphasis(nodes, delimiters);
        return nodes;
    }

    private static bool IsValidEmojiName(string s)
    {
        foreach (char c in s) if (!char.IsLetterOrDigit(c) && c != '_' && c != '+' && c != '-') return false;
        return s.Length > 0;
    }

    private static int FindPairedChar(string raw, int from, char ch)
    {
        for (int i = from; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length) { i++; continue; }
            if (raw[i] == ch) return i;
        }
        return -1;
    }

    private static int FindMathClose(string raw, int from)
    {
        for (int i = from; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length) { i++; continue; }
            if (raw[i] == '$' && (i + 1 >= raw.Length || raw[i + 1] != '$')) return i;
        }
        return -1;
    }

    private static int FindDisplayMathClose(string raw, int from)
    {
        for (int i = from; i + 1 < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length) { i++; continue; }
            if (raw[i] == '$' && raw[i + 1] == '$') return i;
        }
        return -1;
    }

    private static int FindExactBacktickRun(string raw, int from, int runLen)
    {
        string target = new('`', runLen);
        int idx = from;
        while (true)
        {
            int found = raw.IndexOf(target, idx, StringComparison.Ordinal);
            if (found < 0) return -1;
            int after = found + runLen;
            if (after >= raw.Length || raw[after] != '`') return found;
            idx = after;
        }
    }

    // ── Delimiter stack resolution ──────────────────────────────────────

    private static void ResolveEmphasis(List<MarkdownInline> nodes, List<Delimiter> delimiters)
    {
        var openersBottom = new Dictionary<char, int>();

        for (int closeIdx = 0; closeIdx < delimiters.Count; closeIdx++)
        {
            var closer = delimiters[closeIdx];
            if (!closer.Active || !closer.CanClose || closer.Length <= 0) continue;

            int bottom  = openersBottom.TryGetValue(closer.Marker, out var b) ? b : 0;
            int openIdx = -1;
            for (int k = closeIdx - 1; k >= bottom; k--)
            {
                var cand = delimiters[k];
                if (!cand.Active || cand.Marker != closer.Marker || !cand.CanOpen || cand.Length <= 0) continue;
                bool oddRule = (cand.CanOpen && cand.CanClose) || (closer.CanOpen && closer.CanClose);
                if (oddRule && (cand.Length + closer.Length) % 3 == 0 && cand.Length % 3 != 0 && closer.Length % 3 != 0) continue;
                openIdx = k; break;
            }

            if (openIdx < 0) { openersBottom[closer.Marker] = closeIdx; if (!closer.CanOpen) closer.Active = false; continue; }

            var opener = delimiters[openIdx];
            for (int k = openIdx + 1; k < closeIdx; k++) delimiters[k].Active = false;

            int use = closer.Marker == '~' ? 2 : Math.Min(2, Math.Min(opener.Length, closer.Length));
            use = Math.Min(use, Math.Min(opener.Length, closer.Length));

            int openNode  = opener.NodeIndex;
            int closeNode = closer.NodeIndex;

            var children = new List<MarkdownInline>();
            for (int idx = openNode + 1; idx < closeNode; idx++) if (nodes[idx] is not null) children.Add(nodes[idx]);

            MarkdownInline wrapper = (use >= 2)
                ? (closer.Marker == '~' ? (MarkdownInline)new StrikethroughInline { Children = children } : new StrongInline { Children = children })
                : new EmphasisInline { Children = children };

            for (int idx = openNode + 1; idx < closeNode; idx++) nodes[idx] = null!;

            opener.Length -= use; closer.Length -= use;
            ((TextInline)nodes[openNode]!).Text  = new string(opener.Marker, opener.Length);
            var closerText = (TextInline)nodes[closeNode]!;
            closerText.Text = new string(closer.Marker, closer.Length);
            nodes[closeNode] = closerText;
            nodes.Insert(closeNode, wrapper);
            foreach (var d in delimiters) if (d.NodeIndex >= closeNode) d.NodeIndex++;

            if (opener.Length == 0) opener.Active = false;
            if (closer.Length == 0) closer.Active = false; else closeIdx--;
        }

        for (int idx = nodes.Count - 1; idx >= 0; idx--)
        {
            if (nodes[idx] is null) { nodes.RemoveAt(idx); continue; }
            if (nodes[idx] is TextInline t && t.Text.Length == 0) nodes.RemoveAt(idx);
        }
    }

    // ── Link / Image parsing ────────────────────────────────────────────

    private static int TryParseLink(string raw, int start, Dictionary<string, LinkReferenceDefinition> refs,
        List<MarkdownInline> nodes, List<Delimiter> delimiters, Action flush)
    {
        int closeBracket = FindMatchingBracket(raw, start);
        if (closeBracket < 0) return 0;
        string label = raw.Substring(start + 1, closeBracket - start - 1);
        int after = closeBracket + 1;

        if (after < raw.Length && raw[after] == '(')
        {
            if (TryParseInlineDest(raw, after, out string url, out string? title, out int clen))
            {
                flush();
                nodes.Add(new LinkInline { Url = url, Title = title, Children = Parse(label, refs) });
                return after + clen - start;
            }
        }

        string refLabel = label; int refConsumed = closeBracket + 1 - start;
        if (after < raw.Length && raw[after] == '[')
        {
            int closeRef = FindMatchingBracket(raw, after);
            if (closeRef > after)
            {
                string expLabel = raw.Substring(after + 1, closeRef - after - 1);
                if (expLabel.Length > 0) refLabel = expLabel;
                refConsumed = closeRef + 1 - start;
            }
        }
        if (refs.TryGetValue(NormLabel(refLabel), out var def))
        {
            flush(); nodes.Add(new LinkInline { Url = def.Url, Title = def.Title, Children = Parse(label, refs) });
            return refConsumed;
        }
        return 0;
    }

    private static int TryParseImage(string raw, int start, Dictionary<string, LinkReferenceDefinition> refs,
        List<MarkdownInline> nodes, Action flush)
    {
        int bracketStart = start + 1;
        int closeBracket = FindMatchingBracket(raw, bracketStart);
        if (closeBracket < 0) return 0;
        string alt     = raw.Substring(bracketStart + 1, closeBracket - bracketStart - 1);
        string cleanAlt = StripMarkup(alt);
        int after = closeBracket + 1;

        if (after < raw.Length && raw[after] == '(')
        {
            if (TryParseInlineDest(raw, after, out string url, out string? title, out int clen))
            { flush(); nodes.Add(new ImageInline { Url = url, Title = title, AltText = cleanAlt }); return after + clen - start; }
        }

        // Full [alt][id] / collapsed [alt][] / shortcut [alt]
        string refLabel = alt; int refConsumed = closeBracket + 1 - start;
        if (after < raw.Length && raw[after] == '[')
        {
            int closeRef = FindMatchingBracket(raw, after);
            if (closeRef > after)
            {
                string expLabel = raw.Substring(after + 1, closeRef - after - 1);
                if (expLabel.Length > 0) refLabel = expLabel;
                refConsumed = closeRef + 1 - start;
            }
        }
        if (refs.TryGetValue(NormLabel(refLabel), out var def))
        { flush(); nodes.Add(new ImageInline { Url = def.Url, Title = def.Title, AltText = cleanAlt }); return refConsumed; }
        return 0;
    }

    private static bool TryParseInlineDest(string raw, int parenStart, out string url, out string? title, out int consumedLen)
    {
        url = ""; title = null; consumedLen = 0;
        int i = parenStart + 1, n = raw.Length;
        while (i < n && raw[i] == ' ') i++;
        var ub = new StringBuilder();
        if (i < n && raw[i] == '<')
        { i++; while (i < n && raw[i] != '>') { ub.Append(raw[i]); i++; } if (i >= n) return false; i++; }
        else { int depth = 0; while (i < n) { char c = raw[i]; if (c == '(') depth++; else if (c == ')') { if (depth == 0) break; depth--; } else if (char.IsWhiteSpace(c)) break; ub.Append(c); i++; } }
        url = ub.ToString();
        while (i < n && raw[i] == ' ') i++;
        if (i < n && (raw[i] == '"' || raw[i] == '\''))
        {
            char q = raw[i]; i++;
            var tb = new StringBuilder();
            while (i < n && raw[i] != q) { tb.Append(raw[i]); i++; }
            if (i >= n) return false; i++;
            title = tb.ToString();
            while (i < n && raw[i] == ' ') i++;
        }
        if (i >= n || raw[i] != ')') return false;
        i++; consumedLen = i - parenStart; return true;
    }

    private static int FindMatchingBracket(string raw, int open)
    {
        int depth = 0;
        for (int i = open; i < raw.Length; i++)
        {
            if (raw[i] == '\\') { i++; continue; }
            if (raw[i] == '[') depth++;
            else if (raw[i] == ']') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    private static string NormLabel(string l) => Regex.Replace(l.Trim(), @"\s+", " ").ToLowerInvariant();
    private static string StripMarkup(string t) => Regex.Replace(t, @"[\[\]*_`~^=+]", "");

    // ── Angle constructs (autolinks + safelisted HTML) ──────────────────

    private static readonly Regex AutoLinkAngle      = new(@"\G<([a-zA-Z][a-zA-Z0-9+.-]{1,31}:[^\s<>]*)>",  RegexOptions.Compiled);
    private static readonly Regex AutoLinkEmailAngle = new(@"\G<([A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,})>", RegexOptions.Compiled);

    private static int TryParseAngleConstruct(string raw, int i, List<MarkdownInline> nodes,
        StringBuilder plain, Dictionary<string, LinkReferenceDefinition> refs, Action flush)
    {
        var m = AutoLinkAngle.Match(raw, i);
        if (m.Success && m.Index == i)
        { flush(); nodes.Add(new AutoLinkInline { Url = m.Groups[1].Value, DisplayText = m.Groups[1].Value }); return m.Length; }

        m = AutoLinkEmailAngle.Match(raw, i);
        if (m.Success && m.Index == i)
        { flush(); nodes.Add(new AutoLinkInline { Url = "mailto:" + m.Groups[1].Value, DisplayText = m.Groups[1].Value }); return m.Length; }

        if (TryParseHtmlImage(raw, i, out var image, out int imageLen))
        {
            flush();
            nodes.Add(image);
            return imageLen;
        }

        if (TryParseHtmlSource(raw, i, out var source, out int sourceLen))
        {
            flush();
            nodes.Add(source);
            return sourceLen;
        }

        if (TryParseHtmlPicture(raw, i, out var picture, out int pictureLen))
        {
            flush();
            nodes.Add(picture);
            return pictureLen;
        }

        if (TryParseHtmlAnchor(raw, i, refs, out var link, out int linkLen))
        {
            flush();
            nodes.Add(link);
            return linkLen;
        }

        if (TryParseHtmlLinkTag(raw, i, out var linkTag, out int linkTagLen))
        {
            flush();
            nodes.Add(linkTag);
            return linkTagLen;
        }

        // Named inline HTML tags we handle structurally
        if (TryMatchOpenTag(raw, i, "span", out int spanLen))
        {
            int close = raw.IndexOf("</span>", i + spanLen, StringComparison.OrdinalIgnoreCase);
            if (close >= 0)
            {
                string inner = raw.Substring(i + spanLen, close - (i + spanLen));
                var span = ParseStyleTag(raw.Substring(i, spanLen));
                span.Children = Parse(inner, refs);
                flush();
                nodes.Add(span);
                return close + 7 - i;
            }
        }

        foreach (var (tag, factory) in InlineHtmlTags(refs))
        {
            if (!TryMatchOpenTag(raw, i, tag, out int openLen)) continue;
            string closeTag = $"</{tag}>";
            int close = raw.IndexOf(closeTag, i + openLen, StringComparison.OrdinalIgnoreCase);
            if (close < 0) continue;
            flush();
            string inner = raw.Substring(i + openLen, close - (i + openLen));
            var node = factory(inner);
            var style = ParseStyleTag(raw.Substring(i, openLen));
            if (HasStyle(style))
            {
                style.Children.Add(node);
                nodes.Add(style);
            }
            else
            {
                nodes.Add(node);
            }
            return close + closeTag.Length - i;
        }

        if (TryMatchOpenAnyTag(raw, i, out string anyTag, out int anyOpenLen))
        {
            string closeTag = $"</{anyTag}>";
            int close = raw.IndexOf(closeTag, i + anyOpenLen, StringComparison.OrdinalIgnoreCase);
            if (close >= 0)
            {
                string inner = raw.Substring(i + anyOpenLen, close - (i + anyOpenLen));
                var style = ParseStyleTag(raw.Substring(i, anyOpenLen));
                style.Children = Parse(inner, refs);
                flush();
                nodes.Add(style);
                return close + closeTag.Length - i;
            }
        }

        // <br/>
        if (TryMatchTag(raw, i, "br")) { flush(); nodes.Add(new LineBreakInline { Hard = true }); return TagLen(raw, i); }

        // Drop unknown tags silently
        if (TryMatchAnyTag(raw, i, out int anyLen)) return anyLen;
        return 0;
    }

    private static bool TryParseHtmlAnchor(string raw, int i, Dictionary<string, LinkReferenceDefinition> refs,
        out LinkInline link, out int length)
    {
        link = new LinkInline();
        length = 0;
        if (!TryMatchOpenTag(raw, i, "a", out int openLen)) return false;

        int close = raw.IndexOf("</a>", i + openLen, StringComparison.OrdinalIgnoreCase);
        if (close < 0) return false;

        var attrs = ParseHtmlAttributes(raw.Substring(i, openLen));
        if (!TryGetNonEmptyAttribute(attrs, "href", out string href)) return false;

        string inner = raw.Substring(i + openLen, close - (i + openLen));
        string? title = GetAttribute(attrs, "title");
        link = new LinkInline { Url = DecodeHtmlAttribute(href), Title = title, Children = Parse(inner, refs) };
        length = close + 4 - i;
        return true;
    }

    private static bool TryParseHtmlImage(string raw, int i, out ImageInline image, out int length)
    {
        image = new ImageInline();
        length = 0;

        if (!TryMatchVoidTag(raw, i, "img", out string tagText, out length)) return false;
        var attrs = ParseHtmlAttributes(tagText);
        if (!TryGetNonEmptyAttribute(attrs, "src", out string src)) return false;

        string? alt = GetAttribute(attrs, "alt");
        string? title = GetAttribute(attrs, "title");
        image = new ImageInline
        {
            Url = DecodeHtmlAttribute(src),
            AltText = DecodeHtmlAttribute(alt ?? ""),
            Title = title == null ? null : DecodeHtmlAttribute(title)
        };
        return true;
    }

    private static bool TryParseHtmlLinkTag(string raw, int i, out LinkInline link, out int length)
    {
        link = new LinkInline();
        length = 0;
        if (!TryMatchVoidTag(raw, i, "link", out string tagText, out length)) return false;

        var attrs = ParseHtmlAttributes(tagText);
        if (!TryGetNonEmptyAttribute(attrs, "href", out string href)) return false;

        string? title = GetAttribute(attrs, "title");
        string label = !string.IsNullOrWhiteSpace(title)
            ? title
            : href;
        link = new LinkInline
        {
            Url = DecodeHtmlAttribute(href),
            Title = title == null ? null : DecodeHtmlAttribute(title),
            Children = new List<MarkdownInline> { new TextInline { Text = DecodeHtmlAttribute(label) } }
        };
        return true;
    }

    private static bool TryParseHtmlSource(string raw, int i, out ImageInline image, out int length)
    {
        image = new ImageInline();
        length = 0;

        if (!TryMatchVoidTag(raw, i, "source", out string tagText, out length)) return false;
        var attrs = ParseHtmlAttributes(tagText);
        string? src = GetAttribute(attrs, "src");
        if (string.IsNullOrWhiteSpace(src) && TryGetNonEmptyAttribute(attrs, "srcset", out string srcset))
            src = FirstSrcSetUrl(srcset);
        if (string.IsNullOrWhiteSpace(src)) return false;

        string? alt = GetAttribute(attrs, "alt");
        string? title = GetAttribute(attrs, "title");
        image = new ImageInline
        {
            Url = DecodeHtmlAttribute(src),
            AltText = DecodeHtmlAttribute(alt ?? ""),
            Title = title == null ? null : DecodeHtmlAttribute(title)
        };
        return true;
    }

    private static bool TryParseHtmlPicture(string raw, int i, out ImageInline image, out int length)
    {
        image = new ImageInline();
        length = 0;
        if (!TryMatchOpenTag(raw, i, "picture", out int openLen)) return false;

        int close = raw.IndexOf("</picture>", i + openLen, StringComparison.OrdinalIgnoreCase);
        if (close < 0) return false;

        string inner = raw.Substring(i + openLen, close - (i + openLen));
        length = close + "</picture>".Length - i;

        if (TryFindHtmlImageSource(inner, preferImg: true, out image)) return true;
        if (TryFindHtmlImageSource(inner, preferImg: false, out image)) return true;
        return false;
    }

    private static bool TryFindHtmlImageSource(string html, bool preferImg, out ImageInline image)
    {
        image = new ImageInline();
        string tagName = preferImg ? "img" : "source";
        int pos = 0;
        while (pos < html.Length)
        {
            int idx = html.IndexOf("<" + tagName, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            if (TryMatchVoidTag(html, idx, tagName, out string tagText, out int tagLen))
            {
                var attrs = ParseHtmlAttributes(tagText);
                string? src = null;
                if (preferImg)
                    src = GetAttribute(attrs, "src");
                else if (!TryGetNonEmptyAttribute(attrs, "src", out src) && TryGetNonEmptyAttribute(attrs, "srcset", out string srcset))
                    src = FirstSrcSetUrl(srcset);

                if (!string.IsNullOrWhiteSpace(src))
                {
                    string? alt = GetAttribute(attrs, "alt");
                    string? title = GetAttribute(attrs, "title");
                    image = new ImageInline
                    {
                        Url = DecodeHtmlAttribute(src),
                        AltText = DecodeHtmlAttribute(alt ?? ""),
                        Title = title == null ? null : DecodeHtmlAttribute(title)
                    };
                    return true;
                }
                pos = idx + tagLen;
            }
            else
            {
                pos = idx + tagName.Length + 1;
            }
        }
        return false;
    }

    private static string? GetAttribute(Dictionary<string, string> attrs, string name) =>
        attrs.TryGetValue(name, out var value) ? value : null;

    private static bool TryGetNonEmptyAttribute(Dictionary<string, string> attrs, string name, out string value)
    {
        value = "";
        if (!attrs.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw)) return false;
        value = raw;
        return true;
    }

    private static IEnumerable<(string Tag, Func<string, MarkdownInline> Factory)> InlineHtmlTags(
        Dictionary<string, LinkReferenceDefinition> refs)
    {
        yield return ("ins",    inner => new InsertInline    { Children = Parse(inner, refs) });
        yield return ("mark",   inner => new MarkInline      { Children = Parse(inner, refs) });
        yield return ("strong", inner => new StrongInline    { Children = Parse(inner, refs) });
        yield return ("b",      inner => new StrongInline    { Children = Parse(inner, refs) });
        yield return ("em",     inner => new EmphasisInline  { Children = Parse(inner, refs) });
        yield return ("i",      inner => new EmphasisInline  { Children = Parse(inner, refs) });
        yield return ("del",    inner => new StrikethroughInline { Children = Parse(inner, refs) });
        yield return ("s",      inner => new StrikethroughInline { Children = Parse(inner, refs) });
        yield return ("strike", inner => new StrikethroughInline { Children = Parse(inner, refs) });
        yield return ("u",      inner => new InsertInline    { Children = Parse(inner, refs) });
        yield return ("sub",    inner => new SubscriptInline   { Children = Parse(inner, refs) });
        yield return ("sup",    inner => new SuperscriptInline { Children = Parse(inner, refs) });
        yield return ("kbd",    inner => new InlineHtmlInline  { TagName = "kbd", Children = Parse(inner, refs) });
        yield return ("code",   inner => (MarkdownInline)new CodeSpanInline { Code = inner });
    }

    private static bool TryMatchTag(string raw, int i, string name)
    {
        string p = "<" + name; if (!raw.AsSpan(i).StartsWith(p, StringComparison.OrdinalIgnoreCase)) return false;
        int j = i + p.Length; while (j < raw.Length && raw[j] != '>') j++; return j < raw.Length;
    }
    private static int TagLen(string raw, int i) { int j = i; while (j < raw.Length && raw[j] != '>') j++; return j < raw.Length ? j - i + 1 : raw.Length - i; }
    private static bool TryMatchOpenTag(string raw, int i, string name, out int length)
    {
        length = 0;
        string p = "<" + name;
        if (i + p.Length > raw.Length) return false;
        if (!raw.AsSpan(i, p.Length).Equals(p.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
        int j = i + p.Length;
        if (j < raw.Length && raw[j] != '>' && raw[j] != ' ') return false;
        while (j < raw.Length && raw[j] != '>') j++;
        if (j >= raw.Length) return false;
        length = j - i + 1; return true;
    }
    private static bool TryMatchAnyTag(string raw, int i, out int length)
    {
        length = 0; var m = Regex.Match(raw[i..], @"^</?[a-zA-Z][a-zA-Z0-9-]*(\s+[^<>]*)?/?>"); if (!m.Success) return false; length = m.Length; return true;
    }

    private static bool TryMatchOpenAnyTag(string raw, int i, out string tag, out int length)
    {
        tag = "";
        length = 0;
        var m = Regex.Match(raw[i..], @"^<([a-zA-Z][a-zA-Z0-9-]*)(\s+[^<>]*)?>");
        if (!m.Success) return false;
        tag = m.Groups[1].Value;
        length = m.Length;
        return true;
    }

    private static bool TryMatchVoidTag(string raw, int i, string name, out string tagText, out int length)
    {
        tagText = "";
        length = 0;
        string prefix = "<" + name;
        if (i + prefix.Length > raw.Length) return false;
        if (!raw.AsSpan(i, prefix.Length).Equals(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;

        int j = i + prefix.Length;
        if (j < raw.Length && raw[j] != '>' && !char.IsWhiteSpace(raw[j]) && raw[j] != '/') return false;
        char quote = '\0';
        while (j < raw.Length)
        {
            char c = raw[j];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c == '"' || c == '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                length = j - i + 1;
                tagText = raw.Substring(i, length);
                return true;
            }
            j++;
        }
        return false;
    }

    private static Dictionary<string, string> ParseHtmlAttributes(string tagText)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = Regex.Matches(tagText,
            @"([a-zA-Z_:][a-zA-Z0-9_:.-]*)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'=<>`]+))");
        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            string value = match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Success ? match.Groups[3].Value
                : match.Groups[4].Value;
            attrs[name] = DecodeHtmlAttribute(value);
        }
        return attrs;
    }

    private static string DecodeHtmlAttribute(string value) =>
        EntityRegex.Replace(value, m => DecodeEntity(m.Groups[1].Value));

    private static string FirstSrcSetUrl(string srcset)
    {
        foreach (var candidate in srcset.Split(','))
        {
            string trimmed = candidate.Trim();
            if (trimmed.Length == 0) continue;
            int space = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return space > 0 ? trimmed[..space] : trimmed;
        }
        return "";
    }

    private static string TrimTrailingPunct(string url)
    {
        while (url.Length > 0 && ".,;:!?".IndexOf(url[^1]) >= 0) url = url[..^1];
        if (url.EndsWith(')') && url.Count(ch => ch == '(') < url.Count(ch => ch == ')')) url = url[..^1];
        return url;
    }
    private static bool IsAsciiPunct(char c) => (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');
    private static string DecodeEntity(string body)
    {
        if (body.StartsWith("#x", StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(body[2..], System.Globalization.NumberStyles.HexNumber, null, out int hc)) return char.ConvertFromUtf32(hc); }
        else if (body.StartsWith('#')) { if (int.TryParse(body[1..], out int dc)) return char.ConvertFromUtf32(dc); }
        else if (NamedEntities.TryGetValue(body, out char ch)) return ch.ToString();
        return "&" + body + ";";
    }

    private static SpanInline ParseStyleTag(string raw)
    {
        var span = new SpanInline();
        int styleStart = raw.IndexOf("style=", StringComparison.OrdinalIgnoreCase);
        if (styleStart < 0) return span;
        int quote = raw.IndexOfAny(new[] { '"', '\'' }, styleStart + 6);
        if (quote < 0) return span;
        char quoteChar = raw[quote];
        int styleEnd = raw.IndexOf(quoteChar, quote + 1);
        if (styleEnd < 0) return span;
        string styleValue = raw.Substring(quote + 1, styleEnd - quote - 1);
        foreach (var part in styleValue.Split(';'))
        {
            int colon = part.IndexOf(':');
            if (colon <= 0) continue;
            string key = part[..colon].Trim().ToLowerInvariant();
            string val = part[(colon + 1)..].Trim();
            if (key == "color")
            {
                var color = ParseColor(val);
                if (color.HasValue) span.Color = color.Value;
            }
            else if (key == "background-color" || key == "background")
            {
                var color = ParseColor(val);
                if (color.HasValue) span.Background = color.Value;
            }
            else if (key == "font-weight")
            {
                string lower = val.ToLowerInvariant();
                if (lower is "bold" or "bolder" || (int.TryParse(lower, out int weight) && weight >= 600))
                    span.Bold = true;
            }
            else if (key == "font-style")
            {
                if (val.Equals("italic", StringComparison.OrdinalIgnoreCase) || val.Equals("oblique", StringComparison.OrdinalIgnoreCase))
                    span.Italic = true;
            }
            else if (key == "text-decoration" || key == "text-decoration-line")
            {
                string lower = val.ToLowerInvariant();
                if (lower.Contains("underline", StringComparison.Ordinal)) span.Insert = true;
                if (lower.Contains("line-through", StringComparison.Ordinal)) span.Strike = true;
            }
            else if (key == "display")
            {
                if (val.Equals("mark", StringComparison.OrdinalIgnoreCase)) span.Mark = true;
            }
        }
        return span;
    }

    private static bool HasStyle(SpanInline span) =>
        span.Color.HasValue || span.Background.HasValue || span.Bold.HasValue || span.Italic.HasValue ||
        span.Strike.HasValue || span.Insert.HasValue || span.Mark.HasValue;

    private static SKColor? ParseColor(string val)
    {
        val = val.Trim();
        if (val.StartsWith("#") && val.Length == 7)
        {
            return new SKColor(
                Convert.ToByte(val.Substring(1, 2), 16),
                Convert.ToByte(val.Substring(3, 2), 16),
                Convert.ToByte(val.Substring(5, 2), 16));
        }
        if (val.StartsWith("#") && val.Length == 4)
        {
            return new SKColor(
                Convert.ToByte(new string(val[1], 2), 16),
                Convert.ToByte(new string(val[2], 2), 16),
                Convert.ToByte(new string(val[3], 2), 16));
        }
        if ((val.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) ||
             val.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase)) && val.EndsWith(')'))
        {
            int open = val.IndexOf('(');
            var parts = val.Substring(open + 1, val.Length - open - 2).Split(',');
            if (parts.Length >= 3)
            {
                return new SKColor(
                    ParseCssByte(parts[0]),
                    ParseCssByte(parts[1]),
                    ParseCssByte(parts[2]),
                    parts.Length >= 4 ? ParseCssAlpha(parts[3]) : (byte)255);
            }
        }
        if (val.Length > 0 && char.IsDigit(val[0]))
        {
            var parts = val.Split(',');
            if (parts.Length == 3)
            {
                return new SKColor(
                    byte.Parse(parts[0].Trim()),
                    byte.Parse(parts[1].Trim()),
                    byte.Parse(parts[2].Trim()));
            }
        }
        return ParseNamedColor(val);
    }

    private static byte ParseCssByte(string value)
    {
        value = value.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal) &&
            float.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pct))
            return (byte)Math.Clamp(pct / 100f * 255f, 0f, 255f);
        return byte.TryParse(value, out byte b) ? b : (byte)0;
    }

    private static byte ParseCssAlpha(string value)
    {
        value = value.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal) &&
            float.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pct))
            return (byte)Math.Clamp(pct / 100f * 255f, 0f, 255f);
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float alpha) && alpha <= 1f)
            return (byte)Math.Clamp(alpha * 255f, 0f, 255f);
        return ParseCssByte(value);
    }

    private static SKColor? ParseNamedColor(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "black" => SKColors.Black,
            "white" => SKColors.White,
            "red" => SKColors.Red,
            "green" => SKColors.Green,
            "blue" => SKColors.Blue,
            "yellow" => SKColors.Yellow,
            "cyan" => new SKColor(0, 255, 255),
            "magenta" => SKColors.Magenta,
            "orange" => new SKColor(255, 165, 0),
            "purple" => new SKColor(128, 0, 128),
            "brown" => new SKColor(165, 42, 42),
            "gray" => new SKColor(128, 128, 128),
            "pink" => new SKColor(255, 192, 203),
            "gold" => new SKColor(255, 215, 0),
            "silver" => new SKColor(192, 192, 192),
            _ => null
        };
    }
}
