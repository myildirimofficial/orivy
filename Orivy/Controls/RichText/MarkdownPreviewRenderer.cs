// SPDX-License-Identifier: MIT
// Orivy RichText — Markdown Preview Renderer
//
// Used in MarkdownPreview mode: walks the AST produced by MarkdownParser and
// produces a NEW StyledTextDocument where the source markdown syntax is
// hidden and only the rendered output remains. For example:
//   source: "**hello**"
//   rendered text: "hello" with TextStyle.Bold
//
// The resulting document is read-only from the user's perspective (caret
// is disabled in preview mode). To edit, the user toggles back to source mode.

using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Orivy.Controls.RichText.Markdown;

/// <summary>
/// Renders an AST into a StyledTextDocument for preview display.
/// </summary>
public sealed class MarkdownPreviewRenderer
{
    // Theme palette (can be overridden by the consumer).
    public SKColor HeadingColor { get; set; } = new(0x1A, 0x1A, 0x1A);
    public SKColor TextColor { get; set; } = new(0x33, 0x33, 0x33);
    public SKColor CodeColor { get; set; } = new(0xC5, 0x76, 0x33);
    public SKColor CodeBgColor { get; set; } = new(0xF4, 0xF4, 0xF4, 0xC0);
    public SKColor QuoteColor { get; set; } = new(0x66, 0x66, 0x66);
    public SKColor QuoteBarColor { get; set; } = new(0xCC, 0xCC, 0xCC);
    public SKColor LinkColor { get; set; } = new(0x1F, 0x6F, 0xEB);
    public SKColor HrColor { get; set; } = new(0xDD, 0xDD, 0xDD);
    public SKColor BulletColor { get; set; } = new(0x33, 0x33, 0x33);
    public SKColor TaskDoneColor { get; set; } = new(0x4C, 0xAF, 0x50);
    public SKColor TaskPendingColor { get; set; } = new(0x99, 0x99, 0x99);

    private readonly MarkdownParser _parser = new();

    /// <summary>Render markdown source into a styled document.</summary>
    public StyledTextDocument Render(string source)
    {
        var ast = _parser.Parse(source);
        return RenderDocument(ast);
    }

    private StyledTextDocument RenderDocument(MarkdownDocument doc)
    {
        var sb = new StringBuilder();
        var runs = new List<TextRun>();
        var ctx = new RenderContext(sb, runs);

        for (var i = 0; i < doc.Blocks.Count; i++)
        {
            RenderBlock(doc.Blocks[i], ctx);
            // Add a blank line between blocks (except after the last).
            if (i < doc.Blocks.Count - 1)
                ctx.Append("\n\n", TextStyle.Default);
        }

        var document = new StyledTextDocument();
        document.Load(sb.ToString(), runs);
        return document;
    }

    private void RenderBlock(MarkdownBlock block, RenderContext ctx)
    {
        switch (block)
        {
            case HeadingBlock h:
                RenderHeading(h, ctx);
                break;
            case ParagraphBlock p:
                RenderInlines(p.Inlines, ctx, TextStyle.Default.With(foreColor: TextColor));
                break;
            case CodeBlock code:
                RenderCodeBlock(code, ctx);
                break;
            case BlockquoteBlock bq:
                RenderBlockquote(bq, ctx);
                break;
            case UnorderedListBlock ul:
                RenderUnorderedList(ul, ctx);
                break;
            case OrderedListBlock ol:
                RenderOrderedList(ol, ctx);
                break;
            case HorizontalRuleBlock:
                RenderHorizontalRule(ctx);
                break;
            case TableBlock table:
                RenderTable(table, ctx);
                break;
        }
    }

    private void RenderHeading(HeadingBlock h, RenderContext ctx)
    {
        var style = new TextStyle
        {
            Bold = true,
            ForeColor = HeadingColor,
            FontSize = h.Level switch
            {
                1 => 26f,
                2 => 22f,
                3 => 18f,
                4 => 16f,
                5 => 14f,
                _ => 13f,
            },
        };
        RenderInlines(h.Inlines, ctx, style);
    }

    private void RenderCodeBlock(CodeBlock code, RenderContext ctx)
    {
        var style = new TextStyle
        {
            Monospace = true,
            ForeColor = CodeColor,
            BackColor = CodeBgColor,
        };
        ctx.Append(code.Code, style);
    }

    private void RenderBlockquote(BlockquoteBlock bq, RenderContext ctx)
    {
        var style = new TextStyle { Italic = true, ForeColor = QuoteColor };
        // Render children with quote style applied.
        for (var i = 0; i < bq.Children.Count; i++)
        {
            RenderBlockWithStyle(bq.Children[i], ctx, style);
            if (i < bq.Children.Count - 1)
                ctx.Append("\n", style);
        }
    }

    private void RenderBlockWithStyle(MarkdownBlock block, RenderContext ctx, TextStyle style)
    {
        // Recursively render but apply the style to all text.
        switch (block)
        {
            case ParagraphBlock p:
                RenderInlines(p.Inlines, ctx, style);
                break;
            case HeadingBlock h:
                RenderInlines(h.Inlines, ctx, style.Merge(new TextStyle { Bold = true }));
                break;
            default:
                RenderBlock(block, ctx);
                break;
        }
    }

