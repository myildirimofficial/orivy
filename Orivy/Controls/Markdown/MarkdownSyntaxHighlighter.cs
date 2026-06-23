using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Orivy.Controls.Markdown;

public enum SyntaxKind { Plain, Keyword, String, Comment, Number, Type, Function, Tag, Attribute }

public readonly struct SyntaxToken
{
    public readonly int Start;
    public readonly int Length;
    public readonly SyntaxKind Kind;

    public SyntaxToken(int start, int length, SyntaxKind kind)
    {
        Start = start;
        Length = length;
        Kind = kind;
    }
}

/// <summary>
/// A pragmatic, dependency-free syntax highlighter. It is NOT a real per-language grammar
/// (no AST, no semantic analysis) -- it is a single-pass keyword/string/comment/number
/// tokenizer parameterized per language family, plus a few dedicated lexers (JSON, XML/HTML,
/// CSS) for languages that don't fit the generic "keyword soup" shape. This is intentionally
/// the same trade-off most lightweight embedded code viewers make: it produces good-looking,
/// readable results for the vast majority of real-world snippets at a fraction of the cost of
/// a true grammar-based highlighter (e.g. TextMate grammars / Tree-sitter).
/// </summary>
public static class MarkdownSyntaxHighlighter
{
    private sealed record LanguageDefinition(
        HashSet<string> Keywords,
        HashSet<string> Types,
        string[] LineComments,
        (string Start, string End)[] BlockComments,
        char[] StringDelimiters,
        bool TripleQuoteStrings);

