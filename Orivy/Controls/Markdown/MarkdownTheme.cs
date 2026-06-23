using SkiaSharp;

namespace Orivy.Controls.Markdown;

/// <summary>
/// Visual scale + palette used to render a <see cref="MarkdownDocument"/>.
/// All sizes here are "logical" (96 DPI) pixels; <see cref="MarkdownLayoutBuilder"/>
/// multiplies them by the host control's ScaleFactor at layout time -- the same
/// convention <c>ElementBase.CreateRenderFont</c> uses internally.
///
/// Construct via <see cref="Light"/> / <see cref="Dark"/> for ready GitHub-like
/// presets, or instantiate directly and override individual members.
/// </summary>
public sealed class MarkdownTheme
{
    // ----- Typeface families (resolved in priority order; first match wins) -----
    public string[] BodyFontFamilies { get; set; } =
    {
        "Segoe UI Variable Text", "Segoe UI", "Inter", "Helvetica Neue", "Helvetica", "Noto Sans", "Arial"
    };

    public string[] MonospaceFontFamilies { get; set; } =
    {
        "Cascadia Mono", "Cascadia Code", "JetBrains Mono", "Fira Code", "Consolas",
        "SF Mono", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New"
    };

    /// <summary>
    /// When true, the body typeface falls back to <c>Application.SharedDefaultFont</c>'s
    /// typeface instead of <see cref="BodyFontFamilies"/>, keeping the document visually
    /// consistent with the rest of the host application's chrome.
    /// </summary>
    public bool UseHostDefaultFontForBody { get; set; } = true;

    // ----- Type scale (logical px) -----
    public float BodyFontSize { get; set; } = 16f;

    /// <summary>Font sizes for H1..H6, indices 0..5.</summary>
    public float[] HeadingFontSizes { get; set; } = { 32f, 24f, 20f, 16f, 14f, 13f };

    public float CodeFontSize { get; set; } = 13.5f;
    public float SmallFontSize { get; set; } = 13f;

    public float BodyLineHeight { get; set; } = 1.6f;
    public float HeadingLineHeight { get; set; } = 1.3f;
    public float CodeLineHeight { get; set; } = 1.5f;

    // ----- Spacing (logical px) -----
    public float BlockSpacing { get; set; } = 16f;
    public float TightBlockSpacing { get; set; } = 4f;
    public float HeadingSpacingTop { get; set; } = 24f;
    public float HeadingSpacingBottom { get; set; } = 16f;
    public float ListIndent { get; set; } = 28f;
    public float BlockquoteIndent { get; set; } = 20f;
    public float BlockquoteBarWidth { get; set; } = 4f;
    public float CodeBlockPaddingH { get; set; } = 16f;
    public float CodeBlockPaddingV { get; set; } = 14f;
    public float CodeBlockHeaderHeight { get; set; } = 34f;
    public float TableCellPaddingH { get; set; } = 13f;
    public float TableCellPaddingV { get; set; } = 8f;
    public float ThematicBreakHeight { get; set; } = 4f;
    public float CornerRadius { get; set; } = 6f;
    public float CheckboxSize { get; set; } = 16f;
    public float ImagePlaceholderHeight { get; set; } = 120f;
    public float MaxImageHeight { get; set; } = 520f;

    // ----- Palette -----
    public SKColor BodyColor { get; set; } = new(0x1F, 0x23, 0x28);
    public SKColor MutedColor { get; set; } = new(0x59, 0x63, 0x6E);
    public SKColor HeadingColor { get; set; } = new(0x1F, 0x23, 0x28);
    public SKColor HeadingBorderColor { get; set; } = new(0xD1, 0xD9, 0xE0);
    public SKColor LinkColor { get; set; } = new(0x09, 0x69, 0xDA);
    public SKColor LinkHoverColor { get; set; } = new(0x0A, 0x50, 0xA1);
    public SKColor BorderColor { get; set; } = new(0xD1, 0xD9, 0xE0);
    public SKColor SurfaceBackground { get; set; } = SKColors.Transparent;
    public SKColor CodeBackground { get; set; } = new(0xF6, 0xF8, 0xFA);
    public SKColor CodeBlockHeaderBackground { get; set; } = new(0xEC, 0xF0, 0xF3);
    public SKColor CodeInlineBackground { get; set; } = new(0x81, 0x8B, 0x98, 0x2D);
    public SKColor CodeForeground { get; set; } = new(0x1F, 0x23, 0x28);
    public SKColor BlockquoteBarColor { get; set; } = new(0xD1, 0xD9, 0xE0);
    public SKColor TableHeaderBackground { get; set; } = new(0xF6, 0xF8, 0xFA);
    public SKColor TableBorderColor { get; set; } = new(0xD1, 0xD9, 0xE0);
    public SKColor CheckboxBorderColor { get; set; } = new(0x8C, 0x95, 0x9F);
    public SKColor CheckboxFillColor { get; set; } = new(0x09, 0x69, 0xDA);
    public SKColor CheckmarkColor { get; set; } = SKColors.White;
    public SKColor SelectionBackground { get; set; } = new(0x09, 0x69, 0xDA, 0x40);
    public SKColor ScrollIndicatorColor { get; set; } = new(0x00, 0x00, 0x00, 0x55);

