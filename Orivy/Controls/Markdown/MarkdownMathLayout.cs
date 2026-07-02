using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

internal static class MarkdownMathLayout
{
    private const float ScriptScale = 0.66f;

    public static MathFormulaBox Build(string latex, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float y, float sizePx, SKColor color, bool display)
    {
        latex = NormalizeLatex(latex);
        MathNode root = MathParser.Parse(latex);
        var measure = Measure(root, theme, fonts, sizePx);
        float padX = display ? MathF.Max(10f, sizePx * 0.55f) : MathF.Max(2f, sizePx * 0.12f);
        float padY = display ? MathF.Max(8f, sizePx * 0.45f) : MathF.Max(2f, sizePx * 0.08f);

        var box = new MathFormulaBox
        {
            Latex = latex,
            Display = display,
            Color = color,
            Bounds = new SKRect(x, y, x + measure.Width + 2 * padX, y + measure.Height + 2 * padY)
        };

        Render(root, box, theme, fonts, x + padX, y + padY + measure.Ascent, sizePx, color);
        return box;
    }

    public static MathSize Measure(string latex, MarkdownTheme theme, MarkdownFontCache fonts, float sizePx) =>
        Measure(MathParser.Parse(NormalizeLatex(latex)), theme, fonts, sizePx);

    private static MathSize Measure(MathNode node, MarkdownTheme theme, MarkdownFontCache fonts, float sizePx)
    {
        switch (node)
        {
            case TextNode text:
                return MeasureMathText(text.Text, theme, fonts, sizePx);

            case OperatorNode op:
                return MeasureOperatorText(op.Text, theme, fonts, sizePx);

            case RowNode row:
                if (row.Children.Count == 0)
                    return Measure(new TextNode(" "), theme, fonts, sizePx);
                float width = 0f, asc = 0f, desc = 0f;
                foreach (var child in row.Children)
                {
                    var s = Measure(child, theme, fonts, sizePx);
                    width += s.Width;
                    asc = MathF.Max(asc, s.Ascent);
                    desc = MathF.Max(desc, s.Descent);
                }
                return new MathSize(width, asc, desc);

            case FractionNode frac:
                var num = Measure(frac.Numerator, theme, fonts, sizePx * 0.9f);
                var den = Measure(frac.Denominator, theme, fonts, sizePx * 0.9f);
                float gap = sizePx * 0.18f;
                float line = MathF.Max(1f, sizePx * 0.055f);
                return new MathSize(MathF.Max(num.Width, den.Width) + sizePx * 0.55f,
                    num.Height + gap + line,
                    den.Height + gap);

            case SqrtNode sqrt:
                var inner = Measure(sqrt.Inner, theme, fonts, sizePx);
                return new MathSize(inner.Width + sizePx * 0.75f, inner.Ascent + sizePx * 0.22f, inner.Descent + sizePx * 0.08f);

            case SupSubNode ss:
                var b = Measure(ss.Base, theme, fonts, sizePx);
                var sup = ss.Superscript == null ? MathSize.Empty : Measure(ss.Superscript, theme, fonts, sizePx * ScriptScale);
                var sub = ss.Subscript == null ? MathSize.Empty : Measure(ss.Subscript, theme, fonts, sizePx * ScriptScale);
                if (IsLimitsOperator(ss.Base))
                {
                    float limitGap = sizePx * 0.10f;
                    return new MathSize(MathF.Max(b.Width, MathF.Max(sup.Width, sub.Width)) + sizePx * 0.10f,
                        b.Ascent + (ss.Superscript == null ? 0f : sup.Height + limitGap),
                        b.Descent + (ss.Subscript == null ? 0f : sub.Height + limitGap));
                }
                float scriptW = MathF.Max(sup.Width, sub.Width);
                return new MathSize(b.Width + scriptW + sizePx * 0.08f,
                    MathF.Max(b.Ascent, b.Ascent * 0.65f + sup.Height),
                    MathF.Max(b.Descent, b.Descent + sub.Height * 0.8f));

            case CasesNode cases:
                float braceW = sizePx * 0.55f;
                float rowGap = sizePx * 0.35f;
                float rowsW = 0f, rowsH = 0f;
                foreach (var row in cases.Rows)
                {
                    var rs = Measure(row, theme, fonts, sizePx);
                    rowsW = MathF.Max(rowsW, rs.Width);
                    rowsH += rs.Height + rowGap;
                }
                if (cases.Rows.Count > 0) rowsH -= rowGap;
                return new MathSize(braceW + rowsW + sizePx * 0.25f, rowsH * 0.55f, rowsH * 0.45f);

            case MatrixNode matrix:
                return MeasureMatrix(matrix, theme, fonts, sizePx);

            case BinomialNode binom:
                var top = Measure(binom.Top, theme, fonts, sizePx * 0.9f);
                var bottom = Measure(binom.Bottom, theme, fonts, sizePx * 0.9f);
                float parenW = sizePx * 0.55f;
                return new MathSize(MathF.Max(top.Width, bottom.Width) + parenW * 2f + sizePx * 0.20f,
                    top.Height + sizePx * 0.18f,
                    bottom.Height + sizePx * 0.18f);
        }

        return MathSize.Empty;
    }