    private static readonly Regex IdentifierRegex = new(@"\G[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"\G(0[xXbBoO][0-9a-fA-F_]+|\d[\d_]*(\.\d+)?([eE][+-]?\d+)?[fFdDmMlLuU]*)", RegexOptions.Compiled);

    private static readonly Dictionary<string, LanguageDefinition> Generic = new(StringComparer.OrdinalIgnoreCase);

    static MarkdownSyntaxHighlighter()
    {
        var cLikeKeywords = Split("if else for while do switch case break continue return goto default " +
            "class struct interface enum namespace using public private protected internal static " +
            "void new this base virtual override abstract sealed readonly const try catch finally " +
            "throw async await null true false in is as out ref params get set partial yield " +
            "import export from typeof instanceof");
        var cLikeTypes = Split("int float double bool string char byte short long uint ulong ushort " +
            "object var decimal void Task List Dictionary IEnumerable string[] String Number Boolean " +
            "any unknown self None");

        Register("csharp", cLikeKeywords, cLikeTypes, new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '\'' }, false);
        Register("cs", Generic["csharp"]);
        Register("java", cLikeKeywords, cLikeTypes, new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '\'' }, false);
        Register("kotlin", cLikeKeywords, cLikeTypes, new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '\'' }, true);
        Register("javascript", Split("function var let const if else for while do switch case break continue " +
                "return class extends new this typeof instanceof null undefined true false try catch finally " +
                "throw async await of in import export from default yield static get set"),
            Split("string number boolean any void Promise Array Map Set Object"),
            new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '\'', '`' }, false);
        Register("js", Generic["javascript"]);
        Register("jsx", Generic["javascript"]);
        Register("typescript", Generic["javascript"].Keywords, Generic["javascript"].Types, new[] { "//" },
            new[] { ("/*", "*/") }, new[] { '"', '\'', '`' }, false);
        Register("ts", Generic["typescript"]);
        Register("tsx", Generic["typescript"]);
        Register("c", cLikeKeywords, Split("int float double char short long unsigned signed void struct"),
            new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '\'' }, false);
        Register("cpp", cLikeKeywords, cLikeTypes, new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '\'' }, false);
        Register("c++", Generic["cpp"]);
        Register("go", Split("func package import var const type struct interface map chan go defer return " +
                "if else for range switch case break continue default select true false nil"),
            Split("string int float64 int64 bool byte rune error"),
            new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"', '`' }, false);
        Register("rust", Split("fn let mut struct enum impl trait pub use mod crate match if else for while " +
                "loop return break continue self Self true false as ref move async await unsafe where dyn"),
            Split("String str i32 i64 u32 u64 f32 f64 bool Vec Option Result Box"),
            new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"' }, false);
        Register("php", Split("function class public private protected static return if else elseif foreach " +
                "for while do switch case break continue new echo print require include namespace use try catch " +
                "finally throw true false null this"),
            Split("string int float bool array object void"),
            new[] { "//", "#" }, new[] { ("/*", "*/") }, new[] { '"', '\'' }, false);
        Register("ruby", Split("def end class module if elsif else unless while until for in do begin rescue " +
                "ensure raise return yield self true false nil require attr_accessor new"),
            Split("String Integer Float Array Hash Symbol"),
            new[] { "#" }, Array.Empty<(string, string)>(), new[] { '"', '\'' }, false);
        Register("python", Split("def class if elif else for while try except finally with as import from " +
                "return yield lambda pass break continue global nonlocal True False None self async await raise " +
                "in is not and or"),
            Split("str int float bool list dict tuple set object bytes"),
            new[] { "#" }, Array.Empty<(string, string)>(), new[] { '"', '\'' }, true);
        Register("py", Generic["python"]);
        Register("swift", Split("func class struct enum protocol extension if else for while guard switch case " +
                "return import var let true false nil self super try catch throw async await in"),
            Split("String Int Double Float Bool Array Dictionary Set Any"),
            new[] { "//" }, new[] { ("/*", "*/") }, new[] { '"' }, false);
        Register("sql", Split("SELECT FROM WHERE INSERT INTO VALUES UPDATE SET DELETE CREATE TABLE ALTER DROP " +
                "JOIN INNER LEFT RIGHT OUTER ON GROUP BY ORDER HAVING AS AND OR NOT NULL IS LIKE IN BETWEEN LIMIT " +
                "DISTINCT UNION PRIMARY KEY FOREIGN REFERENCES DEFAULT VIEW INDEX"),
            Split("INT VARCHAR TEXT BOOLEAN DATE DATETIME FLOAT DOUBLE DECIMAL"),
            new[] { "--" }, new[] { ("/*", "*/") }, new[] { '\'', '"' }, false);
        Register("bash", Split("if then else elif fi for while do done case esac function return exit export local " +
                "echo read true false in"),
            new HashSet<string>(),
            new[] { "#" }, Array.Empty<(string, string)>(), new[] { '"', '\'' }, false);
        Register("sh", Generic["bash"]);
        Register("shell", Generic["bash"]);
        Register("yaml", new HashSet<string>(), new HashSet<string>(), new[] { "#" },
            Array.Empty<(string, string)>(), new[] { '"', '\'' }, false);
        Register("yml", Generic["yaml"]);
    }

    private static void Register(string name, HashSet<string> kw, HashSet<string> types, string[] lc,
        (string, string)[] bc, char[] sd, bool triple) =>
        Generic[name] = new LanguageDefinition(kw, types, lc, bc, sd, triple);

    private static void Register(string alias, LanguageDefinition def) => Generic[alias] = def;

    private static HashSet<string> Split(string s) =>
        new(s.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    /// <summary>Tokenizes source code line by line. Returns one token list per line (lines split on '\n').</summary>
    public static List<List<SyntaxToken>> Tokenize(string code, string? language)
    {
        var lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<List<SyntaxToken>>(lines.Length);

        if (string.IsNullOrWhiteSpace(language))
        {
            foreach (var _ in lines) result.Add(new List<SyntaxToken>());
            return result;
        }

        var lang = language.Trim().ToLowerInvariant();
        if (lang is "json")
        {
            foreach (var line in lines) result.Add(TokenizeJsonLine(line));
            return result;
        }
        if (lang is "html" or "xml" or "svg" or "vue")
        {
            return TokenizeMarkup(lines);
        }
        if (lang is "css" or "scss" or "less")
        {
            foreach (var line in lines) result.Add(TokenizeCssLine(line));
            return result;
        }

        if (!Generic.TryGetValue(lang, out var def))
        {
            foreach (var _ in lines) result.Add(new List<SyntaxToken>());
            return result;
        }

        bool inBlockComment = false;
        string blockCommentEnd = "";
        bool inTripleString = false;
        char tripleQuoteChar = '"';

        foreach (var line in lines)
        {
            var tokens = new List<SyntaxToken>();
            int i = 0;
            int n = line.Length;

            if (inBlockComment)
            {
                int end = line.IndexOf(blockCommentEnd, StringComparison.Ordinal);
                if (end < 0)
                {
                    tokens.Add(new SyntaxToken(0, n, SyntaxKind.Comment));
                    result.Add(tokens);
                    continue;
                }
                tokens.Add(new SyntaxToken(0, end + blockCommentEnd.Length, SyntaxKind.Comment));
                i = end + blockCommentEnd.Length;
                inBlockComment = false;
            }

            if (inTripleString)
            {
                int end = FindTripleQuoteEnd(line, 0, tripleQuoteChar);
                if (end < 0)
                {
                    tokens.Add(new SyntaxToken(0, n, SyntaxKind.String));
                    result.Add(tokens);
                    continue;
                }
                tokens.Add(new SyntaxToken(0, end - 0, SyntaxKind.String));
                i = end;
                inTripleString = false;
            }

            while (i < n)
            {
                char c = line[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                bool matchedLineComment = false;
                foreach (var lc in def.LineComments)
                {
                    if (string.CompareOrdinal(line, i, lc, 0, lc.Length) == 0)
                    {
                        tokens.Add(new SyntaxToken(i, n - i, SyntaxKind.Comment));
                        i = n;
                        matchedLineComment = true;
                        break;
                    }
                }
                if (matchedLineComment) break;

                bool matchedBlockComment = false;
                foreach (var (start, end) in def.BlockComments)
                {
                    if (string.CompareOrdinal(line, i, start, 0, start.Length) == 0)
                    {
                        int closeAt = line.IndexOf(end, i + start.Length, StringComparison.Ordinal);
                        if (closeAt < 0)
                        {
                            tokens.Add(new SyntaxToken(i, n - i, SyntaxKind.Comment));
                            i = n;
                            inBlockComment = true;
                            blockCommentEnd = end;
                        }
                        else
                        {
                            tokens.Add(new SyntaxToken(i, closeAt + end.Length - i, SyntaxKind.Comment));
                            i = closeAt + end.Length;
                        }
                        matchedBlockComment = true;
                        break;
                    }
                }
                if (matchedBlockComment) continue;

                if (def.TripleQuoteStrings && i + 2 < n && (c == '"' || c == '\'') && line[i + 1] == c && line[i + 2] == c)
                {
                    int end = FindTripleQuoteEnd(line, i + 3, c);
                    if (end < 0)
                    {
                        tokens.Add(new SyntaxToken(i, n - i, SyntaxKind.String));
                        inTripleString = true;
                        tripleQuoteChar = c;
                        i = n;
                    }
                    else
                    {
                        tokens.Add(new SyntaxToken(i, end - i, SyntaxKind.String));
                        i = end;
                    }
                    continue;
                }

                if (Array.IndexOf(def.StringDelimiters, c) >= 0)
                {
                    int start = i;
                    i++;
                    while (i < n && line[i] != c)
                    {
                        if (line[i] == '\\' && i + 1 < n) i++;
                        i++;
                    }
                    if (i < n) i++; // consume closing quote
                    tokens.Add(new SyntaxToken(start, i - start, SyntaxKind.String));
                    continue;
                }

                var numberMatch = NumberRegex.Match(line, i);
                if (numberMatch.Success && numberMatch.Index == i)
                {
                    tokens.Add(new SyntaxToken(i, numberMatch.Length, SyntaxKind.Number));
                    i += numberMatch.Length;
                    continue;
                }

                var idMatch = IdentifierRegex.Match(line, i);
                if (idMatch.Success && idMatch.Index == i)
                {
                    var word = idMatch.Value;
                    SyntaxKind kind = SyntaxKind.Plain;
                    if (def.Keywords.Contains(word)) kind = SyntaxKind.Keyword;
                    else if (def.Types.Contains(word)) kind = SyntaxKind.Type;
                    else
                    {
                        int probe = i + word.Length;
                        while (probe < n && line[probe] == ' ') probe++;
                        if (probe < n && line[probe] == '(') kind = SyntaxKind.Function;
                        else if (word.Length > 0 && char.IsUpper(word[0])) kind = SyntaxKind.Type;
                    }
                    if (kind != SyntaxKind.Plain)
                        tokens.Add(new SyntaxToken(i, word.Length, kind));
                    i += word.Length;
                    continue;
                }

                i++;
            }

            result.Add(tokens);
        }

        return result;
    }

    private static int FindTripleQuoteEnd(string line, int from, char q)
    {
        string triple = new string(q, 3);
        int idx = line.IndexOf(triple, from, StringComparison.Ordinal);
        return idx < 0 ? -1 : idx + 3;
    }

    private static List<SyntaxToken> TokenizeJsonLine(string line)
    {
        var tokens = new List<SyntaxToken>();
        int n = line.Length;
        int i = 0;
        while (i < n)
        {
            char c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '"')
            {
                int start = i;
                i++;
                while (i < n && line[i] != '"')
                {
                    if (line[i] == '\\' && i + 1 < n) i++;
                    i++;
                }
                if (i < n) i++;
                int probe = i;
                while (probe < n && line[probe] == ' ') probe++;
                var kind = (probe < n && line[probe] == ':') ? SyntaxKind.Attribute : SyntaxKind.String;
                tokens.Add(new SyntaxToken(start, i - start, kind));
                continue;
            }

            var numberMatch = NumberRegex.Match(line, i);
            if (numberMatch.Success && numberMatch.Index == i)
            {
                tokens.Add(new SyntaxToken(i, numberMatch.Length, SyntaxKind.Number));
                i += numberMatch.Length;
                continue;
            }

            foreach (var lit in new[] { "true", "false", "null" })
            {
                if (string.CompareOrdinal(line, i, lit, 0, lit.Length) == 0)
                {
                    tokens.Add(new SyntaxToken(i, lit.Length, SyntaxKind.Keyword));
                    i += lit.Length;
                    goto continueOuter;
                }
            }

            i++;
            continueOuter: ;
        }
        return tokens;
    }

    private static List<SyntaxToken> TokenizeCssLine(string line)
    {
        var tokens = new List<SyntaxToken>();
        int n = line.Length;
        int i = 0;
        bool afterColon = false;
        while (i < n)
        {
            char c = line[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '/' && i + 1 < n && line[i + 1] == '*')
            {
                int end = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
                int len = end < 0 ? n - i : end + 2 - i;
                tokens.Add(new SyntaxToken(i, len, SyntaxKind.Comment));
                i += len;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                int start = i; i++;
                while (i < n && line[i] != c) i++;
                if (i < n) i++;
                tokens.Add(new SyntaxToken(start, i - start, SyntaxKind.String));
                continue;
            }

            if (c == ':') { afterColon = true; i++; continue; }
            if (c is ';' or '{' or '}') { afterColon = false; i++; continue; }

            var idMatch = Regex.Match(line[i..], @"^[-A-Za-z][-A-Za-z0-9%.#]*");
            if (idMatch.Success)
            {
                var kind = afterColon ? SyntaxKind.String : SyntaxKind.Attribute;
                if (idMatch.Value.StartsWith('.') || idMatch.Value.StartsWith('#')) kind = SyntaxKind.Tag;
                tokens.Add(new SyntaxToken(i, idMatch.Length, kind));
                i += idMatch.Length;
                continue;
            }

            i++;
        }
        return tokens;
    }

    private static List<List<SyntaxToken>> TokenizeMarkup(string[] lines)
    {
        var result = new List<List<SyntaxToken>>(lines.Length);
        bool inComment = false;

        foreach (var line in lines)
        {
            var tokens = new List<SyntaxToken>();
            int n = line.Length;
            int i = 0;

            if (inComment)
            {
                int end = line.IndexOf("-->", StringComparison.Ordinal);
                if (end < 0) { tokens.Add(new SyntaxToken(0, n, SyntaxKind.Comment)); result.Add(tokens); continue; }
                tokens.Add(new SyntaxToken(0, end + 3, SyntaxKind.Comment));
                i = end + 3;
                inComment = false;
            }

            while (i < n)
            {
                char c = line[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '<' && i + 3 < n && line[i + 1] == '!' && line[i + 2] == '-' && line[i + 3] == '-')
                {
                    int end = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    if (end < 0) { tokens.Add(new SyntaxToken(i, n - i, SyntaxKind.Comment)); inComment = true; i = n; continue; }
                    tokens.Add(new SyntaxToken(i, end + 3 - i, SyntaxKind.Comment));
                    i = end + 3;
                    continue;
                }

                if (c == '<')
                {
                    int tagStart = i;
                    i++;
                    if (i < n && line[i] == '/') i++;
                    var nameMatch = IdentifierRegex.Match(line, i);
                    int nameEnd = nameMatch.Success && nameMatch.Index == i ? i + nameMatch.Length : i;
                    if (nameEnd > i) tokens.Add(new SyntaxToken(tagStart, nameEnd - tagStart, SyntaxKind.Tag));
                    else tokens.Add(new SyntaxToken(tagStart, 1, SyntaxKind.Plain));
                    i = nameEnd;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    int start = i; i++;
                    while (i < n && line[i] != c) i++;
                    if (i < n) i++;
                    tokens.Add(new SyntaxToken(start, i - start, SyntaxKind.String));
                    continue;
                }

                var attrMatch = Regex.Match(line[i..], @"^[A-Za-z_:][-A-Za-z0-9_:.]*(?=\s*=)");
                if (attrMatch.Success)
                {
                    tokens.Add(new SyntaxToken(i, attrMatch.Length, SyntaxKind.Attribute));
                    i += attrMatch.Length;
                    continue;
                }

                i++;
            }

            result.Add(tokens);
        }

        return result;
    }
}
