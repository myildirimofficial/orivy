// SPDX-License-Identifier: MIT
// Orivy RichText — Markdown AST
//
// Lightweight AST for a CommonMark subset. Nodes are immutable; the parser
// builds them and the Preview renderer walks them to produce runs.

using System.Collections.Generic;

namespace Orivy.Controls.RichText.Markdown;

/// <summary>Base class for all AST nodes.</summary>
public abstract class MarkdownNode { }

/// <summary>Base class for block-level nodes (a paragraph, heading, etc.).</summary>
public abstract class MarkdownBlock : MarkdownNode { }

/// <summary>Base class for inline nodes.</summary>
public abstract class MarkdownInline : MarkdownNode { }

// ── Block nodes ────────────────────────────────────────────────────────

/// <summary>A top-level document: a list of blocks.</summary>
public sealed class MarkdownDocument : MarkdownBlock
{
    public List<MarkdownBlock> Blocks { get; } = new();
}

/// <summary>Heading: level 1-6.</summary>
public sealed class HeadingBlock : MarkdownBlock
{
    public HeadingBlock(int level, List<MarkdownInline> inlines)
    {
        Level = level;
        Inlines = inlines;
    }
    public int Level { get; }
    public List<MarkdownInline> Inlines { get; }
}

/// <summary>Paragraph: a sequence of inline nodes.</summary>
public sealed class ParagraphBlock : MarkdownBlock
{
    public ParagraphBlock(List<MarkdownInline> inlines) => Inlines = inlines;
    public List<MarkdownInline> Inlines { get; }
}

/// <summary>Fenced code block: ``` lang\n...\n``` </summary>
public sealed class CodeBlock : MarkdownBlock
{
    public CodeBlock(string language, string code)
    {
        Language = language;
        Code = code;
    }
    public string Language { get; }   // empty if unspecified
    public string Code { get; }
}

/// <summary>Blockquote: contains nested blocks.</summary>
public sealed class BlockquoteBlock : MarkdownBlock
{
    public BlockquoteBlock(List<MarkdownBlock> children) => Children = children;
    public List<MarkdownBlock> Children { get; }
}

/// <summary>Unordered list.</summary>
public sealed class UnorderedListBlock : MarkdownBlock
{
    public UnorderedListBlock(List<ListItem> items) => Items = items;
    public List<ListItem> Items { get; }
}

/// <summary>Ordered list. Start is the starting number (default 1).</summary>
public sealed class OrderedListBlock : MarkdownBlock
{
    public OrderedListBlock(List<ListItem> items, int start = 1)
    {
        Items = items;
        Start = start;
    }
    public List<ListItem> Items { get; }
    public int Start { get; }
}

/// <summary>A list item. Contains block children (usually paragraphs).
/// If it's a task list item, IsTask=true and TaskChecked has the state.</summary>
public sealed class ListItem
{
    public ListItem(List<MarkdownBlock> children, bool isTask = false, bool taskChecked = false)
    {
        Children = children;
        IsTask = isTask;
        TaskChecked = taskChecked;
    }
    public List<MarkdownBlock> Children { get; }
    public bool IsTask { get; }
    public bool TaskChecked { get; }
}

/// <summary>Horizontal rule.</summary>
public sealed class HorizontalRuleBlock : MarkdownBlock { }

/// <summary>Table. Header row + body rows. Each cell contains inlines.</summary>
public sealed class TableBlock : MarkdownBlock
{
    public TableBlock(List<List<MarkdownInline>> header, List<List<List<MarkdownInline>>> body,
                      List<TextAlign> alignments)
    {
        Header = header;
        Body = body;
        Alignments = alignments;
    }
    public List<List<MarkdownInline>> Header { get; }
    public List<List<List<MarkdownInline>>> Body { get; }
    public List<TextAlign> Alignments { get; }  // per-column alignment
}

public enum TextAlign { Left, Center, Right }

// ── Inline nodes ───────────────────────────────────────────────────────

/// <summary>Plain text run.</summary>
public sealed class TextInline : MarkdownInline
{
    public TextInline(string text) => Text = text;
    public string Text { get; }
}

/// <summary>Bold (**text** or __text__). Contains nested inlines.</summary>
public sealed class BoldInline : MarkdownInline
{
    public BoldInline(List<MarkdownInline> children) => Children = children;
    public List<MarkdownInline> Children { get; }
}

/// <summary>Italic (*text* or _text_).</summary>
public sealed class ItalicInline : MarkdownInline
{
    public ItalicInline(List<MarkdownInline> children) => Children = children;
    public List<MarkdownInline> Children { get; }
}

/// <summary>Strikethrough (~~text~~).</summary>
public sealed class StrikethroughInline : MarkdownInline
{
    public StrikethroughInline(List<MarkdownInline> children) => Children = children;
    public List<MarkdownInline> Children { get; }
}

/// <summary>Inline code (`code`). Plain text, monospace.</summary>
public sealed class CodeInline : MarkdownInline
{
    public CodeInline(string code) => Code = code;
    public string Code { get; }
}

/// <summary>Link [text](url). Contains inlines for the text.</summary>
public sealed class LinkInline : MarkdownInline
{
    public LinkInline(List<MarkdownInline> children, string url, string? title = null)
    {
        Children = children;
        Url = url;
        Title = title;
    }
    public List<MarkdownInline> Children { get; }
    public string Url { get; }
    public string? Title { get; }
}

/// <summary>Image ![alt](url). Alt text + url.</summary>
public sealed class ImageInline : MarkdownInline
{
    public ImageInline(string alt, string url, string? title = null)
    {
        Alt = alt;
        Url = url;
        Title = title;
    }
    public string Alt { get; }
    public string Url { get; }
    public string? Title { get; }
}

/// <summary>Line break (hard break; two trailing spaces + newline or backslash + newline).</summary>
public sealed class LineBreakInline : MarkdownInline { }

/// <summary>Soft break (single newline inside a paragraph; rendered as space).</summary>
public sealed class SoftBreakInline : MarkdownInline { }
