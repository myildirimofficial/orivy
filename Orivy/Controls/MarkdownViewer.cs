using Orivy;
using Orivy.Controls;
using Orivy.Layout;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orivy.Controls;

public class MarkdownViewer : ElementBase
{
    #region Fields & State
    private string _rawMarkdown = string.Empty;
    private List<MdBlock> _blocks = new();
    private float _totalHeight = 0f;
    private float _maxContentWidth = 0f;

    private MarkdownStyleConfig _config = MarkdownStyleConfig.Default;
    private Dictionary<string, List<CompiledSyntaxRule>> _compiledLanguages = new();

    private SKFont _fontBody, _fontBold, _fontItalic, _fontCode, _fontStrike;
    private SKFont[] _headingFonts = new SKFont[6];
    
    private SKTypeface _emojiTypeface;
    private readonly SKPaint _emojiPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _bgPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _borderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _measurePaint = new() { IsAntialias = true };
    private readonly SKPaint _selectionPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private SelectionPoint _selStart = new(-1, -1, -1);
    private SelectionPoint _selEnd = new(-1, -1, -1);
    private bool _isSelecting;
    private bool _showCaret = true;
    private readonly System.Timers.Timer _caretBlinkTimer = new(500) { AutoReset = true };
    #endregion

    #region Properties
    public string Text
    {
        get => _rawMarkdown;
        set
        {
            if (_rawMarkdown == value) return;
            _rawMarkdown = value;
            RebuildDocument();
            Invalidate();
        }
    }

    public string StyleJsonPath
    {
        set
        {
            if (File.Exists(value))
            {
                var json = File.ReadAllText(value);
                LoadStyleConfig(json);
            }
        }
    }
    
    private bool HasSelection => _selStart.IsValid && _selEnd.IsValid && 
        (_selStart.BlockIndex != _selEnd.BlockIndex || _selStart.LineIndex != _selEnd.LineIndex || _selStart.CharIndex != _selEnd.CharIndex);
    #endregion

    #region Lifecycle & Theme
    public MarkdownViewer()
    {
        AutoScroll = true;
        Padding = new Thickness(24, 24, 24, 24);
        ColorScheme.ThemeChanged += (_, _) => Invalidate();
        CanSelect = true;
        Cursor = Cursors.IBeam;
        
        _caretBlinkTimer.Elapsed += (s, e) => { 
            if (Focused && !HasSelection) { _showCaret = !_showCaret; Invalidate(); } 
        };
    }

