using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

// ============================================================================
// Layout builder: walks the parsed MarkdownDocument and produces a flat,
// positioned List<MdBox> plus the total content height. Runs whenever the
// content, the available width, or the DPI scale changes -- never per frame.
// ============================================================================

internal static class MarkdownLayoutBuilder
{
    private struct AtomStyle
    {
        public bool Bold, Italic, Strike, Sub, Sup;
        public LinkInline? Link;
    }

    private struct Atom
    {
        public string Text;
        public bool IsWhitespace;
        public bool ForceBreak;
        public bool IsCode;
        public bool Bold, Italic, Strike, Subscript, Superscript;
        public bool IsEmoji;        // render with emoji fallback font
        public LinkInline? Link;
        public ImageInline? Image;
    }

    private readonly struct WrappedLine
    {
        public readonly int Start, End;
        public readonly float Width;
        public readonly float Height;
        public WrappedLine(int start, int end, float width, float height)
        {
            Start = start; End = end; Width = width; Height = height;
        }
    }

    private sealed class Ctx
    {
        public MarkdownTheme Theme = null!;
        public MarkdownFontCache Fonts = null!;
        public float Scale = 1f;
        public MarkdownInteractionState State = null!;
        public List<MdBox> Boxes = new();
        public float Y;
        public Dictionary<string, float> HeadingPositions = new();
    }

    public static List<MdBox> Build(
        MarkdownDocument doc,
        MarkdownTheme theme,
        MarkdownFontCache fonts,
        float contentWidth,
        float scale,
        float originX,
        float originY,
        float bottomPadding,
        MarkdownInteractionState state,
        out float totalHeight,
        out Dictionary<string, float> headingPositions)
    {
        var ctx = new Ctx { Theme = theme, Fonts = fonts, Scale = Math.Max(0.1f, scale), State = state, Y = originY };
        LayoutBlocks(ctx, doc.Blocks, originX, Math.Max(20f, contentWidth), tight: false, textColor: theme.BodyColor, isFirstInParent: true);
        totalHeight = ctx.Y + bottomPadding * ctx.Scale;
        headingPositions = ctx.HeadingPositions;
        return ctx.Boxes;
    }

    private static void LayoutBlocks(Ctx ctx, List<MarkdownBlock> blocks, float x, float width, bool tight, SKColor textColor, bool isFirstInParent)
    {
        bool first = isFirstInParent;
        foreach (var block in blocks)
        {
            float spacingBefore = first ? 0f
                : (block is HeadingBlock ? ctx.Theme.HeadingSpacingTop : (tight ? ctx.Theme.TightBlockSpacing : ctx.Theme.BlockSpacing)) * ctx.Scale;
            ctx.Y += spacingBefore;

            switch (block)
            {
                case HeadingBlock h: LayoutHeading(ctx, h, x, width); break;
                case ParagraphBlock p: LayoutParagraph(ctx, p, x, width, textColor); break;
                case ThematicBreakBlock: LayoutThematicBreak(ctx, x, width); break;
                case CodeBlockBlock cb: LayoutCodeBlock(ctx, cb, x, width); break;
                case BlockQuoteBlock bq: LayoutBlockQuote(ctx, bq, x, width); break;
                case ListBlock l: LayoutList(ctx, l, x, width); break;
                case TableBlock t: LayoutTable(ctx, t, x, width); break;
                case DetailsBlock d: LayoutDetails(ctx, d, x, width); break;
                case RawHtmlBlock raw: LayoutRawHtml(ctx, raw, x, width); break;
            }

            first = false;
        }
    }

    // ------------------------------------------------------------------
    // Headings / paragraphs / rules
    // ------------------------------------------------------------------
    private static void LayoutHeading(Ctx ctx, HeadingBlock h, float x, float width)
    {
        int levelIdx = Math.Clamp(h.Level - 1, 0, 5);
        float fontSize = ctx.Theme.HeadingFontSizes[levelIdx];

        ctx.HeadingPositions[h.Slug] = ctx.Y;

        var atoms = new List<Atom>();
        CollectAtoms(h.Inlines, default, atoms);
        if (atoms.Count == 0) atoms.Add(new Atom { Text = " " });

        EmitInlineFlow(ctx, atoms, x, width, fontSize, ctx.Theme.HeadingLineHeight, ctx.Theme.HeadingColor, forceBold: true);

        if (h.Level <= 2)
        {
            ctx.Y += 6f * ctx.Scale;
            float hair = Math.Max(1f, ctx.Scale);
            ctx.Boxes.Add(new RectBox { Bounds = new SKRect(x, ctx.Y, x + width, ctx.Y + hair), Fill = ctx.Theme.HeadingBorderColor });
            ctx.Y += hair;
        }
    }

    private static void LayoutParagraph(Ctx ctx, ParagraphBlock p, float x, float width, SKColor textColor)
    {
        var atoms = new List<Atom>();
        CollectAtoms(p.Inlines, default, atoms);
        if (atoms.Count == 0) return;
        EmitInlineFlow(ctx, atoms, x, width, ctx.Theme.BodyFontSize, ctx.Theme.BodyLineHeight, textColor, forceBold: false);
    }

