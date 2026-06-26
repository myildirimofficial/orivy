using Orivy.Binding;
using Orivy.Collections;
using Orivy.Controls.Markdown;
using Orivy.Enums;
using Orivy.Helpers;
using Orivy.Layout;
using Orivy.Validations;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Orivy.Controls.Markdown;

/// <summary>
/// A GitHub-flavored-Markdown viewer with:
///  • AutoScroll (inherited ScrollBar engine — rubber-band, auto-hide, animation)
///  • Syntax-highlighted fenced code blocks with per-block horizontal scroll
///  • Text selection + copy (Ctrl+C / Ctrl+A)
///  • Mouse-cursor text-beam over selectable runs
///  • Typographic replacements (---, --, ...) and :emoji: shortcodes
///  • Auto link-open in default browser when no LinkClicked handler is attached
///  • ![alt][id] reference image parsing fix
///  • Pixel-accurate inline-code baseline alignment regardless of surrounding font size
/// </summary>
public class MarkdownViewer : ElementBase
{
    // ── Parsed content + cached layout ──────────────────────────────────
    private MarkdownDocument? _document;
    private List<MdBox> _boxes = new();
    private float _contentHeight;
    private float _layoutWidth = -1f;
    private float _layoutFontScale = -1f;
    private bool _layoutDirty = true;
    private Dictionary<string, float> _headingPositions = new();

    // ── Theme ────────────────────────────────────────────────────────────
    private MarkdownTheme? _explicitTheme;
    private MarkdownTheme _resolvedTheme = MarkdownTheme.Light();
    private bool _themeIsExplicit;

    // ── Engines / interaction state ──────────────────────────────────────
    private readonly MarkdownFontCache _fonts = new();
    private readonly MarkdownInteractionState _interaction = new();
    private readonly MarkdownHoverState _hover = new();
    private IMarkdownImageProvider? _imageProvider = new HttpMarkdownImageProvider();

    // ── Code-block horizontal drag-scroll ────────────────────────────────
    private CodeBlockBox? _draggingCodeBlock;
    private TableBox?     _draggingTable;
    private float _dragStartScrollX;
    private float _dragStartMouseX;

    // ── Text selection ───────────────────────────────────────────────────
    private readonly MarkdownSelectionState _selection = new();
    private SKPoint _mouseDownContent;  // content-space mouse-down point for drag detection

    // ── Copy-button "just copied" flash ─────────────────────────────────
    private CodeBlockBox? _copiedCodeBlock;
    private System.Timers.Timer? _copyFlashTimer;

    public MarkdownViewer()
    {
        AutoScroll = true;
        Padding    = new Thickness(16, 16, 16, 16);
        BackColor  = SKColors.Transparent;
        CanSelect  = true;
        TabStop    = true;

        _interaction.ImageProvider  = _imageProvider;
        _interaction.OnImageLoaded  = OnImageLoaded;

        ColorScheme.ThemeChanged += OnHostColorSchemeChanged;
        ResolveTheme();
    }

    // ====================================================================
    // Public API
    // ====================================================================

    /// <summary>The markdown source. Backed by <see cref="ElementBase.Text"/>.</summary>
    [Category("Data"), Browsable(true)]
    public string Markdown
    {
        get => Text;
        set => Text = value ?? string.Empty;
    }

    /// <summary>
    /// Visual theme. When null/never set, auto-picks Light/Dark from
    /// <c>ColorScheme.ForeColor</c> luminance and tracks host-app theme changes.
    /// </summary>
    [Browsable(false)]
    public MarkdownTheme Theme
    {
        get => _resolvedTheme;
        set
        {
            _explicitTheme   = value;
            _themeIsExplicit = value != null;
            ResolveTheme();
        }
    }

    [Browsable(false)]
    public IMarkdownImageProvider? ImageProvider
    {
        get => _imageProvider;
        set
        {
            _imageProvider             = value;
            _interaction.ImageProvider = value;
            _layoutDirty               = true;
            ReflowContent();
        }
    }