    internal override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (!string.IsNullOrEmpty(_rawMarkdown)) RebuildDocument();
    }

    internal override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _caretBlinkTimer.Start();
        _showCaret = true;
        Invalidate();
    }

    internal override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _caretBlinkTimer.Stop();
        Invalidate();
    }

    protected override void InvalidateFontCache()
    {
        base.InvalidateFontCache();
        _fontBody?.Dispose(); _fontBold?.Dispose(); _fontItalic?.Dispose(); 
        _fontCode?.Dispose(); _fontStrike?.Dispose();
        _emojiTypeface?.Dispose();
        for (int i = 0; i < _headingFonts.Length; i++) _headingFonts[i]?.Dispose();

        float s = ScaleFactor;
        _fontBody = CreateFont(14 * s);
        _fontBold = CreateFont(14 * s, true);
        _fontItalic = CreateFont(14 * s, italic: true);
        _fontStrike = CreateFont(14 * s, italic: true);
        _fontCode = CreateFont(13 * s, family: "Consolas");
        
        string emojiFamily = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Segoe UI Emoji" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Apple Color Emoji" : "Noto Color Emoji";
        _emojiTypeface = SKTypeface.FromFamilyName(emojiFamily) ?? SKTypeface.Default;

        float[] hSizes = { 28, 22, 18, 16, 14, 13 };
        if (_config != null) {
            hSizes[0] = _config.BaseStyles.H1Size; hSizes[1] = _config.BaseStyles.H2Size;
            hSizes[2] = _config.BaseStyles.H3Size; hSizes[3] = _config.BaseStyles.H4Size;
        }
        for (int i = 0; i < 6; i++) _headingFonts[i] = CreateFont(hSizes[i] * s, true);
    }

    private SKFont CreateFont(float size, bool bold = false, bool italic = false, string family = null)
    {
        var style = bold && italic ? SKFontStyle.BoldItalic : (bold ? SKFontStyle.Bold : (italic ? SKFontStyle.Italic : SKFontStyle.Normal));
        var typeface = family != null ? SKTypeface.FromFamilyName(family, style) : SKTypeface.FromFamilyName(null, style);
        return new SKFont(typeface, size) { Subpixel = true, Edging = SKFontEdging.SubpixelAntialias };
    }

    private float GetLineHeight(SKFont font)
    {
        var metrics = font.Metrics;
        return MathF.Ceiling(metrics.Descent - metrics.Ascent + metrics.Leading);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _caretBlinkTimer.Stop(); _caretBlinkTimer.Dispose();
            _fontBody?.Dispose(); _fontBold?.Dispose(); _fontItalic?.Dispose(); _fontCode?.Dispose(); _fontStrike?.Dispose();
            _emojiTypeface?.Dispose();
            for (int i = 0; i < _headingFonts.Length; i++) _headingFonts[i]?.Dispose();
            _textPaint.Dispose(); _bgPaint.Dispose(); _borderPaint.Dispose(); _measurePaint.Dispose(); _selectionPaint.Dispose(); _emojiPaint.Dispose();
        }
        base.Dispose(disposing);
    }
    #endregion

    #region JSON Config & Syntax Highlighter
    private void LoadStyleConfig(string json)
    {
        try
        {
            _config = JsonSerializer.Deserialize<MarkdownStyleConfig>(json) ?? MarkdownStyleConfig.Default;
            CompileSyntaxRules();
            InvalidateFontCache();
            if (!string.IsNullOrEmpty(_rawMarkdown)) RebuildDocument();
        }
        catch { _config = MarkdownStyleConfig.Default; }
    }

    private void CompileSyntaxRules()
    {
        _compiledLanguages.Clear();
        if (_config?.Languages == null) return;
        foreach (var lang in _config.Languages)
        {
            var rules = new List<CompiledSyntaxRule>();
            foreach (var rule in lang.Value)
            {
                try { rules.Add(new CompiledSyntaxRule { Regex = new Regex(rule.Pattern, RegexOptions.Compiled), Scope = rule.Scope }); }
                catch { }
            }
            _compiledLanguages[lang.Key.ToLower()] = rules;
        }
    }

    private SKColor GetScopeColor(string scope)
    {
        bool isDark = ColorScheme.IsDarkMode;
        if (_config?.Scopes != null && _config.Scopes.TryGetValue(scope, out var colors))
        {
            string hex = isDark ? colors.Dark : colors.Light;
            if (SKColor.TryParse(hex, out var col)) return col;
        }
        return ColorScheme.ForeColor;
    }
    #endregion

    #region Block Parsing & Layout Engine
    private void RebuildDocument()
    {
        if (string.IsNullOrWhiteSpace(_rawMarkdown))
        {
            _blocks.Clear(); _totalHeight = 0; _maxContentWidth = 0;
            AutoScrollMinSize = SKSize.Empty;
            return;
        }

        _blocks.Clear();
        float contentWidth = Width - Padding.Left - Padding.Right;
        if (contentWidth <= 0) contentWidth = 100;
        _maxContentWidth = contentWidth;

        var lines = _rawMarkdown.Replace("\r\n", "\n").Split('\n');
        var currentParagraph = new StringBuilder();
        bool inCodeBlock = false;
        var codeContent = new StringBuilder();
        string codeLang = "";
        var tableRows = new List<string>();
        
        int orderedListCounter = 0;
        bool inList = false;
        bool lastWasOrdered = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line.TrimStart().StartsWith("```"))
            {
                FlushParagraph(currentParagraph, contentWidth);
                FlushTable(tableRows, contentWidth);
                if (!inCodeBlock) { inCodeBlock = true; codeLang = line.TrimStart()[3..].Trim(); codeContent.Clear(); }
                else { _blocks.Add(new MdBlock { Type = BlockType.Code, RawText = codeContent.ToString().TrimEnd(), Lang = codeLang }); inCodeBlock = false; }
                inList = false; lastWasOrdered = false;
                continue;
            }
            if (inCodeBlock) { codeContent.AppendLine(line); continue; }

            if (line.Trim().StartsWith("|") && line.Trim().EndsWith("|"))
            {
                FlushParagraph(currentParagraph, contentWidth);
                tableRows.Add(line);
                inList = false; lastWasOrdered = false;
                continue;
            }
            else if (tableRows.Count > 0) { FlushTable(tableRows, contentWidth); }

            if (line.Trim().Length >= 3 && line.Trim().Replace("-", "").Replace("*", "").Replace("_", "") == "")
            { FlushParagraph(currentParagraph, contentWidth); _blocks.Add(new MdBlock { Type = BlockType.Hr }); inList = false; lastWasOrdered = false; continue; }

            if (line.StartsWith("#"))
            {
                FlushParagraph(currentParagraph, contentWidth);
                int level = 0;
                while (level < line.Length && line[level] == '#') level++;
                if (level <= 6 && level < line.Length && line[level] == ' ')
                    _blocks.Add(new MdBlock { Type = BlockType.Heading, Level = level, Text = line[(level + 1)..].Trim() });
                inList = false; lastWasOrdered = false;
                continue;
            }

            if (line.StartsWith("> "))
            { FlushParagraph(currentParagraph, contentWidth); _blocks.Add(new MdBlock { Type = BlockType.Quote, Text = line[2..].Trim() }); inList = false; lastWasOrdered = false; continue; }

            if (Regex.IsMatch(line, @"^\s*[-*+]\s") || Regex.IsMatch(line, @"^\s*\d+\.\s"))
            {
                FlushParagraph(currentParagraph, contentWidth);
                bool isOrdered = Regex.IsMatch(line, @"^\s*\d+\.\s");
                string text = isOrdered ? Regex.Replace(line, @"^\s*\d+\.\s", "") : line.TrimStart()[2..];
                
                if (!inList) { orderedListCounter = 0; inList = true; }
                if (isOrdered) { if (!lastWasOrdered) orderedListCounter = 0; orderedListCounter++; lastWasOrdered = true; }
                else { lastWasOrdered = false; }

                _blocks.Add(new MdBlock { Type = BlockType.ListItem, Text = text.Trim(), IsOrdered = isOrdered, ListIndex = orderedListCounter });
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) { FlushParagraph(currentParagraph, contentWidth); inList = false; lastWasOrdered = false; continue; }

            if (currentParagraph.Length > 0) currentParagraph.Append(' ');
            currentParagraph.Append(line);
        }
        
        FlushParagraph(currentParagraph, contentWidth);
        FlushTable(tableRows, contentWidth);
        if (inCodeBlock) _blocks.Add(new MdBlock { Type = BlockType.Code, RawText = codeContent.ToString() });

        float currentY = 0;
        float s = ScaleFactor;

        foreach (var block in _blocks)
        {
            block.Y = currentY;
            block.Inlines = ParseInlinesFast(block.Text ?? "");

            if (block.Type == BlockType.Code)
            {
                var codeLines = block.RawText.Split('\n');
                block.WrappedCodeLines = new List<List<SyntaxToken>>();
                float maxW = 0;
                float codeLineH = GetLineHeight(_fontCode);
                _measurePaint.Typeface = _fontCode.Typeface; _measurePaint.TextSize = _fontCode.Size;
                
                foreach (var cl in codeLines)
                {
                    var lineTokens = new List<SyntaxToken> { new SyntaxToken { Text = cl, Scope = "default" } };
                    block.WrappedCodeLines.Add(lineTokens);
                    float w = _measurePaint.MeasureText(cl);
                    if (w > maxW) maxW = w;
                }
                block.Height = (codeLines.Length * codeLineH) + 24 * s;
                float totalCodeWidth = maxW + 32 * s;
                if (totalCodeWidth > _maxContentWidth) _maxContentWidth = totalCodeWidth;
            }
            else if (block.Type == BlockType.Table)
            {
                MeasureTable(block, contentWidth);
            }
            else
            {
                SKFont font = block.Type == BlockType.Heading ? _headingFonts[block.Level - 1] : _fontBody;
                float maxWidth = block.Type == BlockType.Quote ? contentWidth - 20 * s : contentWidth;
                block.WrappedLines = WrapInlinesFast(block.Inlines, font, maxWidth);
                block.Height = block.WrappedLines.Count * GetLineHeight(font);
            }

            block.MarginBottom = block.Type switch
            {
                BlockType.Heading when block.Level <= 2 => 16 * s,
                BlockType.Heading => 12 * s,
                BlockType.Paragraph => 12 * s,
                BlockType.Quote => 12 * s,
                BlockType.ListItem => 6 * s,
                _ => 12 * s
            };

            currentY += block.Height + block.MarginBottom;
        }

        _totalHeight = currentY;
        AutoScrollMinSize = new SKSize(_maxContentWidth, _totalHeight + Padding.Bottom);
        UpdateScrollBars(); 
    }

    private void FlushParagraph(StringBuilder sb, float width)
    {
        if (sb.Length > 0) { _blocks.Add(new MdBlock { Type = BlockType.Paragraph, Text = sb.ToString() }); sb.Clear(); }
    }

    private void FlushTable(List<string> rows, float width)
    {
        if (rows.Count < 2) { rows.Clear(); return; }
        _blocks.Add(new MdBlock { Type = BlockType.Table, TableRows = new(rows) });
        rows.Clear();
    }
    #endregion

    #region Inline Parser with Emoji Support
    private List<InlineRun> ParseInlinesFast(string text)
    {
        var runs = new List<InlineRun>();
        if (string.IsNullOrEmpty(text)) return runs;

        var sb = new StringBuilder();
        InlineType currentType = InlineType.Normal;

        void Flush()
        {
            if (sb.Length > 0)
            {
                runs.Add(new InlineRun { Text = sb.ToString(), Type = currentType });
                sb.Clear();
            }
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (char.IsHighSurrogate(c) && char.IsLowSurrogate(next))
            {
                Flush();
                runs.Add(new InlineRun { Text = new string(new[] { c, next }), Type = InlineType.Emoji });
                i++;
                continue;
            }
            if ((c >= 0x2600 && c <= 0x27BF) || (c >= 0x1F300 && c <= 0x1F9FF) || (c >= 0x2702 && c <= 0x27B0))
            {
                Flush();
                runs.Add(new InlineRun { Text = c.ToString(), Type = InlineType.Emoji });
                continue;
            }

            if (c == '`' && currentType != InlineType.Bold && currentType != InlineType.Italic)
            {
                Flush();
                currentType = currentType == InlineType.Code ? InlineType.Normal : InlineType.Code;
                continue;
            }

            if ((c == '*' || c == '_') && currentType != InlineType.Code)
            {
                if (c == '*' && next == '*')
                {
                    Flush();
                    currentType = currentType == InlineType.Bold ? InlineType.Normal : InlineType.Bold;
                    i++;
                    continue;
                }
                else if (c == '*' || c == '_')
                {
                    Flush();
                    currentType = currentType == InlineType.Italic ? InlineType.Normal : InlineType.Italic;
                    continue;
                }
            }

            if (c == '~' && next == '~' && currentType != InlineType.Code)
            {
                Flush();
                currentType = currentType == InlineType.Strike ? InlineType.Normal : InlineType.Strike;
                i++;
                continue;
            }

            sb.Append(c);
        }
        Flush();

        return runs.Count == 0 ? new List<InlineRun> { new InlineRun { Text = text, Type = InlineType.Normal } } : runs;
    }

    private List<List<InlineRun>> WrapInlinesFast(List<InlineRun> inlines, SKFont defaultFont, float maxWidth)
    {
        var wrappedLines = new List<List<InlineRun>>();
        var currentLine = new List<InlineRun>();
        float currentWidth = 0;

        _measurePaint.Typeface = defaultFont.Typeface;
        _measurePaint.TextSize = defaultFont.Size;

        foreach (var inline in inlines)
        {
            if (inline.Type == InlineType.Code || inline.Type == InlineType.Emoji)
            {
                float w = inline.Type == InlineType.Emoji ? MeasureEmoji(inline.Text, defaultFont.Size) : _measurePaint.MeasureText(inline.Text);
                if (currentWidth + w > maxWidth && currentLine.Count > 0)
                {
                    wrappedLines.Add(currentLine);
                    currentLine = new List<InlineRun>();
                    currentWidth = 0;
                }
                currentLine.Add(new InlineRun { Text = inline.Text, Type = inline.Type });
                currentWidth += w;
                continue;
            }

            var words = inline.Text.Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                string word = w == 0 ? words[w] : " " + words[w];
                float wordWidth = _measurePaint.MeasureText(word);

                if (currentWidth + wordWidth > maxWidth && currentLine.Count > 0)
                {
                    wrappedLines.Add(currentLine);
                    currentLine = new List<InlineRun>();
                    currentWidth = 0;
                    word = words[w].TrimStart();
                    wordWidth = _measurePaint.MeasureText(word);
                }
                currentLine.Add(new InlineRun { Text = word, Type = inline.Type });
                currentWidth += wordWidth;
            }
        }
        if (currentLine.Count > 0) wrappedLines.Add(currentLine);
        return wrappedLines.Count == 0 ? new List<List<InlineRun>> { new() } : wrappedLines;
    }

    private float MeasureEmoji(string text, float fontSize)
    {
        _emojiPaint.Typeface = _emojiTypeface;
        _emojiPaint.TextSize = fontSize;
        return _emojiPaint.MeasureText(text);
    }

    private SKFont GetFontForRun(InlineRun run, SKFont defaultFont) => run.Type switch
    {
        InlineType.Code => _fontCode,
        InlineType.Bold => _fontBold,
        InlineType.Italic => _fontItalic,
        InlineType.Strike => _fontStrike,
        _ => defaultFont
    };
    #endregion

    #region Table Measurement
    private void MeasureTable(MdBlock block, float maxWidth)
    {
        var rows = block.TableRows;
        block.TableData = new List<List<string>>();
        foreach(var row in rows) {
            if(row.Replace("|", "").Trim() == "") continue; 
            var cells = row.Split('|').Skip(1).Take(row.Count(c => c == '|') - 1).Select(c => c.Trim()).ToList();
            block.TableData.Add(cells);
        }
        if (block.TableData.Count == 0) { block.Height = 0; return; }

        int colCount = block.TableData.Max(r => r.Count);
        float cellPadding = 16 * ScaleFactor;
        float colWidth = maxWidth / colCount;
        block.TableColWidth = colWidth;

        block.TableRowHeights = new List<float>();
        block.TableWrappedCells = new List<List<List<string>>>();
        float totalHeight = 0;

        for (int r = 0; r < block.TableData.Count; r++)
        {
            float maxCellHeight = 0;
            var wrappedRow = new List<List<string>>();
            bool isHeader = (r == 0);
            SKFont cellFont = isHeader ? _fontBold : _fontBody;
            float lineHeight = GetLineHeight(cellFont);
            
            var row = block.TableData[r];
            while (row.Count < colCount) row.Add("");

            _measurePaint.Typeface = cellFont.Typeface; _measurePaint.TextSize = cellFont.Size;

            foreach (var cell in row)
            {
                var lines = WrapTextFast(cell, cellFont, colWidth - (cellPadding * 2));
                float cellHeight = (lines.Count * lineHeight) + (cellPadding * 2); 
                if (cellHeight > maxCellHeight) maxCellHeight = cellHeight;
                wrappedRow.Add(lines);
            }
            
            block.TableWrappedCells.Add(wrappedRow);
            block.TableRowHeights.Add(maxCellHeight);
            totalHeight += maxCellHeight;
        }
        block.Height = totalHeight;
    }

    private List<string> WrapTextFast(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = new StringBuilder();
        
        _measurePaint.Typeface = font.Typeface; _measurePaint.TextSize = font.Size;

        foreach (var word in words)
        {
            string testLine = currentLine.Length == 0 ? word : $"{currentLine} {word}";
            if (_measurePaint.MeasureText(testLine) > maxWidth && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
            else
            {
                if (currentLine.Length > 0) currentLine.Append(" ");
                currentLine.Append(word);
            }
        }
        if (currentLine.Length > 0) lines.Add(currentLine.ToString());
        return lines.Count == 0 ? new List<string> { "" } : lines;
    }
    #endregion

    #region Text Selection & Caret
    private struct SelectionPoint
    { 
        public int BlockIndex; public int LineIndex; public int CharIndex; 
        public SelectionPoint(int b, int l, int c) { BlockIndex=b; LineIndex=l; CharIndex=c; }
        public bool IsValid => BlockIndex >= 0;
    }

    private SelectionPoint HitTest(float mouseX, float mouseY)
    {
        float scrollY = _vScrollBar?.DisplayValue ?? 0;
        float scrollX = _hScrollBar?.DisplayValue ?? 0;

        for (int b = 0; b < _blocks.Count; b++)
        {
            var block = _blocks[b];
            if (block.Type == BlockType.Code || block.Type == BlockType.Table || block.Type == BlockType.Hr) continue;
            if (block.WrappedLines == null) continue;

            float drawY = block.Y - scrollY + Padding.Top;
            float drawX = Padding.Left - scrollX;
            SKFont defaultFont = block.Type == BlockType.Heading ? _headingFonts[block.Level - 1] : _fontBody;
            float lineHeight = GetLineHeight(defaultFont);

            for (int l = 0; l < block.WrappedLines.Count; l++)
            {
                float lineTop = drawY + l * lineHeight;
                if (mouseY >= lineTop && mouseY < lineTop + lineHeight)
                {
                    float currentX = drawX;
                    var line = block.WrappedLines[l];
                    
                    float totalWidth = 0;
                    foreach (var run in line)
                    {
                        totalWidth += run.Type == InlineType.Emoji ? MeasureEmoji(run.Text, defaultFont.Size) : _measurePaint.MeasureText(run.Text);
                    }

                    if (mouseX < drawX) return new SelectionPoint(b, l, 0);
                    if (mouseX > drawX + totalWidth) return new SelectionPoint(b, l, GetLineCharCount(line));

                    currentX = drawX;
                    int charCount = 0;
                    for (int c = 0; c < line.Count; c++)
                    {
                        var run = line[c];
                        float runW = run.Type == InlineType.Emoji ? MeasureEmoji(run.Text, defaultFont.Size) : _measurePaint.MeasureText(run.Text);
                        
                        if (mouseX < currentX + runW) 
                        {
                            float charW = runW / Math.Max(1, run.Text.Length);
                            int localChar = (int)((mouseX - currentX) / charW);
                            return new SelectionPoint(b, l, charCount + localChar);
                        }
                        currentX += runW;
                        charCount += run.Text.Length;
                    }
                    return new SelectionPoint(b, l, charCount);
                }
            }
        }
        return new SelectionPoint(-1, -1, -1);
    }

    private int GetLineCharCount(List<InlineRun> line)
    {
        int count = 0;
        foreach (var run in line) count += run.Text.Length;
        return count;
    }

    private float GetRunXOffset(List<InlineRun> line, int targetCharIndex, float startX, SKFont defaultFont)
    {
        float currentX = startX;
        int charCount = 0;
        for (int i = 0; i < line.Count; i++)
        {
            var run = line[i];
            float runWidth = run.Type == InlineType.Emoji ? MeasureEmoji(run.Text, defaultFont.Size) : _measurePaint.MeasureText(run.Text);
            float charWidth = runWidth / Math.Max(1, run.Text.Length);
            
            if (charCount + run.Text.Length <= targetCharIndex)
            {
                currentX += runWidth;
                charCount += run.Text.Length;
            }
            else
            {
                int remaining = targetCharIndex - charCount;
                currentX += charWidth * remaining;
                break;
            }
        }
        return currentX;
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            _selStart = HitTest(e.X, e.Y);
            _selEnd = _selStart;
            _isSelecting = true;
            _showCaret = true;
            Invalidate();
        }
    }

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isSelecting && e.Button == MouseButtons.Left)
        {
            _selEnd = HitTest(e.X, e.Y);
            Invalidate();
        }
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isSelecting = false;
    }

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.C && HasSelection)
        {
            string selectedText = GetSelectedText();
            if (!string.IsNullOrEmpty(selectedText))
            {
                try { Helpers.ClipboardHelper.TrySetText(selectedText); } catch { }
            }
        }
    }

    private string GetSelectedText()
    {
        if (!HasSelection) return "";
        var (minP, maxP) = (_selStart.BlockIndex < _selEnd.BlockIndex || (_selStart.BlockIndex == _selEnd.BlockIndex && _selStart.LineIndex <= _selEnd.LineIndex)) 
            ? (_selStart, _selEnd) : (_selEnd, _selStart);
            
        var sb = new StringBuilder();
        for (int b = minP.BlockIndex; b <= maxP.BlockIndex; b++)
        {
            var block = _blocks[b];
            if (block.WrappedLines == null) continue;
            
            int startL = (b == minP.BlockIndex) ? minP.LineIndex : 0;
            int endL = (b == maxP.BlockIndex) ? maxP.LineIndex : block.WrappedLines.Count - 1;
            
            for (int l = startL; l <= endL; l++)
            {
                var line = block.WrappedLines[l];
                int startC = (b == minP.BlockIndex && l == minP.LineIndex) ? minP.CharIndex : 0;
                int endC = (b == maxP.BlockIndex && l == maxP.LineIndex) ? maxP.CharIndex : int.MaxValue;
                
                int charCount = 0;
                foreach(var run in line)
                {
                    int runStart = charCount;
                    int runEnd = charCount + run.Text.Length;
                    
                    if (runEnd <= startC || runStart >= endC)
                    {
                        charCount += run.Text.Length;
                        continue;
                    }
                    
                    int localStart = Math.Max(0, startC - runStart);
                    int localEnd = Math.Min(run.Text.Length, endC - runStart);
                    sb.Append(run.Text.Substring(localStart, localEnd - localStart));
                    charCount += run.Text.Length;
                }
                if (l < endL) sb.AppendLine();
            }
            if (b < maxP.BlockIndex) sb.AppendLine();
        }
        return sb.ToString();
    }
    #endregion

    #region Rendering
    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        if (_blocks.Count == 0) return;

        float scrollY = _vScrollBar?.DisplayValue ?? 0;
        float scrollX = _hScrollBar?.DisplayValue ?? 0;
        float viewportHeight = Height;
        float s = ScaleFactor;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, Width, Height));

        for (int i = 0; i < _blocks.Count; i++)
        {
            var block = _blocks[i];
            if (block.Y + block.Height + block.MarginBottom < scrollY) continue;
            if (block.Y > scrollY + viewportHeight) break;

            DrawBlock(canvas, block, scrollX, scrollY, s, i);
        }
        
        canvas.Restore();
    }

    private void DrawBlock(SKCanvas canvas, MdBlock block, float scrollX, float scrollY, float s, int blockIndex)
    {
        float drawY = block.Y - scrollY + Padding.Top;
        float drawX = Padding.Left - scrollX;
        float contentWidth = Width - Padding.Left - Padding.Right;

        switch (block.Type)
        {
            case BlockType.Heading:
                _textPaint.Color = ColorScheme.ForeColor;
                DrawWrappedInlines(canvas, block.WrappedLines, _headingFonts[block.Level - 1], drawX, drawY, blockIndex);
                if (block.Level <= 2) {
                    _borderPaint.Color = ColorScheme.BorderColor; _borderPaint.StrokeWidth = 1 * s;
                    canvas.DrawLine(Padding.Left, drawY + block.Height + 8 * s, Width - Padding.Right, drawY + block.Height + 8 * s, _borderPaint);
                }
                break;

            case BlockType.Code:
                bool isDark = ColorScheme.IsDarkMode;
                SKColor.TryParse(isDark ? (_config?.BaseStyles.CodeBackgroundDark ?? "#161B22") : (_config?.BaseStyles.CodeBackgroundLight ?? "#F6F8FA"), out var bgColor);
                _bgPaint.Color = bgColor;
                float codeBlockWidth = Math.Max(contentWidth, _maxContentWidth - Padding.Left - Padding.Right);
                var codeRect = new SKRect(Padding.Left - scrollX, drawY, Padding.Left + codeBlockWidth, drawY + block.Height);
                canvas.DrawRoundRect(codeRect, 8 * s, 8 * s, _bgPaint);
                _borderPaint.Color = ColorScheme.BorderColor.WithAlpha(40); _borderPaint.StrokeWidth = 1 * s;
                canvas.DrawRoundRect(codeRect, 8 * s, 8 * s, _borderPaint);

                float codeY = drawY + 12 * s;
                float codeX = Padding.Left + 16 * s - scrollX;
                float codeLineH = GetLineHeight(_fontCode);
                foreach (var lineTokens in block.WrappedCodeLines) {
                    float currentX = codeX;
                    foreach (var token in lineTokens) {
                        _textPaint.Color = GetScopeColor(token.Scope); _textPaint.Typeface = _fontCode.Typeface; _textPaint.TextSize = _fontCode.Size;
                        canvas.DrawText(token.Text, currentX, codeY - _fontCode.Metrics.Ascent, _textPaint);
                        currentX += _textPaint.MeasureText(token.Text);
                    }
                    codeY += codeLineH;
                }
                break;

            case BlockType.Table:
                DrawTable(canvas, block, drawX, drawY, s);
                break;

            case BlockType.Quote:
                _bgPaint.Color = ColorScheme.SurfaceContainer.WithAlpha(80);
                canvas.DrawRect(Padding.Left + 16 * s - scrollX, drawY, contentWidth - 24 * s, block.Height, _bgPaint);
                _borderPaint.Color = ColorScheme.BorderColor; _borderPaint.StrokeWidth = 4 * s;
                canvas.DrawLine(Padding.Left + 12 * s - scrollX, drawY, Padding.Left + 12 * s - scrollX, drawY + block.Height, _borderPaint);
                _textPaint.Color = ColorScheme.ForeColor.WithAlpha(200);
                DrawWrappedInlines(canvas, block.WrappedLines, _fontItalic, drawX + 24 * s, drawY, blockIndex);
                break;

            case BlockType.ListItem:
                _textPaint.Typeface = _fontBody.Typeface; 
                _textPaint.TextSize = _fontBody.Size;
                _textPaint.Color = ColorScheme.ForeColor;
                string prefix = block.IsOrdered ? $"{block.ListIndex}." : "•";
                canvas.DrawText(prefix, drawX + 8 * s, drawY - _fontBody.Metrics.Ascent, _textPaint);
                DrawWrappedInlines(canvas, block.WrappedLines, _fontBody, drawX + 24 * s, drawY, blockIndex);
                break;

            case BlockType.Hr:
                _borderPaint.Color = ColorScheme.BorderColor; _borderPaint.StrokeWidth = 2 * s;
                canvas.DrawLine(Padding.Left - scrollX, drawY + block.Height / 2, Width - Padding.Right - scrollX, drawY + block.Height / 2, _borderPaint);
                break;

            default: 
                _textPaint.Color = ColorScheme.ForeColor;
                DrawWrappedInlines(canvas, block.WrappedLines, _fontBody, drawX, drawY, blockIndex);
                break;
        }
    }

    private void DrawTable(SKCanvas canvas, MdBlock block, float x, float y, float s)
    {
        float colWidth = block.TableColWidth;
        float cellPadding = 16 * s;
        float totalWidth = colWidth * block.TableData[0].Count;
        
        _textPaint.Color = ColorScheme.ForeColor;
        float currentY = y;

        for (int r = 0; r < block.TableData.Count; r++)
        {
            float rowHeight = block.TableRowHeights[r];
            float rowX = x;
            bool isHeader = (r == 0);
            SKFont cellFont = isHeader ? _fontBold : _fontBody;
            float lineHeight = GetLineHeight(cellFont);

            if (isHeader) {
                _bgPaint.Color = ColorScheme.SurfaceContainer;
                canvas.DrawRect(x, currentY, totalWidth, rowHeight, _bgPaint);
            }

            for (int c = 0; c < block.TableWrappedCells[r].Count; c++)
            {
                var lines = block.TableWrappedCells[r][c];
                float textX = rowX + cellPadding;
                float drawTextY = currentY + cellPadding - cellFont.Metrics.Ascent;

                _textPaint.Typeface = cellFont.Typeface;
                _textPaint.TextSize = cellFont.Size;

                foreach (var line in lines) {
                    canvas.DrawText(line, textX, drawTextY, _textPaint);
                    drawTextY += lineHeight;
                }
                rowX += colWidth;
            }

            _borderPaint.Color = ColorScheme.BorderColor;
            if (isHeader) {
                _borderPaint.StrokeWidth = 2 * s;
                canvas.DrawLine(x, currentY + rowHeight, x + totalWidth, currentY + rowHeight, _borderPaint);
            } else if (r < block.TableData.Count - 1) {
                _borderPaint.StrokeWidth = 1 * s;
                _borderPaint.Color = ColorScheme.BorderColor.WithAlpha(60);
                canvas.DrawLine(x, currentY + rowHeight, x + totalWidth, currentY + rowHeight, _borderPaint);
            }
            currentY += rowHeight;
        }
    }

    private void DrawWrappedInlines(SKCanvas canvas, List<List<InlineRun>> lines, SKFont defaultFont, float x, float y, int blockIndex)
    {
        float lineHeight = GetLineHeight(defaultFont);
        float baseline = y - defaultFont.Metrics.Ascent;

        for (int l = 0; l < lines.Count; l++)
        {
            var line = lines[l];
            float currentX = x;

            if (HasSelection)
            {
                var (minP, maxP) = (_selStart.BlockIndex < _selEnd.BlockIndex || (_selStart.BlockIndex == _selEnd.BlockIndex && _selStart.LineIndex <= _selEnd.LineIndex)) 
                    ? (_selStart, _selEnd) : (_selEnd, _selStart);
                
                if (blockIndex >= minP.BlockIndex && blockIndex <= maxP.BlockIndex)
                {
                    _selectionPaint.Color = ColorScheme.Primary.WithAlpha(60);
                    float selLeft = x;
                    float selRight = Width - Padding.Right;

                    if (blockIndex == minP.BlockIndex && l == minP.LineIndex)
                        selLeft = GetRunXOffset(line, minP.CharIndex, x, defaultFont);
                    
                    if (blockIndex == maxP.BlockIndex && l == maxP.LineIndex)
                        selRight = GetRunXOffset(line, maxP.CharIndex, x, defaultFont);

                    if (selRight > selLeft)
                        canvas.DrawRect(selLeft, y, selRight - selLeft, lineHeight, _selectionPaint);
                }
            }

            foreach (var run in line)
            {
                float w;
                
                if (run.Type == InlineType.Emoji)
                {
                    _emojiPaint.Typeface = _emojiTypeface;
                    _emojiPaint.TextSize = defaultFont.Size;
                    w = _emojiPaint.MeasureText(run.Text);
                    canvas.DrawText(run.Text, currentX, baseline, _emojiPaint);
                }
                else
                {
                    var font = GetFontForRun(run, defaultFont);
                    _textPaint.Typeface = font.Typeface; 
                    _textPaint.TextSize = font.Size;
                    _textPaint.Color = ColorScheme.ForeColor;
                    w = _textPaint.MeasureText(run.Text);

                    if (run.Type == InlineType.Code) {
                        float paddingX = 4 * ScaleFactor;
                        float paddingY = 2 * ScaleFactor;
                        float bgTop = baseline + defaultFont.Metrics.Ascent - paddingY;
                        float bgBottom = baseline + defaultFont.Metrics.Descent + paddingY;
                        
                        _bgPaint.Color = ColorScheme.BorderColor.WithAlpha(40);
                        canvas.DrawRoundRect(currentX - paddingX, bgTop, w + paddingX * 2, bgBottom - bgTop, 3 * ScaleFactor, 3 * ScaleFactor, _bgPaint);
                    }
                    
                    canvas.DrawText(run.Text, currentX, baseline, _textPaint);
                    
                    if (run.Type == InlineType.Strike) {
                        _borderPaint.Color = _textPaint.Color; _borderPaint.StrokeWidth = 1 * ScaleFactor;
                        float strikeY = baseline + (defaultFont.Metrics.Ascent / 2f);
                        canvas.DrawLine(currentX, strikeY, currentX + w, strikeY, _borderPaint);
                    }
                }

                currentX += w;
            }

            if (Focused && !HasSelection && _showCaret && blockIndex == _selStart.BlockIndex && l == _selStart.LineIndex)
            {
                _borderPaint.Color = ColorScheme.ForeColor;
                _borderPaint.StrokeWidth = 1.5f * ScaleFactor;
                float caretX = GetRunXOffset(line, _selStart.CharIndex, x, defaultFont);
                canvas.DrawLine(caretX, y, caretX, y + lineHeight, _borderPaint);
            }

            y += lineHeight;
            baseline += lineHeight;
        }
    }
    #endregion
}