    private static void LayoutThematicBreak(Ctx ctx, float x, float width)
    {
        float h = ctx.Theme.ThematicBreakHeight * ctx.Scale;
        ctx.Boxes.Add(new RectBox { Bounds = new SKRect(x, ctx.Y, x + width, ctx.Y + h), Fill = ctx.Theme.BorderColor, CornerRadius = h / 2f });
        ctx.Y += h;
    }

    // ------------------------------------------------------------------
    // Code blocks (with syntax highlighting + per-block horizontal scroll)
    // ------------------------------------------------------------------
    private static void LayoutCodeBlock(Ctx ctx, CodeBlockBlock cb, float x, float width)
    {
        var tokensPerLine = MarkdownSyntaxHighlighter.Tokenize(cb.Code, cb.Language);
        var codeLines = cb.Code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        float codeFontPx = ctx.Theme.CodeFontSize * ctx.Scale;
        var codeFont = ctx.Fonts.GetFont(ctx.Theme, true, codeFontPx, false, false);
        float lineHeight = MathF.Ceiling(codeFontPx * ctx.Theme.CodeLineHeight);
        float ascent = -codeFont.Metrics.Ascent;

        float padH = ctx.Theme.CodeBlockPaddingH * ctx.Scale;
        float padV = ctx.Theme.CodeBlockPaddingV * ctx.Scale;
        float headerH = ctx.Theme.CodeBlockHeaderHeight * ctx.Scale;

        float widestLine = 0f;
        var lineBoxes = new List<List<TextRunBox>>(codeLines.Length);

        for (int li = 0; li < codeLines.Length; li++)
        {
            string lineText = codeLines[li];
            var tokens = li < tokensPerLine.Count ? tokensPerLine[li] : new List<SyntaxToken>();
            var runs = new List<TextRunBox>();
            float cx = 0f;
            int cursor = 0;

            void EmitRun(string text, SyntaxKind kind)
            {
                if (text.Length == 0) return;
                float w = codeFont.MeasureText(text);
                runs.Add(new TextRunBox
                {
                    Bounds = new SKRect(cx, 0, cx + w, lineHeight),
                    Text = text,
                    Font = codeFont,
                    Color = ColorForSyntaxKind(ctx.Theme, kind),
                    Baseline = new SKPoint(cx, ascent)
                });
                cx += w;
            }

            foreach (var tok in tokens)
            {
                if (tok.Start > cursor && tok.Start <= lineText.Length) EmitRun(lineText[cursor..tok.Start], SyntaxKind.Plain);
                int end = Math.Min(lineText.Length, tok.Start + tok.Length);
                if (end > tok.Start) EmitRun(lineText[tok.Start..end], tok.Kind);
                cursor = Math.Max(cursor, end);
            }
            if (cursor < lineText.Length) EmitRun(lineText[cursor..], SyntaxKind.Plain);

            widestLine = Math.Max(widestLine, cx);
            lineBoxes.Add(runs);
        }

        float viewportWidth = width - 2 * padH;
        bool needsHScroll = widestLine > viewportWidth;
        var scrollState = GetOrCreateScrollState(ctx.State, cb);
        float maxScroll = Math.Max(0f, widestLine - viewportWidth);
        scrollState.ScrollX = Math.Clamp(scrollState.ScrollX, 0f, maxScroll);

        float bodyHeight = lineBoxes.Count * lineHeight + 2 * padV;
        float totalHeight = headerH + bodyHeight;

        var outer = new CodeBlockBox
        {
            Bounds = new SKRect(x, ctx.Y, x + width, ctx.Y + totalHeight),
            Source = cb,
            Lines = lineBoxes,
            ContentWidth = widestLine,
            Language = cb.Language,
            HeaderRect = new SKRect(x, ctx.Y, x + width, ctx.Y + headerH),
            BodyRect = new SKRect(x, ctx.Y + headerH, x + width, ctx.Y + totalHeight),
            CopyButtonRect = new SKRect(x + width - 36f * ctx.Scale, ctx.Y + (headerH - 22f * ctx.Scale) / 2f, x + width - 8f * ctx.Scale, ctx.Y + (headerH + 22f * ctx.Scale) / 2f),
            BodyOrigin = new SKPoint(x + padH, ctx.Y + headerH + padV),
            LineHeight = lineHeight,
            NeedsHorizontalScroll = needsHScroll,
            Scroll = scrollState,
            ViewportWidth = viewportWidth,
        };
        ctx.Boxes.Add(outer);

        if (!string.IsNullOrEmpty(cb.Language))
        {
            float langSize = Math.Max(10f * ctx.Scale, lineHeight * 0.62f);
            var langFont = ctx.Fonts.GetFont(ctx.Theme, true, langSize, false, false);
            float langAscent = -langFont.Metrics.Ascent;
            float baselineY = ctx.Y + (headerH - (langAscent + langFont.Metrics.Descent)) / 2f + langAscent;
            ctx.Boxes.Add(new TextRunBox
            {
                Bounds = new SKRect(x + 14f * ctx.Scale, ctx.Y, x + 160f * ctx.Scale, ctx.Y + headerH),
                Text = cb.Language!.ToUpperInvariant(),
                Font = langFont,
                Color = ctx.Theme.MutedColor,
                Baseline = new SKPoint(x + 14f * ctx.Scale, baselineY)
            });
        }

        ctx.Y += totalHeight;
    }