    // Syntax highlight palette
    public SKColor SyntaxKeyword { get; set; } = new(0xCF, 0x22, 0x2E);
    public SKColor SyntaxString { get; set; } = new(0x0A, 0x30, 0x69);
    public SKColor SyntaxComment { get; set; } = new(0x6E, 0x77, 0x81);
    public SKColor SyntaxNumber { get; set; } = new(0x00, 0x5C, 0xC5);
    public SKColor SyntaxType { get; set; } = new(0x95, 0x03, 0x00);
    public SKColor SyntaxFunction { get; set; } = new(0x6F, 0x42, 0xC1);
    public SKColor SyntaxAttribute { get; set; } = new(0x11, 0x67, 0x29);
    public SKColor SyntaxTag { get; set; } = new(0x11, 0x67, 0x29);

    // GitHub-style alert colors (> [!NOTE] etc.)
    public SKColor AlertNote { get; set; } = new(0x09, 0x69, 0xDA);
    public SKColor AlertTip { get; set; } = new(0x1A, 0x7F, 0x37);
    public SKColor AlertImportant { get; set; } = new(0x82, 0x50, 0xDF);
    public SKColor AlertWarning { get; set; } = new(0x9A, 0x67, 0x00);
    public SKColor AlertCaution { get; set; } = new(0xCF, 0x22, 0x2E);

    public static MarkdownTheme Light() => new();

    public static MarkdownTheme Dark() => new()
    {
        BodyColor = new SKColor(0xE6, 0xED, 0xF3),
        MutedColor = new SKColor(0x9B, 0xA7, 0xB3),
        HeadingColor = new SKColor(0xE6, 0xED, 0xF3),
        HeadingBorderColor = new SKColor(0x30, 0x36, 0x3D),
        LinkColor = new SKColor(0x4A, 0x93, 0xF8),
        LinkHoverColor = new SKColor(0x7A, 0xB7, 0xFF),
        BorderColor = new SKColor(0x30, 0x36, 0x3D),
        CodeBackground = new SKColor(0x16, 0x1B, 0x22),
        CodeBlockHeaderBackground = new SKColor(0x1C, 0x21, 0x28),
        CodeInlineBackground = new SKColor(0x6E, 0x76, 0x81, 0x4D),
        CodeForeground = new SKColor(0xE6, 0xED, 0xF3),
        BlockquoteBarColor = new SKColor(0x30, 0x36, 0x3D),
        TableHeaderBackground = new SKColor(0x16, 0x1B, 0x22),
        TableBorderColor = new SKColor(0x30, 0x36, 0x3D),
        CheckboxBorderColor = new SKColor(0x6E, 0x76, 0x81),
        CheckboxFillColor = new SKColor(0x4A, 0x93, 0xF8),
        CheckmarkColor = new SKColor(0x0D, 0x11, 0x17),
        SelectionBackground = new SKColor(0x4A, 0x93, 0xF8, 0x40),
        ScrollIndicatorColor = new SKColor(0xFF, 0xFF, 0xFF, 0x55),

        SyntaxKeyword = new SKColor(0xFF, 0x7B, 0x72),
        SyntaxString = new SKColor(0xA5, 0xD6, 0xFF),
        SyntaxComment = new SKColor(0x8B, 0x94, 0x9E),
        SyntaxNumber = new SKColor(0x79, 0xC0, 0xFF),
        SyntaxType = new SKColor(0xFF, 0xA6, 0x57),
        SyntaxFunction = new SKColor(0xD2, 0xA8, 0xFF),
        SyntaxAttribute = new SKColor(0x7E, 0xE7, 0x87),
        SyntaxTag = new SKColor(0x7E, 0xE7, 0x87),

        AlertNote = new SKColor(0x4A, 0x93, 0xF8),
        AlertTip = new SKColor(0x3F, 0xB9, 0x50),
        AlertImportant = new SKColor(0xA3, 0x71, 0xF7),
        AlertWarning = new SKColor(0xD2, 0x99, 0x22),
        AlertCaution = new SKColor(0xF8, 0x51, 0x49),
    };

    /// <summary>Relative luminance heuristic, used by MarkdownViewer to auto-pick Light/Dark
    /// when tracking the host application's ColorScheme.</summary>
    public static bool IsLightColor(SKColor c)
    {
        double luminance = (0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue) / 255.0;
        return luminance > 0.5;
    }
}