    private void RenderUnorderedList(UnorderedListBlock ul, RenderContext ctx)
    {
        for (var i = 0; i < ul.Items.Count; i++)
        {
            var item = ul.Items[i];
            var prefix = item.IsTask
                ? (item.TaskChecked ? "[\u2611] " : "[\u2610] ")
                : "\u2022  ";  // bullet + spaces
            var prefixStyle = item.IsTask
                ? new TextStyle { ForeColor = item.TaskChecked ? TaskDoneColor : TaskPendingColor }
                : new TextStyle { ForeColor = BulletColor };

            ctx.Append(prefix, prefixStyle);
            RenderItemBody(item, ctx);
            if (i < ul.Items.Count - 1)
                ctx.Append("\n", TextStyle.Default);
        }
    }

    private void RenderOrderedList(OrderedListBlock ol, RenderContext ctx)
    {
        for (var i = 0; i < ol.Items.Count; i++)
        {
            var item = ol.Items[i];
            var prefix = $"{ol.Start + i}. ";
            ctx.Append(prefix, new TextStyle { ForeColor = BulletColor });
            RenderItemBody(item, ctx);
            if (i < ol.Items.Count - 1)
                ctx.Append("\n", TextStyle.Default);
        }
    }

    private void RenderItemBody(ListItem item, RenderContext ctx)
    {
        for (var i = 0; i < item.Children.Count; i++)
        {
            RenderBlock(item.Children[i], ctx);
            if (i < item.Children.Count - 1)
                ctx.Append("\n", TextStyle.Default);
        }
    }

    private void RenderHorizontalRule(RenderContext ctx)
    {
        var line = new string('\u2500', 40);  // box drawing horizontal
        ctx.Append(line, new TextStyle { ForeColor = HrColor });
    }

    private void RenderTable(TableBlock table, RenderContext ctx)
    {
        // Simple text-based table render: tab-separated cells, with the
        // header row bolded. A real implementation would render SkiaSharp
        // rects around cells; that requires layout cooperation with the
        // RichTextBox. For v1 we keep it text-only.
        var maxCols = table.Alignments.Count;
        for (var i = 0; i < table.Header.Count; i++)
        {
            RenderInlines(table.Header[i], ctx, new TextStyle { Bold = true });
            if (i < table.Header.Count - 1)
                ctx.Append(" \u2502 ", TextStyle.Default);
        }
        ctx.Append("\n", TextStyle.Default);

        // Separator line.
        for (var i = 0; i < maxCols; i++)
        {
            ctx.Append(new string('\u2500', 8), new TextStyle { ForeColor = HrColor });
            if (i < maxCols - 1)
                ctx.Append("\u253C", new TextStyle { ForeColor = HrColor });
        }
        ctx.Append("\n", TextStyle.Default);

        foreach (var row in table.Body)
        {
            for (var i = 0; i < row.Count; i++)
            {
                RenderInlines(row[i], ctx, TextStyle.Default);
                if (i < row.Count - 1)
                    ctx.Append(" \u2502 ", TextStyle.Default);
            }
            ctx.Append("\n", TextStyle.Default);
        }
    }

    private void RenderInlines(List<MarkdownInline> inlines, RenderContext ctx, TextStyle inherited)
    {
        foreach (var inline in inlines)
            RenderInline(inline, ctx, inherited);
    }

    private void RenderInline(MarkdownInline inline, RenderContext ctx, TextStyle inherited)
    {
        switch (inline)
        {
            case TextInline t:
                ctx.Append(t.Text, inherited);
                break;
            case BoldInline b:
                RenderInlines(b.Children, ctx, inherited.Merge(TextStyle.BoldStyle));
                break;
            case ItalicInline it:
                RenderInlines(it.Children, ctx, inherited.Merge(TextStyle.ItalicStyle));
                break;
            case StrikethroughInline s:
                RenderInlines(s.Children, ctx, inherited.Merge(TextStyle.StrikethroughStyle));
                break;
            case CodeInline c:
                ctx.Append(c.Code, inherited.Merge(new TextStyle
                {
                    Monospace = true,
                    ForeColor = CodeColor,
                    BackColor = CodeBgColor,
                }));
                break;
            case LinkInline l:
                RenderInlines(l.Children, ctx, inherited.Merge(new TextStyle
                {
                    ForeColor = LinkColor,
                    Underline = true,
                    Hyperlink = l.Url,
                }));
                break;
            case ImageInline img:
                ctx.Append($"[{img.Alt}]", inherited.Merge(new TextStyle
                {
                    Italic = true,
                    ForeColor = LinkColor,
                }));
                break;
            case LineBreakInline:
                ctx.Append("\n", inherited);
                break;
            case SoftBreakInline:
                ctx.Append(" ", inherited);
                break;
        }
    }

    /// <summary>Helper that accumulates text + runs while tracking the current position.</summary>
    private sealed class RenderContext
    {
        private readonly StringBuilder _sb;
        private readonly List<TextRun> _runs;

        public RenderContext(StringBuilder sb, List<TextRun> runs)
        {
            _sb = sb;
            _runs = runs;
        }

        public void Append(string text, TextStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var start = _sb.Length;
            _sb.Append(text);
            // Merge with previous run if same style and adjacent.
            if (_runs.Count > 0 && _runs[^1].End == start && _runs[^1].Style.Equals(style))
                _runs[^1] = _runs[^1].WithLength(_runs[^1].Length + text.Length);
            else
                _runs.Add(new TextRun(start, text.Length, style));
        }
    }
}
