using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.Controls;

public class MarkdownViewer : ElementBase
{
    private const float HorizontalPadding = 16f;
    private const float VerticalPadding = 16f;
    private const float HeadingTopMargin = 18f;
    private const float HeadingBottomMargin = 4f;
    private const float BlockGap = 8f;
    private const float CodePaddingH = 12f;
    private const float CodePaddingV = 10f;
    private const float QuoteLeftBar = 3.2f;
    private const float QuotePadding = 10f;
    private const float ListIndent = 24f;
    private const float ParagraphSpacing = 4f;
    private const float HRMargin = 18f;

    private string _markdown = string.Empty;
    private SKFont? _bodyFont;
    private SKFont? _headingFont;
    private SKFont? _codeFont;
    private SKFont? _blockquoteFont;

    private bool _renderDirty = true;
    private float _contentHeight;
    private SKRect _contentBounds;
    private SKColor _codeBackColor = SKColors.Empty;
    private SKColor _codeTextColor = SKColors.Empty;
    private SKColor _quoteBarColor = SKColors.Empty;
    private SKColor _quoteTextColor = SKColors.Empty;
    private SKColor _hrColor = SKColors.Empty;
    private SKColor _headingColor = SKColors.Empty;
    private SKColor _bodyColor = SKColors.Empty;
    private SKColor _bulletColor = SKColors.Empty;

    private List<MarkdownBlock> _blocks = new();

    private readonly SKPaint _headingPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _bodyPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _codeBackgroundPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _codeTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _blockquoteBarPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _blockquoteTextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _hrPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _bulletPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    public MarkdownViewer()
    {
        AutoScroll = true;
        CanSelect = false;
        Cursor = Cursors.Default;
        Padding = new Thickness((int)HorizontalPadding, (int)VerticalPadding, (int)HorizontalPadding, (int)VerticalPadding);
        Width = 640;
        Height = 520;

        ColorScheme.ThemeChanged += OnColorSchemeChanged;
    }

    public virtual string Markdown
    {
        get => _markdown;
        set
        {
            if (!ReferenceEquals(_markdown, value) && _markdown != value)
            {
                _markdown = value ?? string.Empty;
                _renderDirty = true;
                Invalidate();
            }
        }
    }

    protected override bool HandlesMouseWheelScroll => AutoScroll;