    private static SKColor ColorForSyntaxKind(MarkdownTheme theme, SyntaxKind kind) => kind switch
    {
        SyntaxKind.Keyword => theme.SyntaxKeyword,
        SyntaxKind.String => theme.SyntaxString,
        SyntaxKind.Comment => theme.SyntaxComment,
        SyntaxKind.Number => theme.SyntaxNumber,
        SyntaxKind.Type => theme.SyntaxType,
        SyntaxKind.Function => theme.SyntaxFunction,
        SyntaxKind.Attribute => theme.SyntaxAttribute,
        SyntaxKind.Tag => theme.SyntaxTag,
        _ => theme.CodeForeground
    };

    private static CodeBlockScrollState GetOrCreateScrollState(MarkdownInteractionState state, CodeBlockBlock block)
    {
        if (!state.CodeScroll.TryGetValue(block, out var s)) { s = new CodeBlockScrollState(); state.CodeScroll[block] = s; }
        return s;
    }

    // ------------------------------------------------------------------
    // Blockquotes (incl. GitHub-style alerts)
    // ------------------------------------------------------------------
    private static void LayoutBlockQuote(Ctx ctx, BlockQuoteBlock bq, float x, float width)
    {
        float barW = ctx.Theme.BlockquoteBarWidth * ctx.Scale;
        float indent = ctx.Theme.BlockquoteIndent * ctx.Scale;
        float startY = ctx.Y;

        SKColor barColor = ctx.Theme.BlockquoteBarColor;
        SKColor textColor = ctx.Theme.MutedColor;

        if (bq.AlertKind != AlertKind.None)
        {
            barColor = AlertColor(ctx.Theme, bq.AlertKind);
            textColor = ctx.Theme.BodyColor;

            float iconSize = 18f * ctx.Scale;
            ctx.Boxes.Add(new AlertHeaderBox
            {
                Bounds = new SKRect(x + indent, ctx.Y, x + indent + iconSize, ctx.Y + iconSize),
                Kind = bq.AlertKind
            });

            var labelFont = ctx.Fonts.GetFont(ctx.Theme, false, ctx.Theme.BodyFontSize * ctx.Scale, true, false);
            float labelAscent = -labelFont.Metrics.Ascent;
            float labelDescent = labelFont.Metrics.Descent;
            float labelBoxHeight = Math.Max(iconSize, labelAscent + labelDescent);
            float baselineY = ctx.Y + (labelBoxHeight - (labelAscent + labelDescent)) / 2f + labelAscent;

            ctx.Boxes.Add(new TextRunBox
            {
                Bounds = new SKRect(x + indent + iconSize + 8f * ctx.Scale, ctx.Y, x + width, ctx.Y + labelBoxHeight),
                Text = AlertDisplayName(bq.AlertKind),
                Font = labelFont,
                Color = barColor,
                Baseline = new SKPoint(x + indent + iconSize + 8f * ctx.Scale, baselineY)
            });

            ctx.Y += labelBoxHeight + ctx.Theme.TightBlockSpacing * ctx.Scale;
        }

        LayoutBlocks(ctx, bq.Blocks, x + indent, width - indent, tight: false, textColor: textColor, isFirstInParent: true);

        ctx.Boxes.Add(new RectBox
        {
            Bounds = new SKRect(x, startY, x + barW, Math.Max(ctx.Y, startY + barW)),
            Fill = barColor,
            CornerRadius = barW / 2f
        });
    }

    private static SKColor AlertColor(MarkdownTheme t, AlertKind k) => k switch
    {
        AlertKind.Note => t.AlertNote,
        AlertKind.Tip => t.AlertTip,
        AlertKind.Important => t.AlertImportant,
        AlertKind.Warning => t.AlertWarning,
        AlertKind.Caution => t.AlertCaution,
        _ => t.BlockquoteBarColor
    };

    private static string AlertDisplayName(AlertKind k) => k switch
    {
        AlertKind.Note => "Note",
        AlertKind.Tip => "Tip",
        AlertKind.Important => "Important",
        AlertKind.Warning => "Warning",
        AlertKind.Caution => "Caution",
        _ => ""
    };