    private static void Render(MathNode node, MathFormulaBox box, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float baselineY, float sizePx, SKColor color)
    {
        switch (node)
        {
            case TextNode text:
                if (text.Text.Length == 0) return;
                RenderMathText(text.Text, box, theme, fonts, x, baselineY, sizePx, color);
                break;

            case OperatorNode op:
                RenderOperatorText(op.Text, box, theme, fonts, x, baselineY, sizePx, color);
                break;

            case RowNode row:
                float cx = x;
                foreach (var child in row.Children)
                {
                    var s = Measure(child, theme, fonts, sizePx);
                    Render(child, box, theme, fonts, cx, baselineY, sizePx, color);
                    cx += s.Width;
                }
                break;

            case FractionNode frac:
                var ms = Measure(frac, theme, fonts, sizePx);
                var num = Measure(frac.Numerator, theme, fonts, sizePx * 0.9f);
                var den = Measure(frac.Denominator, theme, fonts, sizePx * 0.9f);
                float lineY = baselineY;
                float left = x + sizePx * 0.18f;
                float right = x + ms.Width - sizePx * 0.18f;
                Render(frac.Numerator, box, theme, fonts, x + (ms.Width - num.Width) / 2f,
                    lineY - sizePx * 0.24f - num.Descent, sizePx * 0.9f, color);
                Render(frac.Denominator, box, theme, fonts, x + (ms.Width - den.Width) / 2f,
                    lineY + sizePx * 0.32f + den.Ascent, sizePx * 0.9f, color);
                box.Lines.Add(new MathLineSegment(new SKPoint(left, lineY), new SKPoint(right, lineY), MathF.Max(1f, sizePx * 0.055f)));
                break;

            case SqrtNode sqrt:
                var inner = Measure(sqrt.Inner, theme, fonts, sizePx);
                float rx = x + sizePx * 0.55f;
                Render(sqrt.Inner, box, theme, fonts, rx, baselineY, sizePx, color);
                float top = baselineY - inner.Ascent - sizePx * 0.14f;
                float bottom = baselineY + inner.Descent * 0.45f;
                box.Lines.Add(new MathLineSegment(new SKPoint(x + sizePx * 0.10f, baselineY - sizePx * 0.10f), new SKPoint(x + sizePx * 0.27f, bottom), MathF.Max(1f, sizePx * 0.05f)));
                box.Lines.Add(new MathLineSegment(new SKPoint(x + sizePx * 0.27f, bottom), new SKPoint(x + sizePx * 0.48f, top), MathF.Max(1f, sizePx * 0.05f)));
                box.Lines.Add(new MathLineSegment(new SKPoint(x + sizePx * 0.48f, top), new SKPoint(rx + inner.Width + sizePx * 0.10f, top), MathF.Max(1f, sizePx * 0.05f)));
                break;

            case SupSubNode ss:
                var b = Measure(ss.Base, theme, fonts, sizePx);
                if (IsLimitsOperator(ss.Base))
                {
                    var full = Measure(ss, theme, fonts, sizePx);
                    float baseX = x + (full.Width - b.Width) / 2f;
                    Render(ss.Base, box, theme, fonts, baseX, baselineY, sizePx, color);
                    if (ss.Superscript != null)
                    {
                        var sup = Measure(ss.Superscript, theme, fonts, sizePx * ScriptScale);
                        Render(ss.Superscript, box, theme, fonts, x + (full.Width - sup.Width) / 2f,
                            baselineY - b.Ascent - sizePx * 0.10f - sup.Descent, sizePx * ScriptScale, color);
                    }
                    if (ss.Subscript != null)
                    {
                        var sub = Measure(ss.Subscript, theme, fonts, sizePx * ScriptScale);
                        Render(ss.Subscript, box, theme, fonts, x + (full.Width - sub.Width) / 2f,
                            baselineY + b.Descent + sizePx * 0.10f + sub.Ascent, sizePx * ScriptScale, color);
                    }
                }
                else
                {
                    Render(ss.Base, box, theme, fonts, x, baselineY, sizePx, color);
                    float sx = x + b.Width + sizePx * 0.06f;
                    if (ss.Superscript != null)
                        Render(ss.Superscript, box, theme, fonts, sx, baselineY - b.Ascent * 0.62f, sizePx * ScriptScale, color);
                    if (ss.Subscript != null)
                        Render(ss.Subscript, box, theme, fonts, sx, baselineY + b.Descent + sizePx * 0.42f, sizePx * ScriptScale, color);
                }
                break;

            case CasesNode cases:
                var cs = Measure(cases, theme, fonts, sizePx);
                float braceW = sizePx * 0.42f;
                float rowGap = sizePx * 0.35f;
                float topY = baselineY - cs.Ascent;
                box.Braces.Add(new MathBrace(new SKRect(x, topY, x + braceW, baselineY + cs.Descent), MathF.Max(1.2f, sizePx * 0.055f)));
                float rowY = topY;
                foreach (var row in cases.Rows)
                {
                    var rs = Measure(row, theme, fonts, sizePx);
                    Render(row, box, theme, fonts, x + braceW + sizePx * 0.25f, rowY + rs.Ascent, sizePx, color);
                    rowY += rs.Height + rowGap;
                }
                break;

            case MatrixNode matrix:
                RenderMatrix(matrix, box, theme, fonts, x, baselineY, sizePx, color);
                break;

            case BinomialNode binom:
                RenderBinomial(binom, box, theme, fonts, x, baselineY, sizePx, color);
                break;
        }
    }

