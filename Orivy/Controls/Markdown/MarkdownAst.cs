using System.Collections.Generic;

namespace Orivy.Controls.Markdown;

public sealed class MarkdownDocument
{
    public List<MarkdownBlock> Blocks { get; } = new();
    public Dictionary<string, LinkReferenceDefinition> LinkReferences { get; } =
        new(System.StringComparer.OrdinalIgnoreCase);
    public List<(int Level, string Text, string Slug)> Outline { get; } = new();
}

public sealed class LinkReferenceDefinition
{
    public string Label = ""; public string Url = ""; public string? Title;
}

// ── Blocks ─────────────────────────────────────────────────────────────────

public abstract class MarkdownBlock { public int SourceLine; }

public sealed class HeadingBlock : MarkdownBlock
{
    public int Level = 1; public List<MarkdownInline> Inlines = new(); public string Slug = "";
}

public sealed class ParagraphBlock : MarkdownBlock { public List<MarkdownInline> Inlines = new(); }

public sealed class ThematicBreakBlock : MarkdownBlock { }

public sealed class CodeBlockBlock : MarkdownBlock
{
    public string Code = ""; public string? Language; public bool Fenced;
}

public enum ListKind { Unordered, Ordered }

public sealed class ListBlock : MarkdownBlock
{
    public ListKind Kind; public int StartNumber = 1;
    public bool Tight = true; public char BulletChar = '-';
    public List<ListItemBlock> Items = new();
}

public sealed class ListItemBlock : MarkdownBlock
{
    public List<MarkdownBlock> Blocks = new(); public bool? TaskChecked;
}

public enum AlertKind { None, Note, Tip, Important, Warning, Caution }

public sealed class BlockQuoteBlock : MarkdownBlock
{
    public List<MarkdownBlock> Blocks = new(); public AlertKind AlertKind = AlertKind.None;
}

public enum ColumnAlignment { None, Left, Center, Right }

public sealed class TableBlock : MarkdownBlock
{
    public List<List<MarkdownInline>> HeaderCells = new();
    public List<List<List<MarkdownInline>>> Rows = new();
    public List<ColumnAlignment> Alignments = new();
}

public sealed class DetailsBlock : MarkdownBlock
{
    public string Summary = "Details"; public List<MarkdownBlock> Blocks = new(); public bool DefaultOpen;
}

/// <summary>Custom container block: `::: name ... :::` (markdown-it style).</summary>
public sealed class ContainerBlock : MarkdownBlock
{
    public string Name = ""; public List<MarkdownBlock> Blocks = new();
}

/// <summary>Definition list block: `Term\n  ~ Definition`.</summary>
public sealed class DefinitionListBlock : MarkdownBlock
{
    public List<DefinitionEntry> Entries = new();
}

public sealed class DefinitionEntry
{
    public List<MarkdownInline> Term = new();
    public List<List<MarkdownInline>> Definitions = new();
}

public sealed class RawHtmlBlock : MarkdownBlock { public string Html = ""; }

// ── Inlines ────────────────────────────────────────────────────────────────

public abstract class MarkdownInline { }

public sealed class TextInline    : MarkdownInline { public string Text = ""; }
public sealed class EmphasisInline : MarkdownInline { public List<MarkdownInline> Children = new(); }
public sealed class StrongInline   : MarkdownInline { public List<MarkdownInline> Children = new(); }
public sealed class StrikethroughInline : MarkdownInline { public List<MarkdownInline> Children = new(); }
public sealed class CodeSpanInline : MarkdownInline { public string Code = ""; }
public sealed class LineBreakInline : MarkdownInline { public bool Hard; }
public sealed class LinkInline : MarkdownInline
{
    public string Url = ""; public string? Title; public List<MarkdownInline> Children = new();
}
public sealed class ImageInline : MarkdownInline
{
    public string Url = ""; public string? Title; public string AltText = "";
}
public sealed class AutoLinkInline : MarkdownInline { public string Url = ""; public string DisplayText = ""; }

/// <summary>Underline: `<ins>text</ins>` or `++text++`.</summary>
public sealed class InsertInline : MarkdownInline { public List<MarkdownInline> Children = new(); }

/// <summary>Highlight: `<mark>text</mark>` or `==text==`.</summary>
public sealed class MarkInline : MarkdownInline { public List<MarkdownInline> Children = new(); }

/// <summary>Superscript: `^text^`.</summary>
public sealed class SuperscriptInline : MarkdownInline { public List<MarkdownInline> Children = new(); }

/// <summary>Subscript: `~text~` (single tilde; `~~` is strikethrough).</summary>
public sealed class SubscriptInline : MarkdownInline { public List<MarkdownInline> Children = new(); }

/// <summary>Safelisted HTML: kbd only (sub/sup/ins/mark handled as dedicated nodes above).</summary>
public sealed class InlineHtmlInline : MarkdownInline
{
    public List<MarkdownInline> Children = new();
    public string TagName = ""; // "kbd"
}

// ── Footnotes ──────────────────────────────────────────────────────────────

/// <summary>Inline footnote reference: `[^label]` renders as superscript link.</summary>
public sealed class FootnoteRefInline : MarkdownInline
{
    public string Label = "";   // The footnote label (e.g. "first")
    public int    Number;        // 1-based order of first use
}

/// <summary>Block that collects all footnote definitions at document end.</summary>
public sealed class FootnotesBlock : MarkdownBlock
{
    public List<FootnoteDefinition> Definitions = new();
}

public sealed class FootnoteDefinition
{
    public string            Label  = "";
    public int               Number;          // 1-based order
    public List<MarkdownBlock> Blocks = new();
}