    // ------------------------------------------------------------------
    // Lists (incl. GFM task lists)
    // ------------------------------------------------------------------
    private static void LayoutList(Ctx ctx, ListBlock list, float x, float width)
    {
        float indent = ctx.Theme.ListIndent * ctx.Scale;
        int number = list.StartNumber;
        bool first = true;

        foreach (var item in list.Items)
        {
            float spacing = first ? 0f : (list.Tight ? ctx.Theme.TightBlockSpacing : ctx.Theme.BlockSpacing) * ctx.Scale;
            ctx.Y += spacing;

            float markerFontSize = ctx.Theme.BodyFontSize * ctx.Scale;
            var markerFont = ctx.Fonts.GetFont(ctx.Theme, false, markerFontSize, false, false);
            float ascent = -markerFont.Metrics.Ascent;
            float lineHeight = MathF.Ceiling(markerFontSize * ctx.Theme.BodyLineHeight);

            if (item.TaskChecked.HasValue)
            {
                float boxSize = ctx.Theme.CheckboxSize * ctx.Scale;
                float cy = ctx.Y + (lineHeight - boxSize) / 2f;
                ctx.Boxes.Add(new CheckboxBox
                {
                    Bounds = new SKRect(x + indent - boxSize - 8f * ctx.Scale, cy, x + indent - 8f * ctx.Scale, cy + boxSize),
                    Checked = item.TaskChecked.Value,
                    Item = item
                });
            }
            else
            {
                string marker = list.Kind == ListKind.Ordered ? $"{number}." : "\u2022";
                float markerW = markerFont.MeasureText(marker);
                float markerX = x + indent - markerW - 8f * ctx.Scale;
                ctx.Boxes.Add(new TextRunBox
                {
                    Bounds = new SKRect(markerX, ctx.Y, markerX + markerW, ctx.Y + lineHeight),
                    Text = marker,
                    Font = markerFont,
                    Color = ctx.Theme.BodyColor,
                    Baseline = new SKPoint(markerX, ctx.Y + ascent)
                });
            }

            LayoutBlocks(ctx, item.Blocks, x + indent, width - indent, tight: list.Tight, textColor: ctx.Theme.BodyColor, isFirstInParent: true);

            number++;
            first = false;
        }
    }

    // ------------------------------------------------------------------
    // Tables
    // ------------------------------------------------------------------
    private static void LayoutTable(Ctx ctx, TableBlock table, float x, float width)
    {
        int cols = table.Alignments.Count;
        if (cols == 0) return;

        float fontSizePx = ctx.Theme.BodyFontSize * ctx.Scale;
        float padH = ctx.Theme.TableCellPaddingH * ctx.Scale;
        float padV = ctx.Theme.TableCellPaddingV * ctx.Scale;

        var natural = new float[cols];
        for (int c = 0; c < cols; c++)
            natural[c] = MeasureCellNaturalWidth(ctx, c < table.HeaderCells.Count ? table.HeaderCells[c] : new(), fontSizePx, true);
        foreach (var row in table.Rows)
            for (int c = 0; c < cols; c++)
                natural[c] = Math.Max(natural[c], MeasureCellNaturalWidth(ctx, c < row.Count ? row[c] : new(), fontSizePx, false));

        float totalNatural = 0f;
        for (int c = 0; c < cols; c++) { natural[c] = Math.Min(natural[c] + 2 * padH, width); totalNatural += natural[c]; }

        var colWidths = new float[cols];
        if (totalNatural > 0 && totalNatural <= width)
        {
            float extra = (width - totalNatural) / cols;
            for (int c = 0; c < cols; c++) colWidths[c] = natural[c] + extra;
        }
        else
        {
            float scaleDown = totalNatural > 0 ? width / totalNatural : 1f;
            for (int c = 0; c < cols; c++) colWidths[c] = Math.Max(48f * ctx.Scale, natural[c] * scaleDown);
        }

        var colX = new float[cols + 1];
        colX[0] = x;
        for (int c = 0; c < cols; c++) colX[c + 1] = colX[c] + colWidths[c];

        float startY = ctx.Y;
        float headerRowHeight = LayoutTableRow(ctx, table.HeaderCells, colX, colWidths, table.Alignments, padH, padV, fontSizePx, true, measureOnly: true);
        ctx.Boxes.Add(new RectBox { Bounds = new SKRect(x, ctx.Y, colX[cols], ctx.Y + headerRowHeight), Fill = ctx.Theme.TableHeaderBackground });
        LayoutTableRow(ctx, table.HeaderCells, colX, colWidths, table.Alignments, padH, padV, fontSizePx, true, measureOnly: false);
        ctx.Y += headerRowHeight;

        foreach (var row in table.Rows)
        {
            float rowHeight = LayoutTableRow(ctx, row, colX, colWidths, table.Alignments, padH, padV, fontSizePx, false, measureOnly: true);
            LayoutTableRow(ctx, row, colX, colWidths, table.Alignments, padH, padV, fontSizePx, false, measureOnly: false);
            ctx.Y += rowHeight;
        }

        float endY = ctx.Y;
        float hair = Math.Max(1f, ctx.Scale);
        for (int c = 0; c <= cols; c++)
            ctx.Boxes.Add(new RectBox { Bounds = new SKRect(colX[c], startY, colX[c] + hair, endY), Fill = ctx.Theme.TableBorderColor });
        ctx.Boxes.Add(new RectBox { Bounds = new SKRect(x, startY, colX[cols], startY + hair), Fill = ctx.Theme.TableBorderColor });
        ctx.Boxes.Add(new RectBox { Bounds = new SKRect(x, startY + headerRowHeight, colX[cols], startY + headerRowHeight + hair), Fill = ctx.Theme.TableBorderColor });
        ctx.Boxes.Add(new RectBox { Bounds = new SKRect(x, endY - hair, colX[cols], endY), Fill = ctx.Theme.TableBorderColor });
    }