    private static MathSize MeasureMatrix(MatrixNode matrix, MarkdownTheme theme, MarkdownFontCache fonts, float sizePx)
    {
        int cols = MatrixColumnCount(matrix);
        if (cols == 0 || matrix.Rows.Count == 0)
            return Measure(new TextNode(" "), theme, fonts, sizePx);

        float colGap = sizePx * 0.90f;
        float rowGap = sizePx * 0.38f;
        float delimiterW = MatrixDelimiterWidth(matrix.Environment, sizePx);
        var colWidths = new float[cols];
        float totalH = 0f;

        foreach (var row in matrix.Rows)
        {
            float rowAsc = 0f, rowDesc = 0f;
            for (int c = 0; c < row.Count; c++)
            {
                var s = Measure(row[c], theme, fonts, sizePx);
                colWidths[c] = MathF.Max(colWidths[c], s.Width);
                rowAsc = MathF.Max(rowAsc, s.Ascent);
                rowDesc = MathF.Max(rowDesc, s.Descent);
            }
            totalH += rowAsc + rowDesc + rowGap;
        }

        totalH -= rowGap;
        float totalW = 0f;
        for (int c = 0; c < cols; c++) totalW += colWidths[c];
        totalW += colGap * MathF.Max(0, cols - 1);
        totalW += delimiterW * 2f;
        return new MathSize(totalW, totalH * 0.55f, totalH * 0.45f);
    }

