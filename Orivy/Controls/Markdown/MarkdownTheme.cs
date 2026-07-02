using SkiaSharp;

namespace Orivy.Controls.Markdown;

public sealed class MarkdownTheme
{
    public string[] BodyFontFamilies { get; set; } =
        { "Segoe UI Variable Text","Segoe UI","Inter","Helvetica Neue","Helvetica","Noto Sans","Arial" };
    public string[] MonospaceFontFamilies { get; set; } =
        { "Cascadia Mono","Cascadia Code","JetBrains Mono","Fira Code","Consolas",
          "SF Mono","Menlo","DejaVu Sans Mono","Liberation Mono","Courier New" };
    public bool UseHostDefaultFontForBody { get; set; } = true;

    // ── Type scale (logical px) ──
    public float BodyFontSize   { get; set; } = 16f;
    public float[] HeadingFontSizes { get; set; } = { 32f, 24f, 20f, 16f, 14f, 13f };
    public float CodeFontSize   { get; set; } = 13.5f;
    public float SmallFontSize  { get; set; } = 13f;
    public float BodyLineHeight { get; set; } = 1.6f;
    public float HeadingLineHeight { get; set; } = 1.3f;
    public float CodeLineHeight { get; set; } = 1.5f;

    // ── Spacing ──
    public float BlockSpacing           { get; set; } = 16f;
    public float TightBlockSpacing      { get; set; } = 4f;
    public float HeadingSpacingTop      { get; set; } = 24f;
    public float HeadingSpacingBottom   { get; set; } = 16f;
    public float ListIndent             { get; set; } = 28f;
    public float BlockquoteIndent       { get; set; } = 20f;
    public float BlockquoteBarWidth     { get; set; } = 4f;
    public float CodeBlockPaddingH      { get; set; } = 16f;
    public float CodeBlockPaddingV      { get; set; } = 14f;
    public float CodeBlockHeaderHeight  { get; set; } = 34f;
    public float TableCellPaddingH      { get; set; } = 14f;
    public float TableCellPaddingV      { get; set; } = 10f;
    public float ThematicBreakHeight    { get; set; } = 2f;
    public float CornerRadius           { get; set; } = 6f;
    public float CheckboxSize           { get; set; } = 16f;
    public float ImagePlaceholderHeight { get; set; } = 120f;
    public float MaxImageHeight         { get; set; } = 520f;

    // ── Palette ──
    public SKColor BodyColor                 { get; set; } = new(0x1F,0x23,0x28);
    public SKColor MutedColor                { get; set; } = new(0x59,0x63,0x6E);
    public SKColor HeadingColor              { get; set; } = new(0x1F,0x23,0x28);
    public SKColor HeadingBorderColor        { get; set; } = new(0xD1,0xD9,0xE0);
    public SKColor LinkColor                 { get; set; } = new(0x09,0x69,0xDA);
    public SKColor LinkHoverColor            { get; set; } = new(0x0A,0x50,0xA1);
    public SKColor BorderColor               { get; set; } = new(0xD1,0xD9,0xE0);
    public SKColor CodeBackground            { get; set; } = new(0xF6,0xF8,0xFA);
    public SKColor CodeBlockHeaderBackground { get; set; } = new(0xEC,0xF0,0xF3);
    public SKColor CodeInlineBackground      { get; set; } = new(0x81,0x8B,0x98,0x73);
    public SKColor CodeForeground            { get; set; } = new(0x1F,0x23,0x28);
    public SKColor BlockquoteBarColor        { get; set; } = new(0xD1,0xD9,0xE0);

    // Table
    public SKColor TableHeaderBackground     { get; set; } = new(0xF6,0xF8,0xFA);
    public SKColor TableRowAltBackground     { get; set; } = new(0xFB,0xFC,0xFD);
    public SKColor TableBorderColor          { get; set; } = new(0xD1,0xD9,0xE0);
    public SKColor TableHoverBackground      { get; set; } = new(0xF3,0xF6,0xF9);

    // Checkbox
    public SKColor CheckboxBorderColor { get; set; } = new(0x8C,0x95,0x9F);
    public SKColor CheckboxFillColor   { get; set; } = new(0x09,0x69,0xDA);
    public SKColor CheckmarkColor      { get; set; } = SKColors.White;

    // Inline decorations
    public SKColor InsertUnderlineColor { get; set; } = new(0x09,0x69,0xDA);
    public SKColor MarkBackground       { get; set; } = new(0xFF,0xF0,0x80,0xCC);
    public SKColor MarkColor            { get; set; } = new(0x4A,0x35,0x00);

    // Selection
    public SKColor SelectionBackground { get; set; } = new(0x09,0x69,0xDA,0x40);
    public SKColor ScrollIndicatorColor { get; set; } = new(0,0,0,0x55);