    private static float MeasureCellNaturalWidth(Ctx ctx, List<MarkdownInline> inlines, float fontSizePx, bool bold)
    {
        var atoms = new List<Atom>();
        CollectAtoms(inlines, default, atoms);
        if (atoms.Count == 0) return 0f;
        var lines = WrapAtoms(ctx, atoms, 100_000f, fontSizePx, fontSizePx * 2f, bold);
        float max = 0f;
        foreach (var l in lines) max = Math.Max(max, l.Width);
        return max;
    }

    private static float LayoutTableRow(Ctx ctx, List<List<MarkdownInline>> cells, float[] colX, float[] colWidths,
        List<ColumnAlignment> alignments, float padH, float padV, float fontSizePx, bool bold, bool measureOnly)
    {
        float lineHeightPx = MathF.Ceiling(fontSizePx * ctx.Theme.BodyLineHeight);
        int cols = colWidths.Length;
        var perCellAtoms = new List<Atom>[cols];
        var perCellLines = new List<WrappedLine>[cols];
        float maxHeight = lineHeightPx + 2 * padV;

        for (int c = 0; c < cols; c++)
        {
            var atoms = new List<Atom>();
            if (c < cells.Count) CollectAtoms(cells[c], default, atoms);
            perCellAtoms[c] = atoms;
            float cellWidth = Math.Max(10f, colWidths[c] - 2 * padH);
            var lines = WrapAtoms(ctx, atoms, cellWidth, fontSizePx, lineHeightPx, bold);
            perCellLines[c] = lines;
            float h = 0f; foreach (var l in lines) h += l.Height;
            maxHeight = Math.Max(maxHeight, h + 2 * padV);
        }

        if (!measureOnly)
        {
            for (int c = 0; c < cols; c++)
            {
                var lines = perCellLines[c];
                var atoms = perCellAtoms[c];
                float textHeight = 0f; foreach (var l in lines) textHeight += l.Height;
                float cellTop = ctx.Y + (maxHeight - textHeight) / 2f;
                float cellLeft = colX[c] + padH;
                float cellWidth = Math.Max(10f, colWidths[c] - 2 * padH);
                var align = c < alignments.Count ? alignments[c] : ColumnAlignment.None;

                float runningY = cellTop;
                foreach (var line in lines)
                {
                    float lineX = cellLeft;
                    if (align == ColumnAlignment.Center) lineX = cellLeft + (cellWidth - line.Width) / 2f;
                    else if (align == ColumnAlignment.Right) lineX = cellLeft + (cellWidth - line.Width);
                    EmitWrappedLine(ctx, atoms, line, lineX, runningY, fontSizePx, ctx.Theme.BodyColor, bold);
                    runningY += line.Height;
                }
            }
        }

        return maxHeight;
    }

    // ------------------------------------------------------------------
    // <details>/<summary>
    // ------------------------------------------------------------------
    private static void LayoutDetails(Ctx ctx, DetailsBlock d, float x, float width)
    {
        bool expanded = ctx.State.DetailsExpanded.TryGetValue(d, out var v) ? v : d.DefaultOpen;

        float headerH = 36f * ctx.Scale;
        var headerRect = new SKRect(x, ctx.Y, x + width, ctx.Y + headerH);
        ctx.Boxes.Add(new RectBox { Bounds = headerRect, Fill = ctx.Theme.CodeBlockHeaderBackground, CornerRadius = ctx.Theme.CornerRadius * ctx.Scale });
        ctx.Boxes.Add(new DetailsHeaderBox { Bounds = headerRect, Source = d, Expanded = expanded });

        var font = ctx.Fonts.GetFont(ctx.Theme, false, ctx.Theme.BodyFontSize * ctx.Scale, true, false);
        float ascent = -font.Metrics.Ascent;
        float baselineY = ctx.Y + (headerH - (ascent + font.Metrics.Descent)) / 2f + ascent;
        ctx.Boxes.Add(new TextRunBox
        {
            Bounds = headerRect,
            Text = d.Summary,
            Font = font,
            Color = ctx.Theme.BodyColor,
            Baseline = new SKPoint(x + 34f * ctx.Scale, baselineY)
        });

        ctx.Y += headerH;

        if (expanded)
        {
            ctx.Y += ctx.Theme.BlockSpacing * ctx.Scale * 0.5f;
            LayoutBlocks(ctx, d.Blocks, x + 8f * ctx.Scale, width - 8f * ctx.Scale, tight: false, textColor: ctx.Theme.BodyColor, isFirstInParent: true);
        }
    }