    private static void RenderMatrix(MatrixNode matrix, MathFormulaBox box, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float baselineY, float sizePx, SKColor color)
    {
        int cols = MatrixColumnCount(matrix);
        if (cols == 0) return;

        float colGap = sizePx * 0.90f;
        float rowGap = sizePx * 0.38f;
        float delimiterW = MatrixDelimiterWidth(matrix.Environment, sizePx);
        var colWidths = new float[cols];
        var rowAscents = new float[matrix.Rows.Count];
        var rowDescents = new float[matrix.Rows.Count];

        for (int r = 0; r < matrix.Rows.Count; r++)
        {
            var row = matrix.Rows[r];
            for (int c = 0; c < row.Count; c++)
            {
                var s = Measure(row[c], theme, fonts, sizePx);
                colWidths[c] = MathF.Max(colWidths[c], s.Width);
                rowAscents[r] = MathF.Max(rowAscents[r], s.Ascent);
                rowDescents[r] = MathF.Max(rowDescents[r], s.Descent);
            }
        }

        var ms = Measure(matrix, theme, fonts, sizePx);
        float top = baselineY - ms.Ascent;
        float rowTop = top;
        RenderMatrixDelimiter(matrix.Environment, box, theme, fonts, x, baselineY, ms.Height, sizePx, color, left: true);
        RenderMatrixDelimiter(matrix.Environment, box, theme, fonts, x + ms.Width - delimiterW, baselineY, ms.Height, sizePx, color, left: false);

        for (int r = 0; r < matrix.Rows.Count; r++)
        {
            var row = matrix.Rows[r];
            float cx = x + delimiterW;
            float rowBaseline = rowTop + rowAscents[r];
            for (int c = 0; c < cols; c++)
            {
                if (c < row.Count)
                {
                    var cell = row[c];
                    var cellSize = Measure(cell, theme, fonts, sizePx);
                    Render(cell, box, theme, fonts, cx + (colWidths[c] - cellSize.Width) / 2f, rowBaseline, sizePx, color);
                }
                cx += colWidths[c] + colGap;
            }
            rowTop += rowAscents[r] + rowDescents[r] + rowGap;
        }
    }

    private static int MatrixColumnCount(MatrixNode matrix)
    {
        int cols = 0;
        foreach (var row in matrix.Rows)
            cols = Math.Max(cols, row.Count);
        return cols;
    }

    private static float MatrixDelimiterWidth(string env, float sizePx) =>
        env == "matrix" ? 0f : sizePx * 0.55f;

    private static void RenderMatrixDelimiter(string env, MathFormulaBox box, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float baselineY, float matrixHeight, float sizePx, SKColor color, bool left)
    {
        if (env == "matrix") return;
        string text = env switch
        {
            "bmatrix" => left ? "[" : "]",
            "vmatrix" => "|",
            "Vmatrix" => "\u2016",
            _ => left ? "(" : ")"
        };
        float scaled = MathF.Max(sizePx * 1.65f, matrixHeight * 0.86f);
        RenderOperatorText(text, box, theme, fonts, x, baselineY + sizePx * 0.10f, scaled, color);
    }

    private static void RenderBinomial(BinomialNode binom, MathFormulaBox box, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float baselineY, float sizePx, SKColor color)
    {
        var ms = Measure(binom, theme, fonts, sizePx);
        var top = Measure(binom.Top, theme, fonts, sizePx * 0.9f);
        var bottom = Measure(binom.Bottom, theme, fonts, sizePx * 0.9f);
        float parenW = sizePx * 0.42f;
        float innerX = x + parenW + sizePx * 0.10f;
        Render(binom.Top, box, theme, fonts, innerX + (MathF.Max(top.Width, bottom.Width) - top.Width) / 2f,
            baselineY - sizePx * 0.20f - top.Descent, sizePx * 0.9f, color);
        Render(binom.Bottom, box, theme, fonts, innerX + (MathF.Max(top.Width, bottom.Width) - bottom.Width) / 2f,
            baselineY + sizePx * 0.28f + bottom.Ascent, sizePx * 0.9f, color);
        RenderMathText("(", box, theme, fonts, x, baselineY, sizePx * 1.85f, color);
        RenderMathText(")", box, theme, fonts, x + ms.Width - parenW, baselineY, sizePx * 1.85f, color);
    }

    private static MathSize MeasureOperatorText(string text, MarkdownTheme theme, MarkdownFontCache fonts, float sizePx)
    {
        if (string.IsNullOrEmpty(text)) return MathSize.Empty;
        var font = fonts.GetFont(theme, false, sizePx, false, false);
        return new MathSize(font.MeasureText(text), -font.Metrics.Ascent, font.Metrics.Descent);
    }

    private static void RenderOperatorText(string text, MathFormulaBox box, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float baselineY, float sizePx, SKColor color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = fonts.GetFont(theme, false, sizePx, false, false);
        box.Runs.Add(new MathTextRun { Text = text, Font = font, Baseline = new SKPoint(x, baselineY), Color = color });
    }