    [Category("Behavior"), DefaultValue(true)]
    public bool EnableTaskListInteraction { get; set; } = true;

    /// <summary>
    /// When true (default) and no <see cref="LinkClicked"/> handler is attached,
    /// clicking a link auto-opens it in the system default browser.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AutoOpenLinks { get; set; } = true;

    public event EventHandler<MarkdownLinkEventArgs>?       LinkClicked;
    public event EventHandler<MarkdownImageEventArgs>?      ImageClicked;
    public event EventHandler<MarkdownTaskToggledEventArgs>? TaskToggled;
    public event EventHandler<MarkdownCodeCopyEventArgs>?   CodeCopyRequested;
    public event EventHandler?                              MarkdownParsed;

    /// <summary>Returns the currently selected plain text, or empty string.</summary>
    public string GetSelectedText()
    {
        if (!_selection.HasSelection) return string.Empty;
        var (from, to) = _selection.Ordered();
        var sb = new StringBuilder();
        for (int idx = from.BoxIndex; idx <= to.BoxIndex && idx < _boxes.Count; idx++)
        {
            if (_boxes[idx] is not TextRunBox run) continue;
            if (run.IsNewlineSentinel) continue;
            int s = idx == from.BoxIndex ? from.CharOffset : 0;
            int e = idx == to.BoxIndex   ? to.CharOffset   : run.Text.Length;
            if (s < e && e <= run.Text.Length)
                sb.Append(run.Text, s, e - s);
        }
        return sb.ToString();
    }

    public IReadOnlyList<(int Level, string Text, string Slug)> GetOutline() =>
        _document?.Outline ?? new List<(int Level, string Text, string Slug)>();

    public bool ScrollToHeading(string slug, bool animate = true)
    {
        if (_vScrollBar == null || string.IsNullOrEmpty(slug) ||
            !_headingPositions.TryGetValue(slug, out var y)) return false;
        float target = Math.Clamp(y - 8f * ScaleFactor, _vScrollBar.Minimum, _vScrollBar.Maximum);
        if (animate) _vScrollBar.Value = target; else _vScrollBar.SetValueImmediate(target);
        return true;
    }

    public void ScrollToTop(bool animate = true)
    {
        if (_vScrollBar == null) return;
        if (animate) _vScrollBar.Value = _vScrollBar.Minimum;
        else         _vScrollBar.SetValueImmediate(_vScrollBar.Minimum);
    }

    public void ScrollToBottom(bool animate = true)
    {
        if (_vScrollBar == null) return;
        if (animate) _vScrollBar.Value = _vScrollBar.Maximum;
        else         _vScrollBar.SetValueImmediate(_vScrollBar.Maximum);
    }