    // ------------------------------------------------------------------
    // Unsupported raw HTML -- shown as inert, clearly-marked text rather than executed.
    // ------------------------------------------------------------------
    private static void LayoutRawHtml(Ctx ctx, RawHtmlBlock raw, float x, float width)
    {
        string text = TrimForDisplay(raw.Html);
        if (text.Length == 0) return;

        var atoms = new List<Atom>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            atoms.Add(new Atom { Text = words[i], IsCode = true });
            if (i < words.Length - 1) atoms.Add(new Atom { IsWhitespace = true, Text = " " });
        }

        float fontSizePx = ctx.Theme.SmallFontSize * ctx.Scale;
        float lineHeightPx = MathF.Ceiling(fontSizePx * ctx.Theme.CodeLineHeight);
        var lines = WrapAtoms(ctx, atoms, width, fontSizePx, lineHeightPx, false);
        foreach (var line in lines)
        {
            EmitWrappedLine(ctx, atoms, line, x, ctx.Y, fontSizePx, ctx.Theme.MutedColor, false);
            ctx.Y += line.Height;
        }
    }

    private static string TrimForDisplay(string html)
    {
        html = html.Trim();
        return html.Length > 400 ? html[..400] + "\u2026" : html;
    }

    // ------------------------------------------------------------------
    // Inline -> atoms
    // ------------------------------------------------------------------
    private static void CollectAtoms(List<MarkdownInline> inlines, AtomStyle style, List<Atom> output)
    {
        foreach (var inline in inlines) CollectAtom(inline, style, output);
    }

    private static void CollectAtom(MarkdownInline inline, AtomStyle style, List<Atom> output)
    {
        switch (inline)
        {
            case TextInline t:
                AppendWords(t.Text, style, output);
                break;
            case StrongInline s:
                { var st = style; st.Bold = true; CollectAtoms(s.Children, st, output); }
                break;
            case EmphasisInline e:
                { var st = style; st.Italic = true; CollectAtoms(e.Children, st, output); }
                break;
            case StrikethroughInline sk:
                { var st = style; st.Strike = true; CollectAtoms(sk.Children, st, output); }
                break;
            case LinkInline l:
                { var st = style; st.Link = l; CollectAtoms(l.Children, st, output); }
                break;
            case CodeSpanInline c:
                AppendCodeWords(c.Code, style, output);
                break;
            case AutoLinkInline a:
                {
                    var st = style;
                    st.Link = new LinkInline { Url = a.Url, Children = new List<MarkdownInline> { new TextInline { Text = a.DisplayText } } };
                    AppendWords(a.DisplayText, st, output);
                }
                break;
            case ImageInline img:
                output.Add(new Atom { Image = img, Link = style.Link });
                break;
            case LineBreakInline lb:
                output.Add(lb.Hard ? new Atom { ForceBreak = true } : new Atom { IsWhitespace = true, Text = " " });
                break;
            case InlineHtmlInline ih:
                {
                    var st = style;
                    if (ih.Kind == InlineHtmlKind.Subscript) st.Sub = true;
                    else if (ih.Kind == InlineHtmlKind.Superscript) st.Sup = true;
                    CollectAtoms(ih.Children, st, output);
                }
                break;
        }
    }

    private static void AppendWords(string text, AtomStyle style, List<Atom> output)
    {
        int i = 0; int n = text.Length;
        while (i < n)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                while (i < n && char.IsWhiteSpace(text[i])) i++;
                output.Add(new Atom { IsWhitespace = true, Text = " " });
                continue;
            }

            // Detect emoji run (characters that need the emoji fallback font)
            int emojiStart = i;
            bool inEmoji = false;
            while (i < n && !char.IsWhiteSpace(text[i]))
            {
                int cp = char.IsHighSurrogate(text[i]) && i + 1 < n && char.IsLowSurrogate(text[i + 1])
                    ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
                bool isEmojiChar = MarkdownEmojiTable.IsEmojiCodePoint(cp);

                if (isEmojiChar != inEmoji && i > emojiStart)
                {
                    // Flush accumulated run
                    output.Add(new Atom
                    {
                        Text = text[emojiStart..i],
                        Bold = style.Bold, Italic = style.Italic, Strike = style.Strike,
                        Subscript = style.Sub, Superscript = style.Sup,
                        Link = style.Link,
                        IsEmoji = inEmoji
                    });
                    emojiStart = i;
                }
                inEmoji = isEmojiChar;
                i += cp > 0xFFFF ? 2 : 1;
            }
            if (i > emojiStart)
            {
                output.Add(new Atom
                {
                    Text = text[emojiStart..i],
                    Bold = style.Bold, Italic = style.Italic, Strike = style.Strike,
                    Subscript = style.Sub, Superscript = style.Sup,
                    Link = style.Link,
                    IsEmoji = inEmoji
                });
            }
        }
    }

    private static void AppendCodeWords(string code, AtomStyle style, List<Atom> output)
    {
        int i = 0; int n = code.Length;
        while (i < n)
        {
            if (char.IsWhiteSpace(code[i]))
            {
                while (i < n && char.IsWhiteSpace(code[i])) i++;
                output.Add(new Atom { IsWhitespace = true, Text = " ", IsCode = true });
                continue;
            }
            int start = i;
            while (i < n && !char.IsWhiteSpace(code[i])) i++;
            output.Add(new Atom { Text = code[start..i], IsCode = true, Link = style.Link });
        }
    }

    // ------------------------------------------------------------------
    // Word-wrap + emission (shared by paragraphs, headings, table cells, raw-html fallback)
    // ------------------------------------------------------------------
    private static List<WrappedLine> WrapAtoms(Ctx ctx, List<Atom> atoms, float width, float fontSizePx, float lineHeightPx, bool forceBold)
    {
        var result = new List<WrappedLine>();
        if (atoms.Count == 0) return result;

        int s = 0, e = atoms.Count - 1;
        while (s <= e && atoms[s].IsWhitespace) s++;
        while (e >= s && atoms[e].IsWhitespace) e--;
        if (s > e) return result;

        int idx = s;
        while (idx <= e)
        {
            while (idx <= e && atoms[idx].IsWhitespace) idx++;
            if (idx > e) break;

            int lineStart = idx;
            float lineWidth = 0f;
            float lineHeightActual = lineHeightPx;

            while (idx <= e)
            {
                var atom = atoms[idx];
                if (atom.ForceBreak) { idx++; break; }

                float atomWidth; float atomHeight = lineHeightPx;
                if (atom.Image != null)
                {
                    var (w, h) = MeasureImageAtom(ctx, atom.Image, width);
                    atomWidth = w; atomHeight = Math.Max(h, lineHeightPx);
                }
                else
                {
                    SKFont font;
                    if (atom.IsEmoji)
                    {
                        var emojiTf = ctx.Fonts.GetEmojiTypeface();
                        float sz = fontSizePx;
                        font = emojiTf != null
                            ? new SKFont(emojiTf, sz)
                            : ctx.Fonts.GetFont(ctx.Theme, false, sz, false, false);
                    }
                    else
                    {
                        font = ctx.Fonts.GetFont(ctx.Theme, atom.IsCode,
                            atom.IsCode ? ctx.Theme.CodeFontSize * ctx.Scale : fontSizePx,
                            atom.Bold || forceBold, atom.Italic);
                    }
                    atomWidth = font.MeasureText(atom.Text);
                    if (atom.Subscript || atom.Superscript) atomWidth *= 0.85f;
                }

                bool fits = lineWidth + atomWidth <= width || idx == lineStart;
                if (!fits) break;

                lineWidth += atomWidth;
                lineHeightActual = Math.Max(lineHeightActual, atomHeight);
                idx++;
            }

            result.Add(new WrappedLine(lineStart, idx, lineWidth, lineHeightActual));
            if (idx <= e && atoms[idx].IsWhitespace) idx++;
        }

        return result;
    }

    private static void EmitInlineFlow(Ctx ctx, List<Atom> atoms, float x, float width, float fontSizeLogical, float lineHeightMultiplier, SKColor baseColor, bool forceBold)
    {
        float scaledSize = fontSizeLogical * ctx.Scale;
        float lineHeightPx = MathF.Ceiling(scaledSize * lineHeightMultiplier);
        var lines = WrapAtoms(ctx, atoms, width, scaledSize, lineHeightPx, forceBold);
        foreach (var line in lines)
        {
            EmitWrappedLine(ctx, atoms, line, x, ctx.Y, scaledSize, baseColor, forceBold);
            ctx.Y += line.Height;
        }
    }

    private static void EmitWrappedLine(Ctx ctx, List<Atom> atoms, WrappedLine line, float x, float y, float fontSizePx, SKColor baseColor, bool forceBold)
    {
        // ── Pass 1: compute the dominant (max-ascent) baseline so all runs align ──
        float maxAscent = 0f;
        for (int k = line.Start; k < line.End; k++)
        {
            var a = atoms[k];
            if (a.IsWhitespace || a.Image != null || a.ForceBreak) continue;
            float sz = a.IsCode ? ctx.Theme.CodeFontSize * ctx.Scale : fontSizePx;
            var f = ctx.Fonts.GetFont(ctx.Theme, a.IsCode, sz, a.Bold || forceBold, a.Italic);
            float asc = -f.Metrics.Ascent;
            if (a.Subscript)  asc -= sz * 0.18f;
            if (a.Superscript) asc += sz * 0.32f;
            maxAscent = Math.Max(maxAscent, asc);
        }
        if (maxAscent <= 0f)
        {
            // Fallback to base font
            var bf = ctx.Fonts.GetFont(ctx.Theme, false, fontSizePx, forceBold, false);
            maxAscent = -bf.Metrics.Ascent;
        }
        float baselineY = y + maxAscent;

        var baseFont = ctx.Fonts.GetFont(ctx.Theme, false, fontSizePx, forceBold, false);
        float cx = x;

        // Track pending inline-code run for background-pill emission
        int   codeRunStart = -1;
        float codeRunLeft  = 0f;
        float codeRunAsc   = 0f;
        float codeRunDesc  = 0f;

        void FlushCodeRun()
        {
            if (codeRunStart < 0) return;
            float padH   = 4f * ctx.Scale;
            float padV   = 2f * ctx.Scale;
            // Pill height is derived from CODE font metrics (not the full line height),
            // so it wraps the text tightly regardless of adjacent heading/body font size.
            float pillTop    = baselineY - codeRunAsc  - padV;
            float pillBottom = baselineY + codeRunDesc + padV;
            ctx.Boxes.Add(new RectBox
            {
                Bounds = new SKRect(codeRunLeft - padH, pillTop, cx + padH, pillBottom),
                Fill = ctx.Theme.CodeInlineBackground,
                CornerRadius = 4f * ctx.Scale
            });
            codeRunStart = -1;
        }

        for (int i = line.Start; i < line.End; i++)
        {
            var atom = atoms[i];

            if (atom.IsWhitespace)
            {
                FlushCodeRun();
                cx += baseFont.MeasureText(" ");
                continue;
            }

            if (atom.Image != null)
            {
                FlushCodeRun();
                var (w, h) = MeasureImageAtom(ctx, atom.Image, line.Width + 1f);
                ctx.Boxes.Add(new ImageBox { Bounds = new SKRect(cx, y, cx + w, y + h), Source = atom.Image, Link = atom.Link });
                cx += w;
                continue;
            }

            bool isCode  = atom.IsCode;
            bool isEmoji = atom.IsEmoji;
            float size   = isCode ? ctx.Theme.CodeFontSize * ctx.Scale : fontSizePx;
            var font     = isEmoji
                ? GetEmojiFont(ctx, size)
                : ctx.Fonts.GetFont(ctx.Theme, isCode, size, atom.Bold || forceBold, atom.Italic);

            float runAscent  = -font.Metrics.Ascent;
            float runDescent = font.Metrics.Descent;

            if (isCode)
            {
                if (codeRunStart < 0) { codeRunStart = i; codeRunLeft = cx; codeRunAsc = runAscent; codeRunDesc = runDescent; }
                codeRunAsc  = Math.Max(codeRunAsc,  runAscent);
                codeRunDesc = Math.Max(codeRunDesc, runDescent);
            }
            else
            {
                FlushCodeRun();
            }

            float w2 = font.MeasureText(atom.Text);
            SKColor color = isCode ? ctx.Theme.CodeForeground
                          : atom.Link != null ? ctx.Theme.LinkColor
                          : baseColor;

            float runBaseline = baselineY;
            if (atom.Subscript)   runBaseline += size * 0.18f;
            if (atom.Superscript) runBaseline -= size * 0.32f;

            ctx.Boxes.Add(new TextRunBox
            {
                Bounds   = new SKRect(cx, y, cx + w2, y + line.Height),
                Text     = atom.Text,
                Font     = font,
                Color    = color,
                Baseline = new SKPoint(cx, runBaseline),
                Link     = atom.Link,
                Underline = atom.Link != null,
                Strike   = atom.Strike,
                IsEmoji  = isEmoji
            });

            cx += w2;
        }

        FlushCodeRun();
    }

    private static SKFont GetEmojiFont(Ctx ctx, float sizePx)
    {
        var emojiTf = ctx.Fonts.GetEmojiTypeface();
        if (emojiTf == null) return ctx.Fonts.GetFont(ctx.Theme, false, sizePx, false, false);
        // Emoji fonts are not cached by MarkdownFontCache (different code path) — create a small
        // short-lived instance. Emoji rarely appear in large quantities, so this is acceptable.
        return new SKFont(emojiTf, sizePx) { Subpixel = true, Edging = SKFontEdging.Antialias };
    }

    private static (float Width, float Height) MeasureImageAtom(Ctx ctx, ImageInline image, float maxWidth)
    {
        var provider = ctx.State.ImageProvider;
        var cached = provider?.TryGetCached(image.Url);
        if (cached != null && cached.Width > 0 && cached.Height > 0)
        {
            float naturalW = cached.Width;
            float naturalH = cached.Height;
            float w = Math.Min(naturalW * ctx.Scale, Math.Max(40f, maxWidth));
            float h = naturalH * (w / naturalW);
            float maxH = ctx.Theme.MaxImageHeight * ctx.Scale;
            if (h > maxH) { h = maxH; w = naturalW * (h / naturalH); }
            return (w, h);
        }

        provider?.RequestLoad(image.Url, ctx.State.OnImageLoaded);
        float placeholderW = Math.Min(Math.Max(40f, maxWidth), 220f * ctx.Scale);
        float placeholderH = ctx.Theme.ImagePlaceholderHeight * ctx.Scale;
        return (placeholderW, placeholderH);
    }
}