    private static string NormalizeLatex(string latex) =>
        latex.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static MathSize MeasureMathText(string text, MarkdownTheme theme, MarkdownFontCache fonts, float sizePx)
    {
        if (string.IsNullOrEmpty(text)) return MathSize.Empty;

        float width = 0f;
        float ascent = 0f;
        float descent = 0f;
        foreach (var (segment, italic) in SplitMathTextRuns(text))
        {
            var font = fonts.GetFont(theme, false, sizePx, false, italic);
            width += font.MeasureText(segment);
            ascent = MathF.Max(ascent, -font.Metrics.Ascent);
            descent = MathF.Max(descent, font.Metrics.Descent);
        }
        return new MathSize(width, ascent, descent);
    }

    private static void RenderMathText(string text, MathFormulaBox box, MarkdownTheme theme, MarkdownFontCache fonts,
        float x, float baselineY, float sizePx, SKColor color)
    {
        float cx = x;
        foreach (var (segment, italic) in SplitMathTextRuns(text))
        {
            var font = fonts.GetFont(theme, false, sizePx, false, italic);
            box.Runs.Add(new MathTextRun { Text = segment, Font = font, Baseline = new SKPoint(cx, baselineY), Color = color });
            cx += font.MeasureText(segment);
        }
    }

    private static IEnumerable<(string Segment, bool Italic)> SplitMathTextRuns(string text)
    {
        int start = 0;
        bool italic = IsMathItalic(text[0]);
        for (int i = 1; i < text.Length; i++)
        {
            bool nextItalic = IsMathItalic(text[i]);
            if (nextItalic == italic) continue;
            yield return (text[start..i], italic);
            start = i;
            italic = nextItalic;
        }
        yield return (text[start..], italic);
    }