    /// <summary>Opens a URL in the OS default browser. Fire-and-forget, errors swallowed.</summary>
    public static void OpenInDefaultBrowser(string url)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch { /* best-effort */ }
    }

    // ====================================================================
    // ElementBase hooks
    // ====================================================================

    protected override bool ProcessTextEscapeSequences => false;
    protected override bool ShouldRenderDefaultText    => false;

    internal override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        _document = MarkdownParser.Parse(Text);
        _interaction.CodeScroll.Clear();
        _interaction.DetailsExpanded.Clear();
        _selection.Clear();
        _layoutDirty = true;
        ReflowContent();
        MarkdownParsed?.Invoke(this, EventArgs.Empty);
    }

    internal override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width > 0 && Height > 0) ReflowContent();
    }

    internal override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        ReflowContent();
    }

    internal override void OnDpiChanged(float newDpi, float oldDpi)
    {
        base.OnDpiChanged(newDpi, oldDpi);
        ReflowContent();
    }

    internal override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _layoutDirty = true;
        ReflowContent();
    }

    // ====================================================================
    // Theme
    // ====================================================================

    private void ResolveTheme()
    {
        var candidate = _explicitTheme ?? PickAutoTheme();
        if (ReferenceEquals(candidate, _resolvedTheme)) return;
        _resolvedTheme = candidate ?? MarkdownTheme.Light();
        _layoutDirty   = true;
        ReflowContent();
    }

    private MarkdownTheme PickAutoTheme()
    {
        try { return MarkdownTheme.IsLightColor(ColorScheme.ForeColor) ? MarkdownTheme.Dark() : MarkdownTheme.Light(); }
        catch { return MarkdownTheme.Light(); }
    }

    private void OnHostColorSchemeChanged(object? sender, EventArgs e)
    {
        if (!_themeIsExplicit) ResolveTheme();
        Invalidate();
    }

    // ====================================================================
    // Layout
    // ====================================================================

    private void ReflowContent()
    {
        float availableWidth = MathF.Max(40f, Width - Padding.Left - Padding.Right);
        float scale          = ScaleFactor;

        bool widthChanged = MathF.Abs(availableWidth - _layoutWidth)   > 0.5f;
        bool scaleChanged = MathF.Abs(scale          - _layoutFontScale) > 0.001f;
        if (!_layoutDirty && !widthChanged && !scaleChanged) return;

        _fonts.SetHostBodyTypeface(Font?.Typeface);

        _boxes = _document == null
            ? new List<MdBox>()
            : MarkdownLayoutBuilder.Build(_document, _resolvedTheme, _fonts,
                availableWidth, scale, Padding.Left, Padding.Top, Padding.Bottom,
                _interaction, out _contentHeight, out _headingPositions);

        _layoutWidth      = availableWidth;
        _layoutFontScale  = scale;
        _layoutDirty      = false;
        _selection.Clear();   // box indices are stale after a reflow

        AutoScrollMinSize = new SKSize(0, _contentHeight);
        UpdateScrollBars();
        Invalidate();
    }

    private void OnImageLoaded(string url, SKImage? image)
    {
        _layoutDirty = true;
        ReflowContent();
    }

    private float GetVerticalScrollOffset() =>
        _vScrollBar != null && _vScrollBar.Visible ? _vScrollBar.DisplayValue : 0f;

    // ====================================================================
    // Painting
    // ====================================================================

    public override void OnPaint(SKCanvas canvas)
    {
        if (_layoutDirty) ReflowContent();

        int saved = canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, Width, Height));

        float scrollY = GetVerticalScrollOffset();
        canvas.Translate(0, -scrollY);

        MarkdownBoxRenderer.Draw(canvas, _boxes, scrollY, scrollY + Height,
            _resolvedTheme, _hover, _imageProvider, _selection);

        canvas.RestoreToCount(saved);
    }

    // ====================================================================
    // Hit testing
    // ====================================================================

    private readonly struct HitResult
    {
        public readonly LinkInline?     Link;
        public readonly ListItemBlock?  Checkbox;
        public readonly ImageInline?    Image;
        public readonly CodeBlockBox?   CodeBlock;
        public readonly bool            OverCopyButton;
        public readonly DetailsBlock?   Details;
        public readonly TextRunBox?     TextRun;
        public readonly int             TextRunBoxIndex;
        public readonly TableBox?       TableBox;

        public HitResult(LinkInline? link, ListItemBlock? checkbox, ImageInline? image,
            CodeBlockBox? codeBlock, bool overCopy, DetailsBlock? details,
            TextRunBox? textRun = null, int textRunBoxIndex = -1,
            TableBox? tableBox = null)
        {
            Link = link; Checkbox = checkbox; Image = image;
            CodeBlock = codeBlock; OverCopyButton = overCopy; Details = details;
            TextRun = textRun; TextRunBoxIndex = textRunBoxIndex;
            TableBox = tableBox;
        }

        public static readonly HitResult None = new(null, null, null, null, false, null);
    }

    private HitResult HitTestBoxes(SKPoint p)
    {
        for (int i = _boxes.Count - 1; i >= 0; i--)
        {
            var box = _boxes[i];
            if (!box.Bounds.Contains(p)) continue;

            switch (box)
            {
                case CodeBlockBox code:
                    return new HitResult(null, null, null, code, code.CopyButtonRect.Contains(p), null);
                case TableBox tbl:
                    return new HitResult(null, null, null, null, false, null, tableBox: tbl);
                case DetailsHeaderBox dh:
                    return new HitResult(null, null, null, null, false, dh.Source);
                case CheckboxBox cb:
                    return new HitResult(null, cb.Item, null, null, false, null);
                case ImageBox img:
                    return new HitResult(img.Link, null, img.Source, null, false, null);
                case TextRunBox t:
                    return new HitResult(t.Link, null, null, t.CodeOwner, false, null, t, i);
            }
        }
        return HitResult.None;
    }

    /// <summary>
    /// Returns the TextRunBox and its list index closest to <paramref name="contentPoint"/>.
    /// Uses Y-band matching first (same text line), then nearest horizontal run.
    /// This makes selection stable even when the mouse is between word-wrapped lines.
    /// </summary>
    private (TextRunBox? run, int index) FindNearestTextRun(SKPoint contentPoint)
    {
        TextRunBox? bestRun  = null;
        int         bestIdx  = -1;
        float       bestDist = float.MaxValue;

        for (int i = 0; i < _boxes.Count; i++)
        {
            if (_boxes[i] is not TextRunBox t) continue;
            if (t.IsNewlineSentinel) continue;

            // For code-block-owned runs the effective X is shifted by the scroll offset
            float effectiveX = contentPoint.X + (t.CodeOwner?.Scroll.ScrollX ?? 0f);

            // Vertical: use an expanded hit band (±2px) so gaps between lines still hit
            float yMid  = (t.Bounds.Top + t.Bounds.Bottom) * 0.5f;
            float yDist = MathF.Max(0f, MathF.Abs(contentPoint.Y - yMid) - t.Bounds.Height * 0.5f);

            // Skip code runs outside the owner's body rect (clipped by the renderer)
            if (t.CodeOwner != null)
            {
                float scrolledX = t.Bounds.Left - t.CodeOwner.Scroll.ScrollX;
                if (scrolledX + t.Bounds.Width < t.CodeOwner.BodyRect.Left ||
                    scrolledX > t.CodeOwner.BodyRect.Right) continue;
            }

            // Horizontal: clamp to run width
            float xLeft  = t.Bounds.Left;
            float xRight = t.Bounds.Right;
            float xDist  = effectiveX < xLeft  ? xLeft  - effectiveX
                         : effectiveX > xRight  ? effectiveX - xRight : 0f;

            float dist = yDist * 4f + xDist;   // weight Y more than X

            if (dist < bestDist) { bestDist = dist; bestRun = t; bestIdx = i; }
        }

        return (bestRun, bestIdx);
    }

    /// <summary>Returns the character range of the word at <paramref name="charOffset"/> within <paramref name="text"/>.</summary>
    private static (int Start, int End) FindWordBounds(string text, int charOffset)
    {
        if (string.IsNullOrEmpty(text)) return (0, 0);
        charOffset = Math.Clamp(charOffset, 0, text.Length);

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        int start = charOffset;
        while (start > 0 && IsWordChar(text[start - 1])) start--;

        int end = charOffset;
        while (end < text.Length && IsWordChar(text[end])) end++;

        // If no word chars at offset, select the single non-word char
        if (start == end && end < text.Length) end++;

        return (start, end);
    }

    private (TextRunBox? run, int index) FindTextRunAt(SKPoint contentPoint)
    {
        for (int i = _boxes.Count - 1; i >= 0; i--)
            if (_boxes[i] is TextRunBox t && t.Bounds.Contains(contentPoint))
                return (t, i);
        return (null, -1);
    }

    private bool IsOverOwnScrollBar(SKPoint p)
    {
        if (_vScrollBar != null && _vScrollBar.Visible && _vScrollBar.Bounds.Contains(p)) return true;
        if (_hScrollBar != null && _hScrollBar.Visible && _hScrollBar.Bounds.Contains(p)) return true;
        return false;
    }

    private static List<MarkdownInline> GetItemTextInlines(ListItemBlock item)
    {
        foreach (var b in item.Blocks)
            if (b is ParagraphBlock p) return p.Inlines;
        return new List<MarkdownInline>();
    }

    // ====================================================================
    // Mouse — move
    // ====================================================================

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // ── Code block horizontal drag ──
        if (_draggingCodeBlock != null)
        {
            float dx        = e.X - _dragStartMouseX;
            float maxScroll = Math.Max(0f, _draggingCodeBlock.ContentWidth - _draggingCodeBlock.ViewportWidth);
            _draggingCodeBlock.Scroll.ScrollX = Math.Clamp(_dragStartScrollX + dx, 0f, maxScroll);
            Invalidate();
            return;
        }

        // ── Table horizontal drag ──
        if (_draggingTable != null)
        {
            float dx        = e.X - _dragStartMouseX;
            float maxScroll = Math.Max(0f, _draggingTable.ContentWidth - _draggingTable.ViewportWidth);
            // Dragging left = positive dx → content moves right → scroll decreases
            _draggingTable.Scroll.ScrollX = Math.Clamp(_dragStartScrollX - dx, 0f, maxScroll);
            Invalidate();
            return;
        }

        // ── Text selection drag ──
        if (_selection.IsSelecting)
        {
            var contentPoint = new SKPoint(e.X, e.Y + GetVerticalScrollOffset());
            var (run, idx) = FindNearestTextRun(contentPoint);
            if (run != null)
            {
                float localX = contentPoint.X - run.Bounds.Left;
                _selection.End = new TextPosition { BoxIndex = idx, CharOffset = run.GetCharOffsetAt(localX) };
                Invalidate();
            }
            return;
        }

        if (IsOverOwnScrollBar(e.Location)) { ClearHover(); return; }

        var cp  = new SKPoint(e.X, e.Y + GetVerticalScrollOffset());
        var hit = HitTestBoxes(cp);
        ApplyHover(hit, cp);
    }

    // ====================================================================
    // Mouse — down
    // ====================================================================

    internal override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (IsOverOwnScrollBar(e.Location)) return;

        var contentPoint = new SKPoint(e.X, e.Y + GetVerticalScrollOffset());
        var hit = HitTestBoxes(contentPoint);

        // Code-block horizontal drag
        if (hit.CodeBlock != null && hit.CodeBlock.NeedsHorizontalScroll && !hit.OverCopyButton
            && hit.TextRun == null)
        {
            _draggingCodeBlock = hit.CodeBlock;
            _dragStartScrollX  = hit.CodeBlock.Scroll.ScrollX;
            _dragStartMouseX   = e.X;
            return;
        }

        // Table horizontal drag
        if (hit.TableBox != null && hit.TableBox.NeedsHorizontalScroll)
        {
            _draggingTable    = hit.TableBox;
            _dragStartScrollX = hit.TableBox.Scroll.ScrollX;
            _dragStartMouseX  = e.X;
            return;
        }

        // Track the content-space down point for drag vs click detection
        _mouseDownContent = contentPoint;

        // Begin text selection
        if (hit.TextRun != null)
        {
            float localX = contentPoint.X - hit.TextRun.Bounds.Left;
            int   offset = hit.TextRun.GetCharOffsetAt(localX);
            _selection.Start = new TextPosition { BoxIndex = hit.TextRunBoxIndex, CharOffset = offset };
            _selection.End   = _selection.Start;
            _selection.IsSelecting = true;
            Invalidate();
            return;
        }

        // Clicking empty space clears selection
        _selection.Clear();
        Invalidate();
    }

    // ====================================================================
    // Mouse — up
    // ====================================================================

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingCodeBlock    = null;
        _draggingTable        = null;
        _selection.IsSelecting = false;
    }

    // ====================================================================
    // Mouse — leave
    // ====================================================================

    internal override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        ClearHover();
        _draggingCodeBlock     = null;
        _draggingTable         = null;
        _selection.IsSelecting = false;
    }

    // ====================================================================
    // Mouse — click (link open, copy button, checkbox, details toggle)
    // ====================================================================

    protected internal override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        if (IsOverOwnScrollBar(e.Location)) return;

        var contentPoint = new SKPoint(e.X, e.Y + GetVerticalScrollOffset());
        var hit = HitTestBoxes(contentPoint);

        // ── Copy button ──
        if (hit.OverCopyButton && hit.CodeBlock != null)
        {
            var code = hit.CodeBlock.Source.Code;
            var lang = hit.CodeBlock.Language;

            if (CodeCopyRequested != null)
            {
                CodeCopyRequested.Invoke(this, new MarkdownCodeCopyEventArgs(code, lang));
            }
            else
            {
                // Default: put code on the clipboard
                TryCopyToClipboard(code);
            }
            return;
        }

        // ── Link ──
        if (hit.Link != null)
        {
            // Suppress link navigation when the user dragged to select text
            var contentPoint2 = new SKPoint(e.X, e.Y + GetVerticalScrollOffset());
            float dragDist = SKPoint.Distance(contentPoint2, _mouseDownContent);
            if (dragDist < 4f)
            {
                var args = new MarkdownLinkEventArgs(hit.Link.Url, hit.Link.Title,
                    MarkdownParser.PlainText(hit.Link.Children));
                if (LinkClicked != null)
                    LinkClicked.Invoke(this, args);
                else if (AutoOpenLinks)
                    OpenInDefaultBrowser(hit.Link.Url);
            }
            return;
        }

        // ── Task checkbox ──
        if (hit.Checkbox != null && EnableTaskListInteraction)
        {
            bool newValue = !(hit.Checkbox.TaskChecked ?? false);
            hit.Checkbox.TaskChecked = newValue;
            _layoutDirty = true;
            ReflowContent();
            TaskToggled?.Invoke(this, new MarkdownTaskToggledEventArgs(newValue,
                MarkdownParser.PlainText(GetItemTextInlines(hit.Checkbox))));
            return;
        }

        // ── Details toggle ──
        if (hit.Details != null)
        {
            bool current = _interaction.DetailsExpanded.TryGetValue(hit.Details, out var v)
                ? v : hit.Details.DefaultOpen;
            _interaction.DetailsExpanded[hit.Details] = !current;
            _layoutDirty = true;
            ReflowContent();
            return;
        }

        // ── Image ──
        if (hit.Image != null)
        {
            ImageClicked?.Invoke(this, new MarkdownImageEventArgs(hit.Image.Url, hit.Image.AltText));
        }
    }

    // ====================================================================
    // Clipboard — Win32 P/Invoke (reliable on all Windows UI frameworks)
    // ====================================================================

    private static void TryCopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            if (!OpenClipboard(IntPtr.Zero)) return;
            EmptyClipboard();
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
            var hMem = GlobalAlloc(0x0042u /* GMEM_MOVEABLE | GMEM_ZEROINIT */, (UIntPtr)(uint)bytes.Length);
            if (hMem == IntPtr.Zero) { CloseClipboard(); return; }
            var ptr = GlobalLock(hMem);
            if (ptr != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
                GlobalUnlock(hMem);
            }
            SetClipboardData(13u /* CF_UNICODETEXT */, hMem);
        }
        catch { /* clipboard access can fail silently */ }
        finally { try { CloseClipboard(); } catch { } }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EmptyClipboard();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool CloseClipboard();
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    // ====================================================================
    // Mouse — wheel  (vertical page scroll + code-block horizontal scroll)
    // ====================================================================

    internal override void OnMouseWheel(MouseEventArgs e)
    {
        // Horizontal wheel or Shift+wheel → scroll code block / table horizontally
        bool isHorizontal = e.IsHorizontalWheel || (ModifierKeys & Keys.Shift) != 0;

        if (isHorizontal)
        {
            if (_hover.HoveredCodeBlock != null && _hover.HoveredCodeBlock.NeedsHorizontalScroll)
            {
                ScrollCodeBlockHorizontal(_hover.HoveredCodeBlock, e.IsHorizontalWheel ? e.Delta : -e.Delta);
                Invalidate();
                e.Handled = true;
                return;
            }
            if (_hover.HoveredTableBox != null && _hover.HoveredTableBox.NeedsHorizontalScroll)
            {
                ScrollTableHorizontal(_hover.HoveredTableBox, e.IsHorizontalWheel ? e.Delta : -e.Delta);
                Invalidate();
                e.Handled = true;
                return;
            }
        }

        // Vertical wheel → always pass to base for page scroll
        base.OnMouseWheel(e);
    }

    private void ScrollTableHorizontal(TableBox tbl, int delta)
    {
        float maxScroll = Math.Max(0f, tbl.ContentWidth - tbl.ViewportWidth);
        float step = 40f * ScaleFactor * (Math.Abs(delta) / 120f);
        // Positive delta = wheel up/right → scroll content left (ScrollX decreases)
        tbl.Scroll.ScrollX = Math.Clamp(tbl.Scroll.ScrollX + (delta > 0 ? -step : step), 0f, maxScroll);
    }

    private void ScrollCodeBlockHorizontal(CodeBlockBox cb, int delta)
    {
        float maxScroll = Math.Max(0f, cb.ContentWidth - cb.ViewportWidth);
        float step = 40f * ScaleFactor * (Math.Abs(delta) / 120f);
        cb.Scroll.ScrollX = Math.Clamp(cb.Scroll.ScrollX + (delta > 0 ? -step : step), 0f, maxScroll);
    }

    // ====================================================================
    // Keyboard — scrolling + text selection copy
    // ====================================================================

    internal override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // ── Ctrl+C — copy selected text ──
        if (e.Control && e.KeyCode == Keys.C)
        {
            string sel = GetSelectedText();
            if (!string.IsNullOrEmpty(sel)) TryCopyToClipboard(sel);
            e.Handled = true;
            return;
        }

        // ── Ctrl+A — select all ──
        if (e.Control && e.KeyCode == Keys.A)
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        // ── Escape — clear selection ──
        if (e.KeyCode == Keys.Escape && _selection.HasSelection)
        {
            _selection.Clear();
            Invalidate();
            e.Handled = true;
            return;
        }

        if (_vScrollBar == null) return;

        float page  = Math.Max(20f, Height - 40f);
        float small = Math.Max(20f, _vScrollBar.SmallChange);

        if      (e.KeyCode == Keys.Down)                     { _vScrollBar.Value += small; e.Handled = true; }
        else if (e.KeyCode == Keys.Up)                       { _vScrollBar.Value -= small; e.Handled = true; }
        else if (e.KeyCode == Keys.PageDown)                 { _vScrollBar.Value += page;  e.Handled = true; }
        else if (e.KeyCode == Keys.PageUp)                   { _vScrollBar.Value -= page;  e.Handled = true; }
        else if (e.KeyCode == Keys.Space  && !e.Shift)       { _vScrollBar.Value += page;  e.Handled = true; }
        else if (e.KeyCode == Keys.Space  && e.Shift)        { _vScrollBar.Value -= page;  e.Handled = true; }
        else if (e.KeyCode == Keys.Home)                     { _vScrollBar.Value = _vScrollBar.Minimum; e.Handled = true; }
        else if (e.KeyCode == Keys.End)                      { _vScrollBar.Value = _vScrollBar.Maximum; e.Handled = true; }
    }

    private void SelectAll()
    {
        // Find the first and last TextRunBox in the box list
        int  firstIdx = -1, lastIdx = -1;
        for (int i = 0; i < _boxes.Count; i++)
            if (_boxes[i] is TextRunBox)
            {
                if (firstIdx < 0) firstIdx = i;
                lastIdx = i;
            }
        if (firstIdx < 0) return;

        _selection.Start = new TextPosition { BoxIndex = firstIdx, CharOffset = 0 };
        var lastRun = (TextRunBox)_boxes[lastIdx];
        _selection.End = new TextPosition { BoxIndex = lastIdx, CharOffset = lastRun.Text.Length };
        Invalidate();
    }

    // ====================================================================
    // Hover state
    // ====================================================================

    private void ClearHover()
    {
        bool changed = _hover.HoveredLink != null || _hover.HoveredCodeBlock != null
                    || _hover.HoveredCopyButton || _hover.HoveredText
                    || _hover.HoveredTableBox != null;
        _hover.HoveredLink      = null;
        _hover.HoveredCodeBlock = null;
        _hover.HoveredCopyButton = false;
        _hover.HoveredText      = false;
        _hover.HoveredTableBox  = null;
        if (changed) Invalidate();
        Cursor = Cursors.Default;
    }

    private void ApplyHover(HitResult hit, SKPoint contentPoint)
    {
        bool isText = hit.TextRun != null && hit.Link == null;

        bool changed =
            !ReferenceEquals(_hover.HoveredLink,      hit.Link)      ||
            !ReferenceEquals(_hover.HoveredCodeBlock, hit.CodeBlock) ||
            _hover.HoveredCopyButton != hit.OverCopyButton           ||
            _hover.HoveredText       != isText                       ||
            !ReferenceEquals(_hover.HoveredTableBox,  hit.TableBox);

        _hover.HoveredLink       = hit.Link;
        _hover.HoveredCodeBlock  = hit.CodeBlock;
        _hover.HoveredCopyButton = hit.OverCopyButton;
        _hover.HoveredText       = isText;
        _hover.HoveredTableBox   = hit.TableBox;

        // Cursor
        if (hit.Link != null || hit.OverCopyButton || hit.Checkbox != null || hit.Details != null)
            Cursor = Cursors.Hand;
        else if (isText)
            Cursor = Cursors.IBeam;
        else
            Cursor = Cursors.Default;

        if (changed) Invalidate();
    }

    // ====================================================================
    // Double-click — word selection
    // ====================================================================

    internal override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left) return;
        if (IsOverOwnScrollBar(e.Location)) return;

        var cp = new SKPoint(e.X, e.Y + GetVerticalScrollOffset());
        var (run, idx) = FindTextRunAt(cp);
        if (run == null || idx < 0) return;

        float localX  = cp.X - run.Bounds.Left;
        int   offset  = run.GetCharOffsetAt(localX);
        var (ws, we)  = FindWordBounds(run.Text, offset);

        _selection.Start      = new TextPosition { BoxIndex = idx, CharOffset = ws };
        _selection.End        = new TextPosition { BoxIndex = idx, CharOffset = we };
        _selection.IsSelecting = false;
        Invalidate();
    }

    // ====================================================================
    // Dispose
    // ====================================================================

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnHostColorSchemeChanged;
            _fonts.Dispose();
            _copyFlashTimer?.Stop();
            _copyFlashTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// ============================================================================
// Event args
// ============================================================================

public sealed class MarkdownLinkEventArgs : EventArgs
{
    public string  Url   { get; }
    public string? Title { get; }
    public string  Text  { get; }
    public MarkdownLinkEventArgs(string url, string? title, string text)
    { Url = url; Title = title; Text = text; }
}

public sealed class MarkdownImageEventArgs : EventArgs
{
    public string Url     { get; }
    public string AltText { get; }
    public MarkdownImageEventArgs(string url, string altText) { Url = url; AltText = altText; }
}

public sealed class MarkdownTaskToggledEventArgs : EventArgs
{
    public bool   Checked { get; }
    public string Text    { get; }
    public MarkdownTaskToggledEventArgs(bool isChecked, string text) { Checked = isChecked; Text = text; }
}

public sealed class MarkdownCodeCopyEventArgs : EventArgs
{
    public string  Code     { get; }
    public string? Language { get; }
    public MarkdownCodeCopyEventArgs(string code, string? language) { Code = code; Language = language; }
}
