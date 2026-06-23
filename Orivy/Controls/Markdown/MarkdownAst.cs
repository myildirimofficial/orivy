using System.Collections.Generic;

namespace Orivy.Controls.Markdown;

/// <summary>Root of a parsed markdown document.</summary>
public sealed class MarkdownDocument
{
    public List<MarkdownBlock> Blocks { get; } = new();

    /// <summary>Link reference definitions ("[label]: url \"title\""), keyed case-insensitively.</summary>
    public Dictionary<string, LinkReferenceDefinition> LinkReferences { get; } =
        new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Flattened (Level, Text, Slug) outline, populated by the parser for quick TOC use.</summary>
    public List<(int Level, string Text, string Slug)> Outline { get; } = new();
}

public sealed class LinkReferenceDefinition
{
    public string Label = "";
    public string Url = "";
    public string? Title;
}

// ============================================================================
// Block-level nodes
// ============================================================================

public abstract class MarkdownBlock
{
    /// <summary>1-based source line, best-effort, for diagnostics only.</summary>
    public int SourceLine;
}

public sealed class HeadingBlock : MarkdownBlock
{
    public int Level = 1; // 1..6
    public List<MarkdownInline> Inlines = new();
    public string Slug = "";
}

public sealed class ParagraphBlock : MarkdownBlock
{
    public List<MarkdownInline> Inlines = new();
}

public sealed class ThematicBreakBlock : MarkdownBlock
{
}

public sealed class CodeBlockBlock : MarkdownBlock
{
    public string Code = "";
    public string? Language;
    public bool Fenced;
}

public enum ListKind { Unordered, Ordered }

public sealed class ListBlock : MarkdownBlock
{
    public ListKind Kind;
    public int StartNumber = 1;
    /// <summary>Tight lists render item paragraphs without extra inter-block spacing (CommonMark rule).</summary>
    public bool Tight = true;
    public char BulletChar = '-';
    public List<ListItemBlock> Items = new();
}

public sealed class ListItemBlock : MarkdownBlock
{
    public List<MarkdownBlock> Blocks = new();
    /// <summary>null = not a task item; otherwise the checked state of a GFM "- [ ]"/"- [x]" item.</summary>
    public bool? TaskChecked;
}

public enum AlertKind { None, Note, Tip, Important, Warning, Caution }

public sealed class BlockQuoteBlock : MarkdownBlock
{
    public List<MarkdownBlock> Blocks = new();
    /// <summary>Set when the quote starts with a GitHub alert marker, e.g. "&gt; [!NOTE]".</summary>
    public AlertKind AlertKind = AlertKind.None;
}

public enum ColumnAlignment { None, Left, Center, Right }

public sealed class TableBlock : MarkdownBlock
{
    public List<List<MarkdownInline>> HeaderCells = new();
    public List<List<List<MarkdownInline>>> Rows = new();
    public List<ColumnAlignment> Alignments = new();
}

/// <summary>A "&lt;details&gt;&lt;summary&gt;...&lt;/summary&gt; ... &lt;/details&gt;" collapsible section.</summary>
public sealed class DetailsBlock : MarkdownBlock
{
    public string Summary = "Details";
    public List<MarkdownBlock> Blocks = new();
    public bool DefaultOpen;
}

/// <summary>
/// Any HTML block not recognized by the limited safelist (details/summary). Rendered as
/// inert, escaped monospace text rather than executed, both for safety and simplicity.
/// </summary>
public sealed class RawHtmlBlock : MarkdownBlock
{
    public string Html = "";
}

// ============================================================================
// Inline-level nodes
// ============================================================================

public abstract class MarkdownInline
{
}

public sealed class TextInline : MarkdownInline
{
    public string Text = "";
}

public sealed class EmphasisInline : MarkdownInline
{
    public List<MarkdownInline> Children = new();
}

public sealed class StrongInline : MarkdownInline
{
    public List<MarkdownInline> Children = new();
}

public sealed class StrikethroughInline : MarkdownInline
{
    public List<MarkdownInline> Children = new();
}

public sealed class CodeSpanInline : MarkdownInline
{
    public string Code = "";
}

public sealed class LineBreakInline : MarkdownInline
{
    /// <summary>True = hard break (trailing "\\" or two+ trailing spaces). False = soft break (collapses to a space).</summary>
    public bool Hard;
}

public sealed class LinkInline : MarkdownInline
{
    public string Url = "";
    public string? Title;
    public List<MarkdownInline> Children = new();
}

public sealed class ImageInline : MarkdownInline
{
    public string Url = "";
    public string? Title;
    public string AltText = "";
}

public sealed class AutoLinkInline : MarkdownInline
{
    public string Url = "";
    public string DisplayText = "";
}

/// <summary>Small safelisted HTML inline tags we understand: sub/sup/kbd.</summary>
public enum InlineHtmlKind { Subscript, Superscript, KeyboardKey }

public sealed class InlineHtmlInline : MarkdownInline
{
    public InlineHtmlKind Kind;
    public List<MarkdownInline> Children = new();
}