    private static bool IsMathItalic(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static bool IsLimitsOperator(MathNode node) =>
        node is TextNode text && (text.Text == "\u2211" || text.Text == "\u220F") ||
        node is OperatorNode op && op.Text == "lim";

    public readonly struct MathSize
    {
        public static readonly MathSize Empty = new(0f, 0f, 0f);
        public readonly float Width;
        public readonly float Ascent;
        public readonly float Descent;
        public float Height => Ascent + Descent;
        public MathSize(float width, float ascent, float descent)
        {
            Width = MathF.Max(0f, width);
            Ascent = MathF.Max(0f, ascent);
            Descent = MathF.Max(0f, descent);
        }
    }

    private abstract class MathNode { }
    private sealed class RowNode : MathNode { public List<MathNode> Children = new(); }
    private sealed class TextNode : MathNode { public string Text; public TextNode(string text) => Text = text; }
    private sealed class OperatorNode : MathNode { public string Text; public OperatorNode(string text) => Text = text; }
    private sealed class FractionNode : MathNode { public MathNode Numerator = new RowNode(); public MathNode Denominator = new RowNode(); }
    private sealed class SqrtNode : MathNode { public MathNode Inner = new RowNode(); }
    private sealed class SupSubNode : MathNode { public MathNode Base = new RowNode(); public MathNode? Superscript; public MathNode? Subscript; }
    private sealed class CasesNode : MathNode { public List<MathNode> Rows = new(); }
    private sealed class MatrixNode : MathNode { public List<List<MathNode>> Rows = new(); public string Environment = "matrix"; }
    private sealed class BinomialNode : MathNode { public MathNode Top = new RowNode(); public MathNode Bottom = new RowNode(); }

    private sealed class MathParser
    {
        private readonly string _source;
        private int _pos;

        private MathParser(string source) => _source = source;

        public static MathNode Parse(string source)
        {
            var parser = new MathParser(source);
            return parser.ParseRow(stopOnBrace: false);
        }

        private RowNode ParseRow(bool stopOnBrace)
        {
            var row = new RowNode();
            var text = new StringBuilder();

            void FlushText()
            {
                if (text.Length == 0) return;
                row.Children.Add(new TextNode(text.ToString()));
                text.Clear();
            }

            while (_pos < _source.Length)
            {
                char c = _source[_pos];
                if (stopOnBrace && c == '}') break;
                if (c == '^' || c == '_')
                {
                    FlushText();
                    ApplyScript(row, c == '^');
                    continue;
                }
                if (c == '\\')
                {
                    FlushText();
                    row.Children.Add(ParseCommand());
                    continue;
                }
                if (c == '{')
                {
                    FlushText();
                    row.Children.Add(ParseGroup());
                    continue;
                }
                if (c == '}') break;
                if (char.IsWhiteSpace(c))
                {
                    if (text.Length == 0 || text[^1] != ' ') text.Append(' ');
                    _pos++;
                    continue;
                }
                text.Append(c);
                _pos++;
            }

            FlushText();
            return row;
        }

        private void ApplyScript(RowNode row, bool superscript)
        {
            _pos++;
            MathNode script = ParseScriptAtom();
            MathNode baseNode = TakeScriptBase(row);
            SupSubNode ss;
            if (baseNode is SupSubNode existing)
            {
                ss = existing;
                row.Children.Add(ss);
            }
            else
            {
                ss = new SupSubNode { Base = baseNode };
                row.Children.Add(ss);
            }

            if (superscript) ss.Superscript = script;
            else ss.Subscript = script;
        }

        private static MathNode TakeScriptBase(RowNode row)
        {
            if (row.Children.Count == 0) return new TextNode("");

            MathNode last = row.Children[^1];
            row.Children.RemoveAt(row.Children.Count - 1);

            if (last is TextNode text && text.Text.Length > 1)
            {
                string raw = text.Text;
                int baseIndex = raw.Length - 1;
                while (baseIndex > 0 && char.IsWhiteSpace(raw[baseIndex])) baseIndex--;

                string prefix = raw[..baseIndex];
                string baseText = raw[baseIndex].ToString();
                string suffix = raw[(baseIndex + 1)..];

                if (prefix.Length > 0) row.Children.Add(new TextNode(prefix));
                if (suffix.Length > 0) row.Children.Add(new TextNode(suffix));
                return new TextNode(baseText);
            }

            return last;
        }

        private MathNode ParseScriptAtom()
        {
            SkipSpaces();
            if (_pos >= _source.Length) return new TextNode("");
            if (_source[_pos] == '{') return ParseGroup();
            if (_source[_pos] == '\\') return ParseCommand();
            return new TextNode(_source[_pos++].ToString());
        }

        private MathNode ParseGroup()
        {
            if (_pos < _source.Length && _source[_pos] == '{') _pos++;
            var node = ParseRow(stopOnBrace: true);
            if (_pos < _source.Length && _source[_pos] == '}') _pos++;
            return node;
        }

        private MathNode ParseCommand()
        {
            _pos++;
            string name = ReadCommandName();
            switch (name)
            {
                case "frac":
                    return new FractionNode { Numerator = ParseRequiredGroup(), Denominator = ParseRequiredGroup() };
                case "binom":
                case "choose":
                    return new BinomialNode { Top = ParseRequiredGroup(), Bottom = ParseRequiredGroup() };
                case "sqrt":
                    return new SqrtNode { Inner = ParseRequiredGroup() };
                case "begin":
                    string env = ReadEnvironmentName();
                    if (env == "cases") return ParseCases();
                    if (env is "matrix" or "pmatrix" or "bmatrix" or "vmatrix" or "Vmatrix") return ParseMatrix(env);
                    return new TextNode("");
                case "left":
                case "right":
                    return new TextNode(ReadDelimiter());
                case "\\": 
                    return new TextNode(" ");
            }

            if (IsOperatorCommand(name))
                return new OperatorNode(name);

            return new TextNode(SymbolForCommand(name));
        }

        private MathNode ParseRequiredGroup()
        {
            SkipSpaces();
            return _pos < _source.Length && _source[_pos] == '{' ? ParseGroup() : new TextNode("");
        }

        private CasesNode ParseCases()
        {
            var body = new StringBuilder();
            const string end = @"\end{cases}";
            int endIndex = _source.IndexOf(end, _pos, StringComparison.Ordinal);
            if (endIndex < 0) endIndex = _source.Length;
            body.Append(_source, _pos, endIndex - _pos);
            _pos = Math.Min(_source.Length, endIndex + end.Length);

            var cases = new CasesNode();
            foreach (var rawRow in SplitRows(body.ToString()))
            {
                var text = rawRow.Trim();
                if (text.Length == 0) continue;
                cases.Rows.Add(Parse(text));
            }
            return cases;
        }

        private MatrixNode ParseMatrix(string env)
        {
            var body = new StringBuilder();
            string end = @"\end{" + env + "}";
            int endIndex = _source.IndexOf(end, _pos, StringComparison.Ordinal);
            if (endIndex < 0) endIndex = _source.Length;
            body.Append(_source, _pos, endIndex - _pos);
            _pos = Math.Min(_source.Length, endIndex + end.Length);

            var matrix = new MatrixNode { Environment = env };
            foreach (var rawRow in SplitRows(body.ToString()))
            {
                var cells = new List<MathNode>();
                foreach (var rawCell in SplitCells(rawRow))
                {
                    string cell = rawCell.Trim();
                    cells.Add(cell.Length == 0 ? new TextNode("") : Parse(cell));
                }
                if (cells.Count > 0) matrix.Rows.Add(cells);
            }
            return matrix;
        }

        private static IEnumerable<string> SplitRows(string text)
        {
            var rows = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '\\')
                {
                    rows.Add(sb.ToString());
                    sb.Clear();
                    i++;
                    continue;
                }
                if (text[i] == '\n')
                {
                    if (sb.Length > 0) sb.Append(' ');
                    continue;
                }
                sb.Append(text[i]);
            }
            rows.Add(sb.ToString());
            return rows;
        }