#region Models & AST
public enum BlockType { Paragraph, Heading, Code, Quote, ListItem, Hr, Table }
public enum InlineType { Normal, Bold, Italic, Code, Strike, Link, Image, Emoji }

public class MdBlock
{
    public BlockType Type;
    public float Y, Height, MarginBottom;
    public string Text, RawText, Lang;
    public int Level;
    public bool IsOrdered;
    public int ListIndex;
    
    public List<InlineRun> Inlines;
    public List<List<InlineRun>> WrappedLines;
    
    public List<SyntaxToken> Tokens;
    public List<List<SyntaxToken>> WrappedCodeLines;
    
    public List<string> TableRows;
    public List<List<string>> TableData;
    public float TableColWidth;
    public List<float> TableRowHeights;
    public List<List<List<string>>> TableWrappedCells;
}

public class InlineRun { public string Text; public InlineType Type; public string Url; }
public class SyntaxToken { public string Text; public string Scope; }

public class MarkdownStyleConfig
{
    public BaseStylesConfig BaseStyles { get; set; } = new();
    public Dictionary<string, ScopeColorConfig> Scopes { get; set; } = new();
    public Dictionary<string, List<SyntaxRuleConfig>> Languages { get; set; } = new();
    public static MarkdownStyleConfig Default => new();
}
public class BaseStylesConfig
{
    public float H1Size { get; set; } = 28; public float H2Size { get; set; } = 22;
    public float H3Size { get; set; } = 18; public float H4Size { get; set; } = 16;
    public float BodySize { get; set; } = 14; public float CodeSize { get; set; } = 13;
    public string CodeBackgroundLight { get; set; } = "#F6F8FA"; public string CodeBackgroundDark { get; set; } = "#161B22";
}
public class ScopeColorConfig { public string Light { get; set; } = "#000"; public string Dark { get; set; } = "#FFF"; }
public class SyntaxRuleConfig { public string Pattern { get; set; } = ""; public string Scope { get; set; } = "default"; }
public class CompiledSyntaxRule { public Regex Regex { get; set; } public string Scope { get; set; } }
#endregion