    // Container blocks
    public SKColor ContainerWarningBorder { get; set; } = new(0x9A,0x67,0x00);
    public SKColor ContainerWarningBg     { get; set; } = new(0xFF,0xF8,0xE6,0xCC);
    public SKColor ContainerDangerBorder  { get; set; } = new(0xCF,0x22,0x2E);
    public SKColor ContainerDangerBg      { get; set; } = new(0xFF,0xEB,0xEB,0xCC);
    public SKColor ContainerInfoBorder    { get; set; } = new(0x09,0x69,0xDA);
    public SKColor ContainerInfoBg        { get; set; } = new(0xE7,0xF3,0xFF,0xCC);
    public SKColor ContainerTipBorder     { get; set; } = new(0x1A,0x7F,0x37);
    public SKColor ContainerTipBg         { get; set; } = new(0xE6,0xF6,0xEC,0xCC);
    public SKColor ContainerDefaultBorder { get; set; } = new(0xD1,0xD9,0xE0);
    public SKColor ContainerDefaultBg     { get; set; } = new(0xF6,0xF8,0xFA,0xCC);

    // Syntax highlight
    public SKColor SyntaxKeyword   { get; set; } = new(0xCF,0x22,0x2E);
    public SKColor SyntaxString    { get; set; } = new(0x0A,0x30,0x69);
    public SKColor SyntaxComment   { get; set; } = new(0x6E,0x77,0x81);
    public SKColor SyntaxNumber    { get; set; } = new(0x00,0x5C,0xC5);
    public SKColor SyntaxType      { get; set; } = new(0x95,0x03,0x00);
    public SKColor SyntaxFunction  { get; set; } = new(0x6F,0x42,0xC1);
    public SKColor SyntaxAttribute { get; set; } = new(0x11,0x67,0x29);
    public SKColor SyntaxTag       { get; set; } = new(0x11,0x67,0x29);

    // Alerts
    public SKColor AlertNote      { get; set; } = new(0x09,0x69,0xDA);
    public SKColor AlertTip       { get; set; } = new(0x1A,0x7F,0x37);
    public SKColor AlertImportant { get; set; } = new(0x82,0x50,0xDF);
    public SKColor AlertWarning   { get; set; } = new(0x9A,0x67,0x00);
    public SKColor AlertCaution   { get; set; } = new(0xCF,0x22,0x2E);

    public static MarkdownTheme Light() => new();

    public static MarkdownTheme Dark() => new()
    {
        BodyColor = new(0xE6,0xED,0xF3), MutedColor = new(0x9B,0xA7,0xB3),
        HeadingColor = new(0xE6,0xED,0xF3), HeadingBorderColor = new(0x30,0x36,0x3D),
        LinkColor = new(0x4A,0x93,0xF8), LinkHoverColor = new(0x7A,0xB7,0xFF),
        BorderColor = new(0x30,0x36,0x3D),
        CodeBackground = new(0x16,0x1B,0x22), CodeBlockHeaderBackground = new(0x1C,0x21,0x28),
        CodeInlineBackground = new(0x6E,0x76,0x81,0x80), CodeForeground = new(0xE6,0xED,0xF3),
        BlockquoteBarColor = new(0x30,0x36,0x3D),
        TableHeaderBackground = new(0x16,0x1B,0x22), TableRowAltBackground = new(0x13,0x18,0x1E),
        TableBorderColor = new(0x30,0x36,0x3D), TableHoverBackground = new(0x21,0x26,0x2E),
        CheckboxBorderColor = new(0x6E,0x76,0x81), CheckboxFillColor = new(0x4A,0x93,0xF8),
        CheckmarkColor = new(0x0D,0x11,0x17),
        InsertUnderlineColor = new(0x4A,0x93,0xF8),
        MarkBackground = new(0x6E,0x54,0x00,0xCC), MarkColor = new(0xFF,0xE8,0x80),
        SelectionBackground = new(0x4A,0x93,0xF8,0x40),
        ScrollIndicatorColor = new(0xFF,0xFF,0xFF,0x55),
        ContainerWarningBorder = new(0xD2,0x99,0x22), ContainerWarningBg = new(0x3D,0x2E,0x00,0xCC),
        ContainerDangerBorder  = new(0xF8,0x51,0x49), ContainerDangerBg  = new(0x3D,0x10,0x10,0xCC),
        ContainerInfoBorder    = new(0x4A,0x93,0xF8), ContainerInfoBg    = new(0x0C,0x2A,0x4A,0xCC),
        ContainerTipBorder     = new(0x3F,0xB9,0x50), ContainerTipBg     = new(0x0C,0x2D,0x16,0xCC),
        ContainerDefaultBorder = new(0x30,0x36,0x3D), ContainerDefaultBg = new(0x16,0x1B,0x22,0xCC),
        SyntaxKeyword = new(0xFF,0x7B,0x72), SyntaxString = new(0xA5,0xD6,0xFF),
        SyntaxComment = new(0x8B,0x94,0x9E), SyntaxNumber = new(0x79,0xC0,0xFF),
        SyntaxType = new(0xFF,0xA6,0x57), SyntaxFunction = new(0xD2,0xA8,0xFF),
        SyntaxAttribute = new(0x7E,0xE7,0x87), SyntaxTag = new(0x7E,0xE7,0x87),
        AlertNote = new(0x4A,0x93,0xF8), AlertTip = new(0x3F,0xB9,0x50),
        AlertImportant = new(0xA3,0x71,0xF7), AlertWarning = new(0xD2,0x99,0x22),
        AlertCaution = new(0xF8,0x51,0x49),
    };

    public static bool IsLightColor(SKColor c) =>
        (0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue) / 255.0 > 0.5;
}