        private static IEnumerable<string> SplitCells(string text)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '&')
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }
                sb.Append(text[i]);
            }
            cells.Add(sb.ToString());
            return cells;
        }

        private string ReadEnvironmentName()
        {
            SkipSpaces();
            if (_pos >= _source.Length || _source[_pos] != '{') return "";
            _pos++;
            int start = _pos;
            while (_pos < _source.Length && _source[_pos] != '}') _pos++;
            string name = _source[start.._pos];
            if (_pos < _source.Length) _pos++;
            return name;
        }

        private string ReadDelimiter()
        {
            SkipSpaces();
            if (_pos >= _source.Length) return "";
            if (_source[_pos] == '\\')
            {
                _pos++;
                return SymbolForCommand(ReadCommandName());
            }
            return _source[_pos++].ToString();
        }

        private string ReadCommandName()
        {
            if (_pos < _source.Length && !char.IsLetter(_source[_pos]))
                return _source[_pos++].ToString();
            int start = _pos;
            while (_pos < _source.Length && char.IsLetter(_source[_pos])) _pos++;
            return _source[start.._pos];
        }

        private void SkipSpaces()
        {
            while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos])) _pos++;
        }

        private static bool IsOperatorCommand(string name) =>
            name is "sin" or "cos" or "tan" or "cot" or "sec" or "csc" or
                "log" or "ln" or "lim" or "max" or "min" or "sup" or "inf";

        private static string SymbolForCommand(string name) => name switch
        {
            "alpha" => "\u03B1", "beta" => "\u03B2", "gamma" => "\u03B3", "delta" => "\u03B4", "epsilon" => "\u03B5",
            "theta" => "\u03B8", "lambda" => "\u03BB", "mu" => "\u03BC", "pi" => "\u03C0", "sigma" => "\u03C3",
            "phi" => "\u03C6", "omega" => "\u03C9", "Gamma" => "\u0393", "Delta" => "\u0394", "Theta" => "\u0398",
            "Lambda" => "\u039B", "Pi" => "\u03A0", "Sigma" => "\u03A3", "Phi" => "\u03A6", "Omega" => "\u03A9",
            "pm" => "\u00B1", "mp" => "\u2213", "times" => "\u00D7", "cdot" => "\u00B7", "div" => "\u00F7",
            "le" or "leq" => "\u2264", "ge" or "geq" => "\u2265", "neq" => "\u2260", "approx" => "\u2248",
            "infty" => "\u221E", "sum" => "\u2211", "prod" => "\u220F", "int" => "\u222B", "partial" => "\u2202",
            "nabla" => "\u2207", "rightarrow" or "to" => "\u2192", "leftarrow" => "\u2190",
            "Rightarrow" => "\u21D2", "Leftarrow" => "\u21D0", "quad" => "    ", "," => " ", ";" => "  ",
            _ => name.Length == 0 ? "" : name
        };
    }
}