    internal override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        _renderDirty = true;
    }

    public override void OnPaint(SKCanvas canvas)
    {
        if (Disposing || IsDisposed)
            return;

        base.OnPaint(canvas);
        EnsureFonts();
        EnsureColors();

        var availableWidth = ClientRectangle.Width - Padding.Left - Padding.Right;
        if (availableWidth < 50f)
            availableWidth = 50f;

        if (_renderDirty || _contentBounds.Width != availableWidth)
        {
            _blocks = ParseMarkdown(_markdown, availableWidth);
            float measureY = 0f;
            foreach (var block in _blocks)
            {
                block.Bounds = new SKRect(0, measureY, availableWidth, 0);
                measureY += block.Measure(this);
            }
            _contentBounds = new SKRect(0, 0, availableWidth, measureY);
            _contentHeight = measureY;
            _renderDirty = false;
        }

        var viewport = ClientRectangle;
        canvas.Save();
        canvas.ClipRect(viewport);

        var renderY = 0f;
        foreach (var block in _blocks)
        {
            if (block.Bounds.Bottom < 0 || block.Bounds.Top > viewport.Height)
            {
                renderY = block.Bounds.Bottom;
                continue;
            }

            block.Render(canvas, this);
            renderY = block.Bounds.Bottom;
        }

        canvas.Restore();
    }

    private void OnColorSchemeChanged(object? sender, EventArgs e)
    {
        EnsureFonts();
        EnsureColors();
        _renderDirty = true;
        Invalidate();
    }

    private void EnsureFonts()
    {
        _bodyFont ??= CreateDefaultFont(14f);
        _headingFont ??= CreateDefaultFont(18f, bold: true);
        _codeFont ??= CreateDefaultFont(13f);
        _blockquoteFont ??= CreateDefaultFont(14f, italic: true);
    }

    private void EnsureColors()
    {
        if (_codeBackColor == SKColors.Empty)
            _codeBackColor = ColorScheme.SurfaceVariant.WithAlpha(110);

        if (_codeTextColor == SKColors.Empty)
            _codeTextColor = ForeColor;

        if (_quoteBarColor == SKColors.Empty)
            _quoteBarColor = ColorScheme.Primary;

        if (_quoteTextColor == SKColors.Empty)
            _quoteTextColor = ForeColor;

        if (_hrColor == SKColors.Empty)
            _hrColor = ColorScheme.Outline.WithAlpha(140);

        if (_headingColor == SKColors.Empty)
            _headingColor = ForeColor;

        if (_bodyColor == SKColors.Empty)
            _bodyColor = ForeColor;

        if (_bulletColor == SKColors.Empty)
            _bulletColor = ForeColor;
    }

    private static bool IsFenceChar(char c) => c == '`' || c == '~';

    private List<MarkdownBlock> ParseMarkdown(string text, float availableWidth)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrWhiteSpace(text))
            return blocks;

        var lines = text.Split('\n');
        var block = new List<string>();
        var codeBuffer = new List<string>();
        var inCodeBlock = false;
        var fenceChar = '\0';
        var fenceLen = 0;
        var i = 0;

        var FlushParagraph = () =>
        {
            if (block.Count == 0)
                return;
            var joined = string.Join("\n", block);
            block.Clear();
            if (string.IsNullOrWhiteSpace(joined))
                return;
            blocks.Add(new ParagraphBlock { Text = joined });
        };

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (!inCodeBlock)
            {
                if (trimmed.Length >= 3 && trimmed[0] == trimmed[1] && trimmed[1] == trimmed[2] && IsFenceChar(trimmed[0]))
                {
                    inCodeBlock = true;
                    fenceChar = trimmed[0];
                    fenceLen = trimmed.Length - trimmed.TrimEnd().Length + (trimmed.Length > 3 ? 3 : trimmed.Length);
                    codeBuffer.Clear();
                    i++;
                    continue;
                }
            }
            else
            {
                if (trimmed.Length >= fenceLen && trimmed[0] == fenceChar)
                {
                    var trailingEnd = trimmed.Length;
                    while (trailingEnd > 0 && char.IsWhiteSpace(trimmed[trailingEnd - 1]))
                        trailingEnd--;
                    if (trailingEnd >= fenceLen)
                    {
                        inCodeBlock = false;
                        if (codeBuffer.Count > 0)
                        {
                            var code = string.Join("\n", codeBuffer);
                            codeBuffer.Clear();
                            blocks.Add(new CodeBlock { Text = code });
                        }
                    }
                }
                else
                {
                    codeBuffer.Add(trimmed);
                }
                i++;
                continue;
            }

            if (!inCodeBlock && trimmed.StartsWith(">"))
            {
                FlushParagraph();
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                {
                    var quoteLine = lines[i].TrimStart();
                    if (quoteLine.Length > 1)
                        quoteLines.Add(quoteLine.Substring(1).TrimStart());
                    i++;
                }
                blocks.Add(new BlockQuoteBlock { Lines = quoteLines });
                continue;
            }

            if (!inCodeBlock && (trimmed.StartsWith("---") || trimmed.StartsWith("***") || trimmed.StartsWith("___")))
            {
                FlushParagraph();
                if (block.Count == 0)
                    blocks.Add(new HorizontalRuleBlock());
                i++;
                continue;
            }

            if (!inCodeBlock && trimmed.StartsWith("#"))
            {
                FlushParagraph();
                var headingMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
                if (headingMatch.Success)
                {
                    var level = headingMatch.Groups[1].Value.Length;
                    var headingText = headingMatch.Groups[2].Value;
                    blocks.Add(new HeadingBlock { Level = level, Text = headingText });
                }
                i++;
                continue;
            }

            if (!inCodeBlock && (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\.\s")))
            {
                FlushParagraph();
                var list = new List<string>();
                while (i < lines.Length)
                {
                    var t = lines[i].TrimStart();
                    if (t.StartsWith("- ") || t.StartsWith("* ") || System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d+\.\s"))
                    {
                        list.Add(t.Substring(t.IndexOf(' ') + 1));
                        i++;
                    }
                    else
                        break;
                }
                blocks.Add(new ListBlock { Items = list });
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                i++;
                continue;
            }

            block.Add(trimmed);
            i++;
        }

        FlushParagraph();
        if (inCodeBlock && codeBuffer.Count > 0)
            blocks.Add(new CodeBlock { Text = string.Join("\n", codeBuffer) });

        return blocks;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnColorSchemeChanged;
            _bodyFont?.Dispose();
            _headingFont?.Dispose();
            _codeFont?.Dispose();
            _blockquoteFont?.Dispose();
            _bodyFont = null;
            _headingFont = null;
            _codeFont = null;
            _blockquoteFont = null;
        }

        base.Dispose(disposing);
    }

    private static List<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
        {
            if (!string.IsNullOrEmpty(text))
                lines.Add(text);
            return lines;
        }

        var paragraphs = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ');
            var current = string.Empty;
            foreach (var word in words)
            {
                if (word.Length == 0)
                    continue;

                var test = current.Length == 0 ? word : $"{current} {word}";
                if (font.MeasureText(test) > maxWidth && current.Length > 0)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = test;
                }
            }

            if (current.Length > 0)
                lines.Add(current);
        }

        return lines;
    }

    private SKFont CreateDefaultFont(float size, bool bold = false, bool italic = false)
    {
        SKTypeface typeface;
        if (bold && italic)
            typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.BoldItalic) ?? SKTypeface.Default;
        else if (bold)
            typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold) ?? SKTypeface.Default;
        else if (italic)
            typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Italic) ?? SKTypeface.Default;
        else
            typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Normal) ?? SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal) ?? SKTypeface.Default;

        return new SKFont(typeface, size);
    }

    private abstract class MarkdownBlock
    {
        public SKRect Bounds { get; set; }
        public abstract float Measure(MarkdownViewer owner);
        public abstract void Render(SKCanvas canvas, MarkdownViewer owner);
    }

    private class HeadingBlock : MarkdownBlock
    {
        public int Level { get; set; }
        public string Text { get; set; } = string.Empty;

        public override float Measure(MarkdownViewer owner)
        {
            var fontSize = Level switch
            {
                1 => 24f,
                2 => 20f,
                3 => 17f,
                _ => 15f,
            };

            var font = new SKFont(owner._headingFont!.Typeface, fontSize);
            var lines = WrapText(Text, font, Bounds.Width);
            var lineHeight = MathF.Ceiling(fontSize * 1.28f);
            return HeadingTopMargin + lines.Count * lineHeight + HeadingBottomMargin;
        }

        public override void Render(SKCanvas canvas, MarkdownViewer owner)
        {
            var fontSize = Level switch
            {
                1 => 24f,
                2 => 20f,
                3 => 17f,
                _ => 15f,
            };

            var font = new SKFont(owner._headingFont!.Typeface, fontSize);
            var lines = WrapText(Text, font, Bounds.Width);
            var lineHeight = MathF.Ceiling(fontSize * 1.28f);
            var y = Bounds.Top + HeadingTopMargin;

            foreach (var line in lines)
            {
                canvas.DrawText(line, HorizontalPadding, y + font.Metrics.Descent, font, owner._headingPaint);
                y += lineHeight;
            }
        }
    }

    private class ParagraphBlock : MarkdownBlock
    {
        public string Text { get; set; } = string.Empty;

        public override float Measure(MarkdownViewer owner)
        {
            var font = owner._bodyFont ?? owner.CreateDefaultFont(14f);
            var lines = WrapText(Text, font, Bounds.Width);
            var lineHeight = MathF.Ceiling(font.Size * 1.22f);
            return ParagraphSpacing + lines.Count * lineHeight + ParagraphSpacing;
        }

        public override void Render(SKCanvas canvas, MarkdownViewer owner)
        {
            var font = owner._bodyFont ?? owner.CreateDefaultFont(14f);
            var lines = WrapText(Text, font, Bounds.Width);
            var lineHeight = MathF.Ceiling(font.Size * 1.22f);
            var y = Bounds.Top + ParagraphSpacing;

            foreach (var line in lines)
            {
                canvas.DrawText(line, HorizontalPadding, y + font.Metrics.Descent, font, owner._bodyPaint);
                y += lineHeight;
            }
        }
    }

    private class CodeBlock : MarkdownBlock
    {
        public string Text { get; set; } = string.Empty;

        public override float Measure(MarkdownViewer owner)
        {
            var font = owner._codeFont ?? owner.CreateDefaultFont(13f);
            var lines = Text.Split('\n');
            var codeWidth = Bounds.Width - CodePaddingH * 2;
            if (codeWidth < 20f)
                codeWidth = 20f;

            var lineHeight = MathF.Ceiling(font.Size * 1.2f);
            var totalWrapped = 0;
            foreach (var line in lines)
                totalWrapped += WrapText(line, font, codeWidth).Count;

            return CodePaddingV + totalWrapped * lineHeight + CodePaddingV;
        }

        public override void Render(SKCanvas canvas, MarkdownViewer owner)
        {
            var font = owner._codeFont ?? owner.CreateDefaultFont(13f);
            var lines = Text.Split('\n');
            var codeWidth = Bounds.Width - CodePaddingH * 2;
            if (codeWidth < 20f)
                codeWidth = 20f;

            var lineHeight = MathF.Ceiling(font.Size * 1.2f);
            var rect = new SKRect(
                HorizontalPadding,
                Bounds.Top + CodePaddingV,
                HorizontalPadding + codeWidth + CodePaddingH * 2,
                Bounds.Top + CodePaddingV + lines.Length * lineHeight + CodePaddingV * 2);

            canvas.DrawRoundRect(rect, 6f, 6f, owner._codeBackgroundPaint);

            var textY = rect.Top + CodePaddingV;
            foreach (var line in lines)
            {
                var wrapped = WrapText(line, font, codeWidth);
                foreach (var wLine in wrapped)
                {
                    canvas.DrawText(wLine, HorizontalPadding + CodePaddingH, textY + font.Metrics.Descent, font, owner._codeTextPaint);
                    textY += lineHeight;
                }
            }
        }
    }

    private class BlockQuoteBlock : MarkdownBlock
    {
        public List<string> Lines { get; set; } = new();

        public override float Measure(MarkdownViewer owner)
        {
            var font = owner._blockquoteFont ?? owner.CreateDefaultFont(14f, italic: true);
            var innerWidth = Bounds.Width - QuoteLeftBar - QuotePadding * 2;
            if (innerWidth < 20f)
                innerWidth = 20f;

            var text = string.Join("\n", Lines);
            var lines = WrapText(text, font, innerWidth);
            var lineHeight = MathF.Ceiling(font.Size * 1.22f);
            return QuotePadding + lines.Count * lineHeight + QuotePadding;
        }

        public override void Render(SKCanvas canvas, MarkdownViewer owner)
        {
            var font = owner._blockquoteFont ?? owner.CreateDefaultFont(14f, italic: true);
            var innerWidth = Bounds.Width - QuoteLeftBar - QuotePadding * 2;
            if (innerWidth < 20f)
                innerWidth = 20f;

            var barX = HorizontalPadding;
            var barY = Bounds.Top + QuotePadding;
            var barHeight = Bounds.Height - QuotePadding * 2;
            var barRect = new SKRect(barX, barY, barX + QuoteLeftBar, barY + barHeight);
            canvas.DrawRoundRect(barRect, QuoteLeftBar / 2f, QuoteLeftBar / 2f, owner._blockquoteBarPaint);

            var text = string.Join("\n", Lines);
            var lines = WrapText(text, font, innerWidth);
            var lineHeight = MathF.Ceiling(font.Size * 1.22f);
            var y = Bounds.Top + QuotePadding;

            foreach (var line in lines)
            {
                canvas.DrawText(line, HorizontalPadding + QuoteLeftBar + QuotePadding, y + font.Metrics.Descent, font, owner._blockquoteTextPaint);
                y += lineHeight;
            }
        }
    }

    private class ListBlock : MarkdownBlock
    {
        public List<string> Items { get; set; } = new();

        public override float Measure(MarkdownViewer owner)
        {
            if (Items.Count == 0)
                return BlockGap;

            var font = owner._bodyFont ?? owner.CreateDefaultFont(14f);
            var itemWidth = Bounds.Width - ListIndent;
            if (itemWidth < 20f)
                itemWidth = 20f;

            var lineHeight = MathF.Ceiling(font.Size * 1.22f);
            var total = 0f;
            foreach (var item in Items)
            {
                var lines = WrapText(item, font, itemWidth);
                total += lines.Count * lineHeight;
            }
            return BlockGap + total + BlockGap;
        }

        public override void Render(SKCanvas canvas, MarkdownViewer owner)
        {
            if (Items.Count == 0)
                return;

            var font = owner._bodyFont ?? owner.CreateDefaultFont(14f);
            var itemWidth = Bounds.Width - ListIndent;
            if (itemWidth < 20f)
                itemWidth = 20f;

            var lineHeight = MathF.Ceiling(font.Size * 1.22f);
            var y = Bounds.Top + BlockGap;
            var index = 0;

            foreach (var item in Items)
            {
                index++;
                var bullet = index.ToString() + ".";
                canvas.DrawText(bullet, HorizontalPadding, y + font.Metrics.Descent, font, owner._bulletPaint);

                var lines = WrapText(item, font, itemWidth);
                var x = HorizontalPadding + ListIndent;
                foreach (var line in lines)
                {
                    canvas.DrawText(line, x, y + font.Metrics.Descent, font, owner._bodyPaint);
                    y += lineHeight;
                }
            }
        }
    }

    private class HorizontalRuleBlock : MarkdownBlock
    {
        public override float Measure(MarkdownViewer owner) => HRMargin * 2f + 1f;

        public override void Render(SKCanvas canvas, MarkdownViewer owner)
        {
            var y = Bounds.Top + HRMargin;
            canvas.DrawLine(HorizontalPadding, y, Bounds.Right - HorizontalPadding, y, owner._hrPaint);
        }
    }
}
