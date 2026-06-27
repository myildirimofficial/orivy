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
        public bool Bold, Italic, Strike, Sub, Sup, Insert, Mark;
        public LinkInline? Link;
    }

    private struct Atom
    {
        public string Text;
        public bool IsWhitespace;
        public bool ForceBreak;
        public bool IsCode;
        public bool Bold, Italic, Strike, Subscript, Superscript;
        public bool IsEmoji;
        public bool Insert;   // underline decoration (<ins>/++++)
        public bool Mark;     // highlight background (<mark>/====)
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
        // Syntax highlight cache: avoids re-tokenizing code blocks when only layout changes
        public Dictionary<(string Code, string? Lang), List<List<SyntaxToken>>> SyntaxCache = new();
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
                case ContainerBlock cb2: LayoutContainer(ctx, cb2, x, width); break;
                case DefinitionListBlock dl: LayoutDefinitionList(ctx, dl, x, width); break;
                case RawHtmlBlock raw: LayoutRawHtml(ctx, raw, x, width); break;
                case FootnotesBlock fn: LayoutFootnotes(ctx, fn, x, width); break;
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
        var cacheKey = (cb.Code, cb.Language);
        if (!ctx.SyntaxCache.TryGetValue(cacheKey, out var tokensPerLine))
        {
            tokensPerLine = MarkdownSyntaxHighlighter.Tokenize(cb.Code, cb.Language);
            ctx.SyntaxCache[cacheKey] = tokensPerLine;
        }
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
                float w = text.Length <= 64 ? ctx.Fonts.MeasureText(text, ctx.Theme, true, codeFontPx, false, false) : codeFont.MeasureText(text);
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

        float viewportWidth = Math.Max(64f * ctx.Scale, width - 2 * padH);
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

        // ── Emit selectable flat TextRunBoxes for code lines (absolute coordinates) ──
        // These allow mouse/keyboard selection inside code blocks.
        {
            float absX    = outer.BodyOrigin.X;
            float absY    = outer.BodyOrigin.Y;
            var   nlFont  = codeFont;  // same font as code content
            for (int li = 0; li < lineBoxes.Count; li++)
            {
                float lineTop = absY + li * lineHeight;
                foreach (var run in lineBoxes[li])
                {
                    ctx.Boxes.Add(new TextRunBox
                    {
                        Bounds     = new SKRect(absX + run.Bounds.Left, lineTop,
                                                absX + run.Bounds.Right, lineTop + lineHeight),
                        Text       = run.Text,
                        Font       = run.Font,
                        Color      = run.Color,
                        Baseline   = new SKPoint(absX + run.Baseline.X, lineTop + ascent),
                        CodeOwner  = outer,
                    });
                }
                // Newline sentinel between lines so GetSelectedText can join them
                string nlText = li < lineBoxes.Count - 1 ? "\n" : "";
                if (!string.IsNullOrEmpty(nlText))
                {
                    ctx.Boxes.Add(new TextRunBox
                    {
                        Bounds    = new SKRect(absX, lineTop, absX + 1f, lineTop + lineHeight),
                        Text      = nlText,
                        Font      = nlFont,
                        Color     = ctx.Theme.MutedColor,
                        Baseline  = new SKPoint(absX, lineTop + ascent),
                        CodeOwner = outer,
                        IsNewlineSentinel = true,
                    });
                }
            }
        }

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
        float radius = ctx.Theme.CornerRadius * ctx.Scale;
        float hair = MathF.Max(1f, ctx.Scale);

        // ── Measure natural column widths (unconstrained) ──
        var natural = new float[cols];
        for (int c = 0; c < cols; c++)
            natural[c] = MeasureCellNaturalWidth(ctx, c < table.HeaderCells.Count ? table.HeaderCells[c] : new(), fontSizePx, true);
        foreach (var row in table.Rows)
            for (int c = 0; c < cols; c++)
                natural[c] = Math.Max(natural[c], MeasureCellNaturalWidth(ctx, c < row.Count ? row[c] : new(), fontSizePx, false));

        float minColW = 64f * ctx.Scale;
        float contentWidth = 0f;
        for (int c = 0; c < cols; c++) { natural[c] = MathF.Max(minColW, natural[c] + 2 * padH); contentWidth += natural[c]; }

        // ── Decide whether to scroll or squeeze columns ──
        bool needsScroll  = contentWidth > width;
        float viewportWidth = Math.Max(64f * ctx.Scale, width - 2 * padH);
        var scrollState   = ctx.State.GetOrCreateTableScroll(table);
        float maxScroll   = needsScroll ? MathF.Max(0f, contentWidth - viewportWidth) : 0f;
        scrollState.ScrollX = Math.Clamp(scrollState.ScrollX, 0f, maxScroll);

        var colWidths = new float[cols];
        if (!needsScroll)
        {
            // Distribute extra space proportionally
            float extra = (width - contentWidth) / cols;
            for (int c = 0; c < cols; c++) colWidths[c] = natural[c] + extra;
            contentWidth = width;
        }
        else
        {
            for (int c = 0; c < cols; c++) colWidths[c] = natural[c];
        }

        var colX = new float[cols + 1];
        colX[0] = 0f;   // relative to table content origin
        for (int c = 0; c < cols; c++) colX[c + 1] = colX[c] + colWidths[c];

        // ── Build children into a temporary sub-context ──
        var childBoxes = new List<MdBox>();
        var subCtx = new Ctx
        {
            Theme = ctx.Theme, Fonts = ctx.Fonts, Scale = ctx.Scale,
            State = ctx.State, Boxes = childBoxes, Y = 0f,
            HeadingPositions = new Dictionary<string, float>()
        };

        // Header row
        float startY = 0f;
        float headerRowH = LayoutTableRow(subCtx, table.HeaderCells, colX, colWidths, table.Alignments,
            padH, padV, fontSizePx, bold: true, measureOnly: true);
        // Header background
        childBoxes.Add(new RectBox { Bounds = new SKRect(0, startY, contentWidth, startY + headerRowH), Fill = ctx.Theme.TableHeaderBackground });
        LayoutTableRow(subCtx, table.HeaderCells, colX, colWidths, table.Alignments,
            padH, padV, fontSizePx, bold: true, measureOnly: false);
        subCtx.Y += headerRowH;
        childBoxes.Add(new RectBox { Bounds = new SKRect(0, subCtx.Y - hair, contentWidth, subCtx.Y), Fill = ctx.Theme.TableBorderColor });

        // Data rows with alternating background
        for (int ri = 0; ri < table.Rows.Count; ri++)
        {
            float rowY   = subCtx.Y;
            float rowH   = LayoutTableRow(subCtx, table.Rows[ri], colX, colWidths, table.Alignments,
                padH, padV, fontSizePx, bold: false, measureOnly: true);
            // Alt-row background (insert before row content)
            if (ri % 2 == 1)
                childBoxes.Add(new RectBox { Bounds = new SKRect(0, rowY, contentWidth, rowY + rowH), Fill = ctx.Theme.TableRowAltBackground });
            LayoutTableRow(subCtx, table.Rows[ri], colX, colWidths, table.Alignments,
                padH, padV, fontSizePx, bold: false, measureOnly: false);
            subCtx.Y += rowH;
        }

        float tableContentH = subCtx.Y;
        float thumbAreaH    = needsScroll ? 10f * ctx.Scale : 0f;
        float totalH        = tableContentH + thumbAreaH;

        // Column dividers
        for (int c = 1; c < cols; c++)
            childBoxes.Add(new RectBox { Bounds = new SKRect(colX[c], 0, colX[c] + hair, tableContentH), Fill = ctx.Theme.TableBorderColor });

        // Emit TableBox into the main boxes list
        float tableTop = ctx.Y;
        var tblBox = new TableBox
        {
            Bounds           = new SKRect(x, tableTop, x + viewportWidth, tableTop + totalH),
            Source           = table,
            Children         = childBoxes,
            ContentWidth     = contentWidth,
            ViewportWidth    = viewportWidth,
            Scroll           = scrollState,
            NeedsHorizontalScroll = needsScroll,
        };
        ctx.Boxes.Add(tblBox);
        ctx.Y += totalH;
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
    // Footnotes section
    // ------------------------------------------------------------------
    private static void LayoutFootnotes(Ctx ctx, FootnotesBlock fn, float x, float width)
    {
        if (fn.Definitions.Count == 0) return;
        // Register the footnotes section top as a named anchor "fn-<label>"
        // so #fn-label links (from FootnoteRefInline) can scroll here.
        ctx.HeadingPositions["footnotes"] = ctx.Y;

        float scale     = ctx.Scale;
        float hairH     = MathF.Max(1f, scale);
        float topPad    = 16f * scale;
        float leftPad   = 24f * scale;   // indent after the number
        float numW      = 24f * scale;

        // Separator line
        ctx.Y += topPad;
        ctx.Boxes.Add(new RectBox
        {
            Bounds = new SKRect(x, ctx.Y, x + Math.Min(width, 160f * scale), ctx.Y + hairH),
            Fill   = ctx.Theme.BorderColor
        });
        ctx.Y += hairH + topPad * 0.5f;

        float smallSizePx = ctx.Theme.SmallFontSize * scale;
        float lineH       = MathF.Ceiling(smallSizePx * ctx.Theme.BodyLineHeight);

        foreach (var def in fn.Definitions)
        {
            // Register anchor for #fn-<label> links
            ctx.HeadingPositions[$"fn-{def.Label.ToLowerInvariant()}"] = ctx.Y;
            float startY = ctx.Y;

            // Number label
            var numFont = ctx.Fonts.GetFont(ctx.Theme, false, smallSizePx, false, false);
            float numAscent = -numFont.Metrics.Ascent;
            ctx.Boxes.Add(new TextRunBox
            {
                Bounds   = new SKRect(x, ctx.Y, x + numW, ctx.Y + lineH),
                Text     = $"{def.Number}.",
                Font     = numFont,
                Color    = ctx.Theme.MutedColor,
                Baseline = new SKPoint(x, ctx.Y + (lineH - (numAscent + numFont.Metrics.Descent)) / 2f + numAscent)
            });

            // Content blocks indented
            float savedY = ctx.Y;
            LayoutBlocks(ctx, def.Blocks, x + numW, width - numW, tight: true,
                textColor: ctx.Theme.MutedColor, isFirstInParent: true);

            // Ensure at least one line height of spacing
            if (ctx.Y <= savedY) ctx.Y = savedY + lineH;
            ctx.Y += 4f * scale;  // gap between entries
        }

        ctx.Y += topPad * 0.5f;
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
            case InsertInline ins:
                { var st = style; st.Insert = true; CollectAtoms(ins.Children, st, output); }
                break;
            case MarkInline mk:
                { var st = style; st.Mark = true; CollectAtoms(mk.Children, st, output); }
                break;
            case SuperscriptInline sup:
                { var st = style; st.Sup = true; CollectAtoms(sup.Children, st, output); }
                break;
            case SubscriptInline sub:
                { var st = style; st.Sub = true; CollectAtoms(sub.Children, st, output); }
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
                // <kbd> etc. — just pass through with current style
                CollectAtoms(ih.Children, style, output);
                break;
            case FootnoteRefInline fn:
                // Render as superscript "¹", "[1]" style clickable ref
                {
                    var st = style;
                    st.Sup = true;
                    string display = fn.Number > 0 ? $"[{fn.Number}]" : $"[{fn.Label}]";
                    // Create a link that scrolls to footnote anchor
                    st.Link = new LinkInline { Url = $"#fn-{fn.Label.ToLowerInvariant()}", Children = new List<MarkdownInline>() };
                    AppendWords(display, st, output);
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
                    Mark = style.Mark, Insert = style.Insert,
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
                    Mark = style.Mark, Insert = style.Insert,
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
            output.Add(new Atom { Text = code[start..i], IsCode = true, Link = style.Link, Mark = style.Mark, Insert = style.Insert });
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
                        font = GetEmojiFont(ctx, fontSizePx);
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
        const float SubSupScale = 0.72f;

        // ── Pass 1: dominant (max-ascent) baseline ──
        float maxAscent = 0f;
        for (int k = line.Start; k < line.End; k++)
        {
            var a = atoms[k];
            if (a.IsWhitespace || a.Image != null || a.ForceBreak) continue;
            bool isSS = a.Subscript || a.Superscript;
            float sz  = a.IsCode ? ctx.Theme.CodeFontSize * ctx.Scale
                      : isSS    ? fontSizePx * SubSupScale : fontSizePx;
            var f  = ctx.Fonts.GetFont(ctx.Theme, a.IsCode, sz, a.Bold || forceBold, a.Italic);
            float asc = -f.Metrics.Ascent;
            if (a.Superscript) asc += fontSizePx * SubSupScale * 0.55f;
            maxAscent = Math.Max(maxAscent, asc);
        }
        if (maxAscent <= 0f)
            maxAscent = -ctx.Fonts.GetFont(ctx.Theme, false, fontSizePx, forceBold, false).Metrics.Ascent;
        float baselineY = y + maxAscent;
        var baseFont = ctx.Fonts.GetFont(ctx.Theme, false, fontSizePx, forceBold, false);
        float cx = x;

        int   codeRunStart = -1; float codeRunLeft = 0f, codeRunAsc = 0f, codeRunDesc = 0f;
        int   markRunStart = -1; float markRunLeft = 0f, markTop = y, markBottom = y + line.Height;

        void FlushCodeRun()
        {
            if (codeRunStart < 0) return;
            float padH = 4f * ctx.Scale, padV = 2f * ctx.Scale;
            ctx.Boxes.Add(new RectBox { Bounds = new SKRect(codeRunLeft - padH, baselineY - codeRunAsc - padV, cx + padH, baselineY + codeRunDesc + padV), Fill = ctx.Theme.CodeInlineBackground, CornerRadius = 4f * ctx.Scale });
            codeRunStart = -1;
        }

        void FlushMarkRun()
        {
            if (markRunStart < 0) return;
            ctx.Boxes.Add(new RectBox { Bounds = new SKRect(markRunLeft, markTop + ctx.Scale, cx, markBottom - ctx.Scale), Fill = ctx.Theme.MarkBackground, CornerRadius = 3f * ctx.Scale });
            markRunStart = -1;
        }

        for (int i = line.Start; i < line.End; i++)
        {
            var atom = atoms[i];
            if (atom.IsWhitespace)
            {
                float spW = baseFont.MeasureText(" ");
                if (!atom.Mark) { FlushCodeRun(); FlushMarkRun(); } else { cx += spW; continue; }
                cx += spW; continue;
            }
            if (atom.Image != null)
            {
                FlushCodeRun(); FlushMarkRun();
                var (w, h) = MeasureImageAtom(ctx, atom.Image, line.Width + 1f);
                ctx.Boxes.Add(new ImageBox { Bounds = new SKRect(cx, y, cx + w, y + h), Source = atom.Image, Link = atom.Link });
                cx += w; continue;
            }

            bool isSS = atom.Subscript || atom.Superscript;
            float size = atom.IsCode ? ctx.Theme.CodeFontSize * ctx.Scale
                       : isSS       ? fontSizePx * SubSupScale : fontSizePx;
            var font = atom.IsEmoji ? GetEmojiFont(ctx, size)
                     : ctx.Fonts.GetFont(ctx.Theme, atom.IsCode, size, atom.Bold || forceBold, atom.Italic);
            float runAsc = -font.Metrics.Ascent, runDesc = font.Metrics.Descent;

            if (atom.IsCode) { if (codeRunStart < 0) { codeRunStart = i; codeRunLeft = cx; codeRunAsc = runAsc; codeRunDesc = runDesc; } codeRunAsc = Math.Max(codeRunAsc, runAsc); codeRunDesc = Math.Max(codeRunDesc, runDesc); }
            else FlushCodeRun();

            if (atom.Mark) { if (markRunStart < 0) { markRunStart = i; markRunLeft = cx; markTop = baselineY - runAsc; markBottom = baselineY + runDesc; } markTop = Math.Min(markTop, baselineY - runAsc); markBottom = Math.Max(markBottom, baselineY + runDesc); }
            else FlushMarkRun();

            float w2 = font.MeasureText(atom.Text);
            SKColor color = atom.IsCode ? ctx.Theme.CodeForeground : atom.Mark ? ctx.Theme.MarkColor : atom.Link != null ? ctx.Theme.LinkColor : baseColor;

            float bl = baselineY;
            if (atom.Superscript) bl = baselineY - fontSizePx * SubSupScale * 0.55f;
            if (atom.Subscript)   bl = baselineY + fontSizePx * SubSupScale * 0.12f;

            ctx.Boxes.Add(new TextRunBox { Bounds = new SKRect(cx, y, cx + w2, y + line.Height), Text = atom.Text, Font = font, Color = color, Baseline = new SKPoint(cx, bl), Link = atom.Link, Underline = atom.Link != null || atom.Insert, Strike = atom.Strike, Mark = atom.Mark, IsEmoji = atom.IsEmoji });
            cx += w2;
        }
        FlushCodeRun(); FlushMarkRun();
    }
    private static SKFont GetEmojiFont(Ctx ctx, float sizePx)
    {
        // Route through the font cache so emoji fonts are reused, not re-created each call.
        return ctx.Fonts.GetEmojiFont(ctx.Theme, sizePx);
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

    // ------------------------------------------------------------------
    // Container blocks (:::)
    // ------------------------------------------------------------------
    private static void LayoutContainer(Ctx ctx, ContainerBlock container, float x, float width)
    {
        var (borderColor, bgColor) = ResolveContainerColors(ctx.Theme, container.Name);

        float pad    = 16f * ctx.Scale;
        float barW   =  4f * ctx.Scale;
        float radius =  6f * ctx.Scale;
        float startY = ctx.Y;

        // Name label (bold, coloured)
        if (!string.IsNullOrEmpty(container.Name))
        {
            float labelSize = ctx.Theme.SmallFontSize * ctx.Scale;
            var labelFont   = ctx.Fonts.GetFont(ctx.Theme, false, labelSize, true, false);
            float lAsc = -labelFont.Metrics.Ascent;
            float labelLineH = MathF.Ceiling(labelSize * 1.4f);
            ctx.Boxes.Add(new TextRunBox
            {
                Bounds   = new SKRect(x + barW + pad, ctx.Y, x + width, ctx.Y + labelLineH),
                Text     = container.Name.ToUpperInvariant(),
                Font     = labelFont,
                Color    = borderColor,
                Baseline = new SKPoint(x + barW + pad, ctx.Y + lAsc)
            });
            ctx.Y += labelLineH + 4f * ctx.Scale;
        }

        // Inner content
        float contentX = x + barW + pad;
        LayoutBlocks(ctx, container.Blocks, contentX, width - barW - pad, tight: false,
            textColor: ctx.Theme.BodyColor, isFirstInParent: true);

        ctx.Y += pad * 0.5f;
        float endY = ctx.Y;

        // Background fill
        if (bgColor.Alpha > 0)
        {
            var bgRect = new SKRect(x, startY - 4f * ctx.Scale, x + width, endY + 4f * ctx.Scale);
            ctx.Boxes.Insert(ctx.Boxes.Count - (int)((endY - startY) / 5 + 1),   // best-effort: insert before content
                new RectBox { Bounds = bgRect, Fill = bgColor, CornerRadius = radius });
        }

        // Left bar
        ctx.Boxes.Add(new RectBox
        {
            Bounds = new SKRect(x, startY - 4f * ctx.Scale, x + barW, endY + 4f * ctx.Scale),
            Fill   = borderColor, CornerRadius = barW / 2f
        });
    }

    private static (SKColor Border, SKColor Bg) ResolveContainerColors(MarkdownTheme t, string name)
    {
        return name.ToLowerInvariant() switch
        {
            "warning" or "warn"    => (t.ContainerWarningBorder, t.ContainerWarningBg),
            "danger"  or "error"   => (t.ContainerDangerBorder,  t.ContainerDangerBg),
            "info"    or "note"    => (t.ContainerInfoBorder,     t.ContainerInfoBg),
            "tip"     or "success" => (t.ContainerTipBorder,      t.ContainerTipBg),
            _                      => (t.ContainerDefaultBorder,  t.ContainerDefaultBg),
        };
    }

    // ------------------------------------------------------------------
    // Definition lists
    // ------------------------------------------------------------------
    private static void LayoutDefinitionList(Ctx ctx, DefinitionListBlock dl, float x, float width)
    {
        float termSize = ctx.Theme.BodyFontSize * ctx.Scale;
        float defSize  = ctx.Theme.BodyFontSize * ctx.Scale;
        float indent   = 24f * ctx.Scale;
        bool first     = true;

        foreach (var entry in dl.Entries)
        {
            if (!first) ctx.Y += ctx.Theme.TightBlockSpacing * ctx.Scale;

            // Term (bold)
            var termAtoms = new List<Atom>();
            CollectAtoms(entry.Term, default, termAtoms);
            if (termAtoms.Count > 0)
            {
                var termStyle = default(AtomStyle); termStyle.Bold = true;
                var boldTermAtoms = new List<Atom>();
                foreach (var a in termAtoms)
                {
                    var ba = a; ba.Bold = true; boldTermAtoms.Add(ba);
                }
                EmitInlineFlow(ctx, boldTermAtoms, x, width, ctx.Theme.BodyFontSize, ctx.Theme.BodyLineHeight,
                    ctx.Theme.BodyColor, forceBold: false);
            }

            // Definitions (indented, body color)
            foreach (var def in entry.Definitions)
            {
                var defAtoms = new List<Atom>();
                CollectAtoms(def, default, defAtoms);
                if (defAtoms.Count > 0)
                    EmitInlineFlow(ctx, defAtoms, x + indent, width - indent, ctx.Theme.BodyFontSize,
                        ctx.Theme.BodyLineHeight, ctx.Theme.MutedColor, forceBold: false);
            }

            first = false;
        }
    }
}
