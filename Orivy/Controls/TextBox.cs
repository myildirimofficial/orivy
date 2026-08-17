using Orivy.Animation;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Orivy.Controls;

public class TextBox : ElementBase
{
    private const int DefaultWidth = 220;
    private const int DefaultHeight = 38;
    private const int DefaultMultilineHeight = 120;
    private const int DefaultMinWidth = 120;
    private const int DefaultMinHeight = 36;
    private const float DefaultTextZoomFactor = 1f;
    private const float DefaultTextZoomStep = 0.1f;
    private const float MinimumTextZoomFactor = 0.5f;
    private const float MaximumTextZoomFactor = 3f;
    private const float CaretThickness = 1.15f;
    private const float CaretMinimumVisibleOpacity = 0.28f;
    private const float SelectionInsetY = 2f;

    private readonly List<TextLineLayout> _lines = new();
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _placeholderPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _selectionPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _caretPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _caretFillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _focusCuePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly AnimationManager _caretBlinkAnimation;
    private readonly ContextMenuStrip _defaultContextMenu;
    private readonly MenuItem _cutMenuItem;
    private readonly MenuItem _copyMenuItem;
    private readonly MenuItem _pasteMenuItem;
    private readonly MenuItem _deleteMenuItem;
    private readonly MenuItem _clearMenuItem;
    private readonly MenuItem _selectAllMenuItem;

    private SKFont? _layoutFont;
    private SKFont? _layoutFontSource;
    private float _layoutFontScale;
    private float _layoutTextZoomFactor = DefaultTextZoomFactor;
    private bool _layoutDirty = true;
    private bool _displayTextDirty = true;
    private float _lineHeight;
    private float _baselineOffset;
    private float _contentWidth;
    private float _contentHeight;
    private string _displayText = string.Empty;
    private bool _multiline;
    protected bool _readOnly;
    private bool _acceptsReturn = true;
    private bool _acceptsTab;
    private bool _passwordMode;
    private string _placeholderText = string.Empty;
    private TextWrap _wrapMode = TextWrap.WordWrap;
    private int _maxLength;
    private int _selectionAnchor;
    private int _selectionCaret;
    private bool _mouseSelecting;
    private float _caretOpacity = 1f;
    private float _preferredCaretX = -1f;
    private float _textZoomFactor = DefaultTextZoomFactor;
    private float _textZoomStep = DefaultTextZoomStep;
    private char _passwordChar = '*';
    private TextBoxCaretMode _caretMode = TextBoxCaretMode.Bar;

    protected override bool HandlesMouseWheelScroll => _multiline && AutoScroll;

    public TextBox()
    {
        AutoScroll = true;
        CanSelect = true;
        Cursor = Cursors.IBeam;
        Padding = new Thickness(14, 10, 14, 10);
        Radius = new Radius(9);
        Border = new Thickness(1);
        BorderColor = ColorScheme.Outline.WithAlpha(104);
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        MinimumSize = new SKSize(DefaultMinWidth, DefaultMinHeight);
        Size = new SKSize(DefaultWidth, DefaultHeight);
        TabStop = true;
        TextAlign = ContentAlignment.MiddleLeft;

        ConfigureVisualStyles(styles =>
        {
            styles
                .DefaultTransition(TimeSpan.FromMilliseconds(170), AnimationType.CubicEaseOut)
                .Base(baseStyle => baseStyle
                    .Background(ColorScheme.Surface)
                    .Foreground(ColorScheme.ForeColor)
                    .Border(1)
                    .BorderColor(ColorScheme.Outline.WithAlpha(104))
                    .Radius(9)
                    .Shadow(new BoxShadow(0f, 4f, 14f, 0, ColorScheme.ShadowColor.WithAlpha(14))))
                .OnHover(rule => rule
                    .Background(ColorScheme.Surface.Brightness(0.014f))
                    .BorderColor(ColorScheme.Primary.WithAlpha(108))
                    .Shadow(new BoxShadow(0f, 8f, 20f, 0, ColorScheme.ShadowColor.WithAlpha(22))))
                .OnFocused(rule => rule
                    .Background(ColorScheme.Surface.Brightness(0.02f))
                    .Border(2)
                    .BorderColor(ColorScheme.Primary.WithAlpha(232))
                    .Shadow(new BoxShadow(0f, 10f, 24f, 0, ColorScheme.Primary.WithAlpha(28))))
                .OnInvalid(rule => rule
                    .BorderColor(SKColors.IndianRed.WithAlpha(210))
                    .Shadow(new BoxShadow(0f, 10f, 20f, 0, SKColors.IndianRed.WithAlpha(18))))
                .OnDisabled(rule => rule
                    .Background(ColorScheme.SurfaceVariant.WithAlpha(220))
                    .Foreground(ColorScheme.ForeColor.WithAlpha(138))
                    .BorderColor(ColorScheme.Outline.WithAlpha(64))
                    .Opacity(0.84f)
                    .Shadow(BoxShadow.None));
        });

        ConfigureMotionEffects(scene => scene
            .Rectangle(effect => effect
                .Anchor(0.14f, 0.2f)
                .Size(52f, 8f)
                .Drift(12f, 0f)
                .CornerRadius(6f)
                .Color(ColorScheme.Primary.WithAlpha(10))
                .Opacity(0.02f, 0.08f)
                .Scale(0.92f, 1.08f)
                .Duration(5.2d)
                .Delay(0.45d)
                .SpeedOnHover(1.2f)
                .SpeedOnFocused(2.1f))
            .Circle(effect => effect
                .Anchor(0.86f, 0.24f)
                .Size(28f, 28f)
                .Orbit(10f, 5f)
                .Color(ColorScheme.Primary.WithAlpha(9))
                .Opacity(0.02f, 0.07f)
                .Scale(0.88f, 1.1f)
                .Duration(6.1d)
                .Delay(1.1d)
                .SpeedOnHover(1.15f)
                .SpeedOnFocused(2.3f)));

        _caretBlinkAnimation = new AnimationManager(true)
        {
            AnimationType = AnimationType.CubicEaseInOut,
            InterruptAnimation = true,
        };
        UpdateCaretBlinkAnimationRate();
        _caretBlinkAnimation.OnAnimationProgress += HandleCaretBlinkAnimationProgress;
        _caretBlinkAnimation.OnAnimationFinished += HandleCaretBlinkAnimationFinished;

        _defaultContextMenu = new ContextMenuStrip { AutoClose = true, Dock = DockStyle.None };
        _defaultContextMenu.Opening += HandleDefaultContextMenuOpening;
        _cutMenuItem = _defaultContextMenu.AddMenuItem("Cut", (_, _) => CutSelection(), Keys.Control | Keys.X);
        _copyMenuItem = _defaultContextMenu.AddMenuItem("Copy", (_, _) => CopySelection(), Keys.Control | Keys.C);
        _pasteMenuItem = _defaultContextMenu.AddMenuItem("Paste", (_, _) => PasteFromClipboard(), Keys.Control | Keys.V);
        _deleteMenuItem = _defaultContextMenu.AddMenuItem("Delete", (_, _) => DeleteSelection(), Keys.Delete);
        _clearMenuItem = _defaultContextMenu.AddMenuItem("Clear", (_, _) => ClearText());
        _defaultContextMenu.AddSeparator();
        _selectAllMenuItem = _defaultContextMenu.AddMenuItem("Select All", (_, _) => SelectAll(), Keys.Control | Keys.A);
        ContextMenuStrip = _defaultContextMenu;
    }

    protected override bool ProcessTextEscapeSequences => false;

    protected override bool ShouldRenderDefaultText => false;

    [DefaultValue(false)]
    public bool Multiline
    {
        get => _multiline;
        set
        {
            if (_multiline == value)
                return;

            _multiline = value;

            AutoScroll = value;

            if (_multiline && Size.Height <= DefaultHeight)
                Size = new SKSize(Size.Width, DefaultMultilineHeight);

            InvalidateTextLayout();
        }
    }

    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value)
                return;

            _readOnly = value;
            ResetCaretBlink();
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool AcceptsReturn
    {
        get => _acceptsReturn;
        set => _acceptsReturn = value;
    }

    [DefaultValue(false)]
    public bool AcceptsTab
    {
        get => _acceptsTab;
        set => _acceptsTab = value;
    }

    [DefaultValue(false)]
    public bool PasswordMode
    {
        get => _passwordMode;
        set
        {
            if (_passwordMode == value)
                return;

            _passwordMode = value;
            InvalidateDisplayedText();
            ResetCaretBlink();
        }
    }

    [DefaultValue('*')]
    public char PasswordChar
    {
        get => _passwordChar;
        set
        {
            var resolved = ResolvePasswordChar(value);
            if (_passwordChar == resolved)
                return;

            _passwordChar = resolved;
            if (_passwordMode)
                InvalidateDisplayedText();
        }
    }

    [DefaultValue("")]
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            var next = value ?? string.Empty;
            if (_placeholderText == next)
                return;

            _placeholderText = next;
            InvalidateMeasure();
            Invalidate();
        }
    }

    [DefaultValue(typeof(TextWrap), nameof(TextWrap.WordWrap))]
    public override TextWrap WrapMode
    {
        get => _wrapMode;
        set
        {
            if (_wrapMode == value)
                return;

            _wrapMode = value;
            InvalidateTextLayout();
        }
    }

    [DefaultValue(DefaultTextZoomFactor)]
    public float TextZoomFactor
    {
        get => _textZoomFactor;
        set => SetTextZoomFactor(value);
    }

    [DefaultValue(DefaultTextZoomStep)]
    public float TextZoomStep
    {
        get => _textZoomStep;
        set => _textZoomStep = Math.Clamp(value, 0.05f, 1f);
    }

    [Browsable(false)]
    public int TextZoomPercent => (int)Math.Round(_textZoomFactor * 100f);

    [DefaultValue(0)]
    public int MaxLength
    {
        get => _maxLength;
        set
        {
            var next = Math.Max(0, value);
            if (_maxLength == next)
                return;

            _maxLength = next;
            if (_maxLength > 0 && Text.Length > _maxLength)
                Text = Text[.._maxLength];
        }
    }

    public override string Text
    {
        get => base.Text;
        set
        {
            var normalized = NormalizeTextForStorage(value);
            if (base.Text == normalized)
                return;

            _displayTextDirty = true;
            base.Text = normalized;
        }
    }

    [Browsable(false)]
    public int CaretIndex
    {
        get => _selectionCaret;
        set => Select(value, 0);
    }

    [Browsable(false)]
    public int SelectionStart => Math.Min(_selectionAnchor, _selectionCaret);

    [Browsable(false)]
    public int SelectionLength => Math.Abs(_selectionCaret - _selectionAnchor);

    [Browsable(false)]
    public string SelectedText
        => SelectionLength == 0 ? string.Empty : Text.Substring(SelectionStart, SelectionLength);

    [Browsable(false)]
    public bool HasSelection => SelectionLength > 0;

    [Browsable(false)]
    public string[] Lines => Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    [DefaultValue(typeof(TextBoxCaretMode), nameof(TextBoxCaretMode.Bar))]
    public TextBoxCaretMode CaretMode
    {
        get => _caretMode;
        set
        {
            if (_caretMode == value)
                return;

            _caretMode = value;
            ResetCaretBlink();
            Invalidate();
        }
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler? TextZoomFactorChanged;

    public void Select(int start, int length)
    {
        var boundedStart = ClampCaretIndex(start);
        var boundedEnd = ClampCaretIndex(start + Math.Max(0, length));
        SetSelectionCore(boundedStart, boundedEnd, preservePreferredCaretX: false, ensureVisible: true);
    }

    public void SelectAll()
    {
        SetSelectionCore(0, Text.Length, preservePreferredCaretX: false, ensureVisible: false);
        ResetCaretBlink();
    }

    public void AppendText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        Select(Text.Length, 0);
        ReplaceSelection(value);
    }

    public void ScrollToCaret()
    {
        EnsureCaretVisible();
    }

    /// <summary>Returns the character index closest to <paramref name="point"/>,
    /// given in control-local coordinates (e.g. from a <see cref="MouseEventArgs"/>).
    /// The point is clamped to the text viewport, so positions outside the
    /// control still resolve to the nearest valid index.</summary>
    public virtual int GetCharIndexFromPosition(SKPoint point)
    {
        return GetTextIndexFromPoint(ClampTextInteractionPoint(point));
    }

    public bool CanCopy()
    {
        return SelectionLength > 0;
    }

    public bool CanCut()
    {
        return !_readOnly && SelectionLength > 0;
    }

    public bool CanPaste()
    {
        if (_readOnly)
            return false;

        if (!ClipboardHelper.TryGetText(out var clipboardText))
            return false;

        var sanitized = SanitizeInsertedText(clipboardText);
        return sanitized.Length > 0 || SelectionLength > 0;
    }

    public bool CanDeleteSelection()
    {
        return !_readOnly && SelectionLength > 0;
    }

    public bool CanClearText()
    {
        return !_readOnly && Text.Length > 0;
    }

    public bool CanSelectAllText()
    {
        return Text.Length > 0 && (SelectionStart != 0 || SelectionLength != Text.Length);
    }

    public bool CopySelection()
    {
        return CanCopy() && ClipboardHelper.TrySetText(SelectedText);
    }

    public bool CutSelection()
    {
        if (!CanCut() || !ClipboardHelper.TrySetText(SelectedText))
            return false;

        DeleteSelection();
        return true;
    }

    public bool PasteFromClipboard()
    {
        if (!CanPaste() || !ClipboardHelper.TryGetText(out var clipboardText))
            return false;

        var sanitized = SanitizeInsertedText(clipboardText);
        if (sanitized.Length == 0 && SelectionLength == 0)
            return false;

        ReplaceSelection(clipboardText);
        return true;
    }

    public bool DeleteSelection()
    {
        if (!CanDeleteSelection())
            return false;

        ReplaceSelection(string.Empty);
        return true;
    }

    public bool ClearText()
    {
        if (!CanClearText())
            return false;

        SelectAll();
        ReplaceSelection(string.Empty);
        return true;
    }

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        EnsureLayoutFont();

        var renderText = GetDisplayText();
        var textSample = string.IsNullOrEmpty(renderText) ? PlaceholderText : renderText;
        if (string.IsNullOrEmpty(textSample))
            textSample = " ";

        var measurementSize = proposedSize;
        if (measurementSize.Width <= 1f)
            measurementSize.Width = short.MaxValue;
        if (measurementSize.Height <= 1f)
            measurementSize.Height = short.MaxValue;

        var wrapMode = _multiline ? WrapMode : TextWrap.None;
        var textSize = TextRenderer.MeasureText(
            textSample,
            _layoutFont,
            measurementSize,
            new TextRenderOptions
            {
                MaxWidth = measurementSize.Width,
                MaxHeight = measurementSize.Height,
                Wrap = wrapMode,
                Trimming = TextTrimming.None,
            });

        var desiredWidth = textSize.Width + Padding.Horizontal + Border.Left + Border.Right;
        var contentHeight = textSize.Height + Padding.Vertical + Border.Top + Border.Bottom;
        var desiredHeight = _multiline
            ? Math.Max(DefaultMultilineHeight, contentHeight)
            : Math.Max(DefaultHeight, contentHeight);

        desiredWidth = Math.Max(desiredWidth, MinimumSize.Width > 0 ? MinimumSize.Width : DefaultMinWidth);
        desiredHeight = Math.Max(desiredHeight, MinimumSize.Height > 0 ? MinimumSize.Height : DefaultMinHeight);

        if (MaximumSize.Width > 0)
            desiredWidth = Math.Min(desiredWidth, MaximumSize.Width);
        if (MaximumSize.Height > 0)
            desiredHeight = Math.Min(desiredHeight, MaximumSize.Height);

        return new SKSize((float)Math.Ceiling(desiredWidth), (float)Math.Ceiling(desiredHeight));
    }

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        EnsureTextLayout();

        var viewport = GetTextViewport();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
            return;

        UpdatePaintResources();

        var scrollX = GetHorizontalScrollOffset();
        var scrollY = GetVerticalScrollOffset();
        var saveCount = canvas.Save();
        canvas.ClipRect(viewport);
        canvas.Translate(viewport.Left - scrollX, viewport.Top - scrollY);

        DrawSelection(canvas);
        DrawTextContent(canvas);
        DrawCaret(canvas);

        canvas.RestoreToCount(saveCount);
    }

    public override void  OnMouseDown(MouseEventArgs e)
    {
        if (!IsTextInteractionPoint(e.Location))
        {
            base.OnMouseDown(e);
            return;
        }

        RaiseMouseDown(e);

        if (!Enabled || !Visible)
            return;

        var hadFocus = Focused;
        Focus();

        var index = GetTextIndexFromPoint(ClampTextInteractionPoint(e.Location));
        if (e.Button == MouseButtons.Left)
        {
            var extendSelection = (ModifierKeys & Keys.Shift) == Keys.Shift;
            var anchor = extendSelection ? _selectionAnchor : index;
            var selectionChanged = _selectionAnchor != anchor || _selectionCaret != index;
            SetSelectionCore(anchor, index, preservePreferredCaretX: false, ensureVisible: false);
            _mouseSelecting = true;
            GetParentWindow()?.SetMouseCapture(this);
            if (selectionChanged && hadFocus)
                ResetCaretBlink();
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            var inCurrentSelection = SelectionLength > 0 && index >= SelectionStart && index <= SelectionStart + SelectionLength;
            var selectionChanged = false;
            if (!inCurrentSelection)
            {
                selectionChanged = _selectionAnchor != index || _selectionCaret != index;
                SetSelectionCore(index, index, preservePreferredCaretX: false, ensureVisible: false);
            }

            if (selectionChanged && hadFocus)
                ResetCaretBlink();
            var contextMenu = ContextMenuStrip;
            contextMenu?.Show(this, PointToScreen(e.Location));
            if (contextMenu != null)
                GetParentWindow()?.UpdateCursor(contextMenu);
        }
    }

    public override void  OnMouseMove(MouseEventArgs e)
    {
        if (!_mouseSelecting)
        {
            base.OnMouseMove(e);
            return;
        }

        var viewport = GetTextViewport();
        var clampedPoint = ClampTextInteractionPoint(e.Location);
        var index = GetTextIndexFromPoint(clampedPoint);
        var selectionChanged = index != _selectionCaret;
        if (!selectionChanged && viewport.Contains(e.Location))
            return;

        SetSelectionCore(_selectionAnchor, index, preservePreferredCaretX: false, ensureVisible: true);
        if (selectionChanged)
            ResetCaretBlink();
    }

    public override void  OnMouseUp(MouseEventArgs e)
    {
        if (!_mouseSelecting && !IsTextInteractionPoint(e.Location))
        {
            base.OnMouseUp(e);
            return;
        }

        RaiseMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        _mouseSelecting = false;
        GetParentWindow()?.ReleaseMouseCapture(this);
        Focus();
    }

    public override void  OnMouseClick(MouseEventArgs e)
    {
        if (!IsTextInteractionPoint(e.Location))
        {
            base.OnMouseClick(e);
            return;
        }

        RaiseMouseClick(e);

        if (e.Button == MouseButtons.Left)
            OnClick(EventArgs.Empty);

        if (!Enabled || !Visible)
            return;

        Focus();
    }

    public override void  OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (!Enabled || !Visible || e.Button != MouseButtons.Left)
            return;

        if (!IsTextInteractionPoint(e.Location))
            return;

        var selectionAnchor = _selectionAnchor;
        var selectionCaret = _selectionCaret;
        SelectWordAt(GetTextIndexFromPoint(ClampTextInteractionPoint(e.Location)));
        if (selectionAnchor != _selectionAnchor || selectionCaret != _selectionCaret)
            ResetCaretBlink();
    }

    public override void  OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        ResetCaretBlink();
    }

    public override void  OnLostFocus(EventArgs e)
    {
        _mouseSelecting = false;
        GetParentWindow()?.ReleaseMouseCapture(this);
        StopCaretBlink();
        base.OnLostFocus(e);
        Invalidate();
    }

    public override void  OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ClampSelection();
        InvalidateTextLayout();
        EnsureCaretVisible();
    }

    public override void  OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        InvalidateCachedLayoutFont();
        InvalidateTextLayout();
    }

    public override void  OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        InvalidateTextLayout();
    }

    public override void  OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateTextLayout();
    }

    public override void  OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && Focused)
            ResetCaretBlink();
        else if (!Visible)
            StopCaretBlink();
    }

    public override void  OnDpiChanged(float newDpi, float oldDpi)
    {
        base.OnDpiChanged(newDpi, oldDpi);
        InvalidateCachedLayoutFont();
        InvalidateTextLayout();
    }

    public override void  OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || !Enabled)
            return;

        if (TryHandleTextZoomShortcut(e))
            return;

        var extendSelection = e.Shift;
        var moveByWord = e.Control;

        switch (e.KeyCode)
        {
            case Keys.Left:
                MoveCaretHorizontal(-1, extendSelection, moveByWord);
                e.Handled = true;
                return;

            case Keys.Right:
                MoveCaretHorizontal(1, extendSelection, moveByWord);
                e.Handled = true;
                return;

            case Keys.Up:
                if (_multiline)
                {
                    MoveCaretVertical(-1, extendSelection);
                    e.Handled = true;
                }
                return;

            case Keys.Down:
                if (_multiline)
                {
                    MoveCaretVertical(1, extendSelection);
                    e.Handled = true;
                }
                return;

            case Keys.Home:
                MoveCaretToBoundary(toStart: true, extendSelection, e.Control || !_multiline);
                e.Handled = true;
                return;

            case Keys.End:
                MoveCaretToBoundary(toStart: false, extendSelection, e.Control || !_multiline);
                e.Handled = true;
                return;

            case Keys.PageUp:
                if (_multiline)
                {
                    MoveCaretVertical(-GetPageLineDelta(), extendSelection);
                    e.Handled = true;
                }
                return;

            case Keys.PageDown:
                if (_multiline)
                {
                    MoveCaretVertical(GetPageLineDelta(), extendSelection);
                    e.Handled = true;
                }
                return;

            case Keys.Back:
                if (!_readOnly)
                {
                    DeleteBackward(moveByWord);
                    e.Handled = true;
                }
                return;

            case Keys.Delete:
                if (!_readOnly)
                {
                    DeleteForward(moveByWord);
                    e.Handled = true;
                }
                return;

            case Keys.C when e.Control:
                e.Handled = CopySelection();
                return;

            case Keys.X when e.Control:
                e.Handled = CutSelection();
                return;

            case Keys.V when e.Control:
                e.Handled = PasteFromClipboard();
                return;

            case Keys.Enter:
                if (_multiline && _acceptsReturn && !_readOnly)
                {
                    ReplaceSelection("\n");
                    e.Handled = true;
                }
                return;

            case Keys.Tab:
                if (_acceptsTab && !_readOnly)
                {
                    ReplaceSelection("\t");
                    e.Handled = true;
                }
                return;

            case Keys.A when e.Control:
                SelectAll();
                e.Handled = true;
                return;
        }
    }

    public override void  OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);

        if (e.Handled || !Enabled || _readOnly)
            return;

        if (e.Control && !e.Alt)
        {
            e.Handled = true;
            return;
        }

        var keyChar = e.KeyChar;
        if (keyChar == '\r' || keyChar == '\n' || keyChar == '\t' || keyChar == '\b' || keyChar == '\0' || char.IsControl(keyChar))
            return;

        ReplaceSelection(keyChar.ToString());
        e.Handled = true;
    }

    public override void  OnMouseWheel(MouseEventArgs e)
    {
        if (_multiline && (ModifierKeys & Keys.Control) == Keys.Control && !e.IsHorizontalWheel)
        {
            var direction = Math.Sign(e.Delta);
            if (direction != 0)
                TryAdjustTextZoom(direction);

            e.Handled = true;
            return;
        }

        base.OnMouseWheel(e);
    }

    protected override float GetMouseWheelScrollStep(ScrollBar scrollBar)
    {
        EnsureTextLayout();

        if (scrollBar.IsVertical)
            return Math.Max(8f, _lineHeight);

        var horizontalStep = _layoutFont?.Size ?? 12f;
        return Math.Max(10f, horizontalStep * 1.5f);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _caretBlinkAnimation.OnAnimationProgress -= HandleCaretBlinkAnimationProgress;
            _caretBlinkAnimation.OnAnimationFinished -= HandleCaretBlinkAnimationFinished;
            _caretBlinkAnimation.Stop();
            _caretBlinkAnimation.Dispose();
            _defaultContextMenu.Opening -= HandleDefaultContextMenuOpening;
            _defaultContextMenu.Dispose();
            InvalidateCachedLayoutFont();
            _textPaint.Dispose();
            _placeholderPaint.Dispose();
            _selectionPaint.Dispose();
            _caretPaint.Dispose();
            _caretFillPaint.Dispose();
            _focusCuePaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HandleCaretBlinkAnimationProgress(object state)
    {
        if (!CanAnimateCaret())
        {
            StopCaretBlink();
            return;
        }

        var progress = (float)_caretBlinkAnimation.GetProgress();
        _caretOpacity = _caretBlinkAnimation.Direction == AnimationDirection.Out
            ? Lerp(1f, CaretMinimumVisibleOpacity, progress)
            : Lerp(CaretMinimumVisibleOpacity, 1f, progress);
        Invalidate();
    }

    private void HandleCaretBlinkAnimationFinished(object state)
    {
        if (!CanAnimateCaret())
        {
            StopCaretBlink();
            return;
        }

        var nextDirection = _caretBlinkAnimation.Direction == AnimationDirection.Out
            ? AnimationDirection.In
            : AnimationDirection.Out;

        _caretBlinkAnimation.StartNewAnimation(nextDirection);
    }

    private void UpdatePaintResources()
    {
        _textPaint.Color = Enabled ? ForeColor : ForeColor.WithAlpha(138);
        _placeholderPaint.Color = Enabled
            ? (Focused ? ForeColor.WithAlpha(124) : ForeColor.WithAlpha(96))
            : ForeColor.WithAlpha(82);
        _selectionPaint.Color = Focused
            ? ColorScheme.Primary.WithAlpha(68)
            : ColorScheme.Primary.WithAlpha(38);
        var caretStrokeColor = _textPaint.Color;
        var caretFillColor = ApplyAlpha(_textPaint.Color, 0.24f);
        _caretPaint.Color = ApplyAlpha(caretStrokeColor, _caretOpacity);
        _caretPaint.StrokeWidth = Math.Max(1f, CaretThickness * ScaleFactor);
        _caretFillPaint.Color = ApplyAlpha(caretFillColor, _caretOpacity);
        _focusCuePaint.Color = Focused ? ColorScheme.Primary.WithAlpha(214) : SKColors.Transparent;
        _focusCuePaint.StrokeWidth = Math.Max(2.1f, 2.5f * ScaleFactor);
    }

    private void EnsureTextLayout()
    {
        if (!_layoutDirty)
            return;

        EnsureLayoutFont();

        var initialViewportWidth = Math.Max(1f, GetTextViewport().Width);
        BuildTextLayout(initialViewportWidth);
        UpdateScrollMetrics();

        if (_multiline && WrapMode != TextWrap.None)
        {
            var refinedViewportWidth = Math.Max(1f, GetTextViewport().Width);
            if (Math.Abs(refinedViewportWidth - initialViewportWidth) > 0.5f)
            {
                BuildTextLayout(refinedViewportWidth);
                UpdateScrollMetrics();
            }
        }

        ClampSelection();
        _layoutDirty = false;
    }

    private void EnsureLayoutFont()
    {
        var sourceFont = Font;
        var scale = ScaleFactor;

        if (_layoutFont == null
            || _layoutFontSource == null
            || !_layoutFontSource.FontEquals(sourceFont)
            || Math.Abs(_layoutFontScale - scale) > 0.001f
            || Math.Abs(_layoutTextZoomFactor - _textZoomFactor) > 0.001f)
        {
            InvalidateCachedLayoutFont();
            using var zoomedFont = sourceFont.CloneFont();
            zoomedFont.Size = Math.Max(1f, zoomedFont.Size * _textZoomFactor);
            _layoutFont = CreateRenderFont(zoomedFont);
            _layoutFontSource = sourceFont.CloneFont();
            _layoutFontScale = scale;
            _layoutTextZoomFactor = _textZoomFactor;
        }

        var metrics = _layoutFont.Metrics;
        var rawLineHeight = metrics.Descent - metrics.Ascent + Math.Max(0f, metrics.Leading);
        _baselineOffset = -metrics.Ascent;
        _lineHeight = Math.Max(16f * scale, rawLineHeight * 1.18f);
    }

    private void BuildTextLayout(float viewportWidth)
    {
        _lines.Clear();

        var text = GetDisplayText();
        if (text.Length == 0)
        {
            _lines.Add(new TextLineLayout(0, 0, 0, 0f));
            _contentWidth = 0f;
            _contentHeight = _lineHeight;
            return;
        }

        var wrapEnabled = _multiline && WrapMode != TextWrap.None;
        var wrapWidth = wrapEnabled ? Math.Max(1f, viewportWidth - 2f * ScaleFactor) : float.MaxValue;
        var paragraphStart = 0;

        while (paragraphStart < text.Length)
        {
            var paragraphEnd = text.IndexOf('\n', paragraphStart);
            var hasBreak = paragraphEnd >= 0;
            if (!hasBreak)
                paragraphEnd = text.Length;

            AddParagraphLines(text, paragraphStart, paragraphEnd, hasBreak ? 1 : 0, wrapEnabled, wrapWidth);

            if (!hasBreak)
                break;

            paragraphStart = paragraphEnd + 1;
            if (paragraphStart == text.Length)
                _lines.Add(new TextLineLayout(text.Length, 0, 0, 0f));
        }

        if (_lines.Count == 0)
            _lines.Add(new TextLineLayout(0, 0, 0, 0f));

        _contentWidth = 0f;
        for (var i = 0; i < _lines.Count; i++)
            _contentWidth = Math.Max(_contentWidth, _lines[i].Width);

        if (_multiline && WrapMode != TextWrap.None)
            _contentWidth = Math.Max(_contentWidth, Math.Max(1f, viewportWidth));
        else
            _contentWidth += 4f * ScaleFactor;

        _contentHeight = Math.Max(_lineHeight, _lines.Count * _lineHeight);
    }

    private void AddParagraphLines(string text, int paragraphStart, int paragraphEnd, int breakLength, bool wrapEnabled, float wrapWidth)
    {
        if (!wrapEnabled || paragraphStart == paragraphEnd)
        {
            AddLine(paragraphStart, paragraphEnd - paragraphStart, breakLength, text);
            return;
        }

        var lineStart = paragraphStart;
        var index = paragraphStart;
        var lastBreakIndex = -1;

        while (index < paragraphEnd)
        {
            var current = text[index];
            if (WrapMode == TextWrap.WordWrap && char.IsWhiteSpace(current) && current != '\n' && current != '\r')
                lastBreakIndex = index;

            var testWidth = MeasureTextWidth(text, lineStart, index - lineStart + 1);
            if (testWidth > wrapWidth && index > lineStart)
            {
                var wrapEnd = index;
                var nextLineStart = index;

                if (WrapMode == TextWrap.WordWrap && lastBreakIndex >= lineStart)
                {
                    wrapEnd = lastBreakIndex + 1;
                    nextLineStart = lastBreakIndex + 1;
                }

                if (wrapEnd <= lineStart)
                {
                    wrapEnd = index;
                    nextLineStart = index;
                }

                AddLine(lineStart, wrapEnd - lineStart, 0, text);
                lineStart = nextLineStart;
                lastBreakIndex = -1;
                continue;
            }

            index++;
        }

        AddLine(lineStart, paragraphEnd - lineStart, breakLength, text);
    }

    private void AddLine(int start, int length, int breakLength, string text)
    {
        var safeLength = Math.Max(0, length);
        _lines.Add(new TextLineLayout(start, safeLength, breakLength, MeasureTextWidth(text, start, safeLength)));
    }

    private float MeasureTextWidth(string text, int start, int length)
    {
        if (_layoutFont == null || length <= 0)
            return 0f;

        return _layoutFont.MeasureText(text.Substring(start, length));
    }

    private void UpdateScrollMetrics()
    {
        var viewport = GetTextViewport();
        var leftInset = viewport.Left;
        var topInset = viewport.Top;
        var rightInset = Width - viewport.Right;
        var bottomInset = Height - viewport.Bottom;

        var minWidth = (float)Math.Ceiling(leftInset + _contentWidth + rightInset);
        var minHeight = _multiline
            ? (float)Math.Ceiling(topInset + _contentHeight + bottomInset)
            : Height;

        if (!_multiline)
            minWidth = Math.Max(Width, minWidth);

        var minSize = new SKSize(minWidth, minHeight);

        if (AutoScrollMinSize != minSize)
            AutoScrollMinSize = minSize;

        UpdateScrollBars();

        var refinedViewport = GetTextViewport();
        if (_vScrollBar != null)
        {
            _vScrollBar.SmallChange = Math.Max(8f, _lineHeight);
            _vScrollBar.LargeChange = Math.Max(_lineHeight * 3f, refinedViewport.Height * 0.85f);
        }

        if (_hScrollBar != null)
        {
            var horizontalStep = _layoutFont?.Size ?? 12f;
            _hScrollBar.SmallChange = Math.Max(10f, horizontalStep * 1.5f);
            _hScrollBar.LargeChange = Math.Max(28f, refinedViewport.Width * 0.8f);
        }
    }

    private bool TryHandleTextZoomShortcut(KeyEventArgs e)
    {
        if (!_multiline || !e.Control)
            return false;

        switch (e.KeyCode)
        {
            case Keys.OemPlus:
            case Keys.Add:
                TryAdjustTextZoom(1);
                e.Handled = true;
                return true;

            case Keys.OemMinus:
            case Keys.Subtract:
                TryAdjustTextZoom(-1);
                e.Handled = true;
                return true;

            case Keys.D0:
                SetTextZoomFactor(DefaultTextZoomFactor);
                e.Handled = true;
                return true;
        }

        return false;
    }

    private bool TryAdjustTextZoom(int direction)
    {
        if (direction == 0)
            return false;

        return SetTextZoomFactor(_textZoomFactor + _textZoomStep * direction);
    }

    private bool SetTextZoomFactor(float value)
    {
        var clamped = Math.Clamp(value, MinimumTextZoomFactor, MaximumTextZoomFactor);
        clamped = (float)Math.Round(clamped, 2, MidpointRounding.AwayFromZero);

        if (Math.Abs(_textZoomFactor - clamped) <= 0.001f)
            return false;

        _textZoomFactor = clamped;
        InvalidateCachedLayoutFont();
        InvalidateTextLayout();
        EnsureCaretVisible();
        ResetCaretBlink();
        TextZoomFactorChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    protected SKRect GetTextViewport()
    {
        var viewport = DisplayRectangle;
        var gap = 4f * ScaleFactor;

        if (_vScrollBar?.Visible == true)
            viewport.Right = Math.Max(viewport.Left, viewport.Right - _vScrollBar.Thickness - gap);

        if (_hScrollBar?.Visible == true)
            viewport.Bottom = Math.Max(viewport.Top, viewport.Bottom - _hScrollBar.Thickness - gap);

        return viewport;
    }

    private bool IsTextInteractionPoint(SKPoint point)
    {
        if (point.X < 0f || point.Y < 0f || point.X > Width || point.Y > Height)
            return false;

        if (_vScrollBar?.Visible == true && _vScrollBar.Bounds.Contains(point))
            return false;

        if (_hScrollBar?.Visible == true && _hScrollBar.Bounds.Contains(point))
            return false;

        return true;
    }

    private SKPoint ClampTextInteractionPoint(SKPoint point)
    {
        var viewport = GetTextViewport();
        return new SKPoint(
            Math.Clamp(point.X, viewport.Left, viewport.Right),
            Math.Clamp(point.Y, viewport.Top, viewport.Bottom));
    }

    protected float GetHorizontalScrollOffset()
    {
        return _hScrollBar?.Visible == true ? _hScrollBar.DisplayValue : 0f;
    }

    protected float GetVerticalScrollOffset()
    {
        return _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;
    }

    protected float GetContentTopInset(SKRect viewport)
    {
        if (_multiline)
            return 0f;

        return Math.Max(0f, (viewport.Height - _lineHeight) * 0.5f);
    }

    private void DrawSelection(SKCanvas canvas)
    {
        if (SelectionLength == 0 || Text.Length == 0)
            return;

        var selectionStart = SelectionStart;
        var selectionEnd = selectionStart + SelectionLength;
        var verticalScroll = GetVerticalScrollOffset();
        var viewport = GetTextViewport();
        var topInset = GetContentTopInset(viewport);
        var firstLine = Math.Max(0, (int)Math.Floor(verticalScroll / Math.Max(1f, _lineHeight)) - 1);
        var lastLine = Math.Min(_lines.Count - 1, (int)Math.Ceiling((verticalScroll + viewport.Height) / Math.Max(1f, _lineHeight)) + 1);

        for (var i = firstLine; i <= lastLine; i++)
        {
            var line = _lines[i];
            var lineSelectionStart = Math.Max(selectionStart, line.Start);
            var lineSelectionEnd = Math.Min(selectionEnd, line.Start + line.Length);
            if (lineSelectionEnd <= lineSelectionStart)
                continue;

            var lineText = GetLineText(line);
            var localStart = lineSelectionStart - line.Start;
            var localEnd = lineSelectionEnd - line.Start;
            var startX = MeasureLocalX(lineText, localStart);
            var endX = MeasureLocalX(lineText, localEnd);
            var top = topInset + i * _lineHeight + SelectionInsetY;
            var rect = SKRect.Create(startX, top, Math.Max(1f, endX - startX), Math.Max(1f, _lineHeight - SelectionInsetY * 2f));
            var radius = Math.Min(6f * ScaleFactor, rect.Height * 0.45f);
            canvas.DrawRoundRect(rect, radius, radius, _selectionPaint);
        }
    }

    private void DrawTextContent(SKCanvas canvas)
    {
        var text = GetDisplayText();
        if (text.Length == 0)
        {
            if (!string.IsNullOrEmpty(_placeholderText))
                DrawLineText(canvas, _placeholderText, 0f, _baselineOffset, _placeholderPaint);
            return;
        }

        var verticalScroll = GetVerticalScrollOffset();
        var viewport = GetTextViewport();
        var topInset = GetContentTopInset(viewport);
        var firstLine = Math.Max(0, (int)Math.Floor(verticalScroll / Math.Max(1f, _lineHeight)) - 1);
        var lastLine = Math.Min(_lines.Count - 1, (int)Math.Ceiling((verticalScroll + viewport.Height) / Math.Max(1f, _lineHeight)) + 1);

        for (var i = firstLine; i <= lastLine; i++)
        {
            var line = _lines[i];
            var y = topInset + i * _lineHeight + _baselineOffset;
            DrawLineText(canvas, GetLineText(line), 0f, y, _textPaint);
        }
    }

    private void DrawCaret(SKCanvas canvas)
    {
        if (_caretOpacity <= 0.01f || !Focused || !Enabled)
            return;

        EnsureTextLayout();

        var caretRect = GetCaretRect();
        switch (_caretMode)
        {
            case TextBoxCaretMode.Block:
                DrawBlockCaret(canvas, caretRect);
                return;

            case TextBoxCaretMode.Underline:
                DrawUnderlineCaret(canvas, caretRect);
                return;

            case TextBoxCaretMode.DoubleBar:
                DrawDoubleBarCaret(canvas, caretRect);
                return;

            case TextBoxCaretMode.HollowBlock:
                DrawHollowBlockCaret(canvas, caretRect);
                return;

            case TextBoxCaretMode.Dot:
                DrawDotCaret(canvas, caretRect);
                return;

            default:
                canvas.DrawLine(caretRect.Left, caretRect.Top, caretRect.Left, caretRect.Bottom, _caretPaint);
                return;
        }
    }

    private void DrawLineText(SKCanvas canvas, string text, float x, float y, SKPaint paint)
    {
        if (_layoutFont == null)
            return;

        TextRenderer.DrawText(canvas, text, x, y, SKTextAlign.Left, _layoutFont, paint, new TextRenderOptions());
    }

    private SKRect GetCaretRect()
    {
        EnsureTextLayout();
        var lineIndex = FindLineIndexForCaret(_selectionCaret);
        var line = _lines[lineIndex];
        var lineText = GetLineText(line);
        var localIndex = Math.Clamp(_selectionCaret - line.Start, 0, line.Length);
        var x = MeasureLocalX(lineText, localIndex);
        var viewport = GetTextViewport();
        var topInset = GetContentTopInset(viewport);
        var baseline = topInset + lineIndex * _lineHeight + _baselineOffset;

        if (_layoutFont != null)
        {
            var metrics = _layoutFont.Metrics;
            var top = baseline + metrics.Ascent;
            var bottom = baseline + metrics.Descent;
            var height = Math.Max(1f, bottom - top);
            return SKRect.Create(x, top, Math.Max(1f, CaretThickness * ScaleFactor), height);
        }

        var fallbackTop = topInset + lineIndex * _lineHeight + SelectionInsetY;
        return SKRect.Create(x, fallbackTop, Math.Max(1f, CaretThickness * ScaleFactor), Math.Max(1f, _lineHeight - SelectionInsetY * 2f));
    }

    private float MeasureLocalX(string lineText, int length)
    {
        if (_layoutFont == null || length <= 0 || string.IsNullOrEmpty(lineText))
            return 0f;

        if (length >= lineText.Length)
            return _layoutFont.MeasureText(lineText);

        return _layoutFont.MeasureText(lineText.Substring(0, length));
    }

    private void DrawBlockCaret(SKCanvas canvas, SKRect caretRect)
    {
        var blockRect = ResolveBlockCaretRect(caretRect);
        var radius = Math.Min(6f * ScaleFactor, Math.Min(blockRect.Width, blockRect.Height) * 0.28f);
        canvas.DrawRoundRect(blockRect, radius, radius, _caretFillPaint);
        canvas.DrawRoundRect(blockRect, radius, radius, _caretPaint);
    }

    private void DrawUnderlineCaret(SKCanvas canvas, SKRect caretRect)
    {
        var underlineRect = ResolveUnderlineCaretRect(caretRect);
        canvas.DrawLine(underlineRect.Left, underlineRect.Bottom, underlineRect.Right, underlineRect.Bottom, _caretPaint);
    }

    private void DrawDoubleBarCaret(SKCanvas canvas, SKRect caretRect)
    {
        var secondX = caretRect.Left + Math.Max(_caretPaint.StrokeWidth * 2.2f, 3f * ScaleFactor);
        canvas.DrawLine(caretRect.Left, caretRect.Top, caretRect.Left, caretRect.Bottom, _caretPaint);
        canvas.DrawLine(secondX, caretRect.Top, secondX, caretRect.Bottom, _caretPaint);
    }

    private void DrawHollowBlockCaret(SKCanvas canvas, SKRect caretRect)
    {
        var blockRect = ResolveBlockCaretRect(caretRect);
        var radius = Math.Min(6f * ScaleFactor, Math.Min(blockRect.Width, blockRect.Height) * 0.28f);
        canvas.DrawRoundRect(blockRect, radius, radius, _caretPaint);
    }

    private void DrawDotCaret(SKCanvas canvas, SKRect caretRect)
    {
        var radius = Math.Max(2f * ScaleFactor, _caretPaint.StrokeWidth * 1.2f);
        var cx = caretRect.Left + Math.Max(radius, ResolveCaretBlockWidth() * 0.28f);
        var cy = caretRect.Bottom - radius;
        canvas.DrawCircle(cx, cy, radius, _caretFillPaint);
    }

    private SKRect ResolveBlockCaretRect(SKRect caretRect)
    {
        var blockWidth = ResolveCaretBlockWidth();
        return SKRect.Create(caretRect.Left, caretRect.Top, blockWidth, caretRect.Height);
    }

    private SKRect ResolveUnderlineCaretRect(SKRect caretRect)
    {
        var underlineWidth = Math.Max(caretRect.Width * 3f, ResolveCaretBlockWidth() * 0.72f);
        var y = caretRect.Bottom - Math.Max(1f, _caretPaint.StrokeWidth * 0.5f);
        return SKRect.Create(caretRect.Left, y - _caretPaint.StrokeWidth, underlineWidth, _caretPaint.StrokeWidth);
    }

    private float ResolveCaretBlockWidth()
    {
        EnsureTextLayout();

        if (_layoutFont == null)
            return Math.Max(6f * ScaleFactor, CaretThickness * ScaleFactor * 3f);

        var lineIndex = FindLineIndexForCaret(_selectionCaret);
        var line = _lines[lineIndex];
        var localIndex = Math.Clamp(_selectionCaret - line.Start, 0, line.Length);
        var lineText = GetLineText(line);

        float width;
        if (localIndex < line.Length)
        {
            width = _layoutFont.MeasureText(lineText.Substring(localIndex, 1));
        }
        else
        {
            width = _layoutFont.MeasureText(" ");
        }

        return Math.Max(6f * ScaleFactor, width);
    }

    private int GetTextIndexFromPoint(SKPoint point)
    {
        EnsureTextLayout();

        var viewport = GetTextViewport();
        var localX = point.X - viewport.Left + GetHorizontalScrollOffset();
        var localY = point.Y - viewport.Top + GetVerticalScrollOffset() - GetContentTopInset(viewport);
        var lineIndex = Math.Clamp((int)Math.Floor(localY / Math.Max(1f, _lineHeight)), 0, _lines.Count - 1);
        var line = _lines[lineIndex];

        if (localX <= 0f || line.Length == 0)
            return line.Start;

        return line.Start + GetClosestColumn(line, localX);
    }

    private void MoveCaretHorizontal(int direction, bool extendSelection, bool moveByWord)
    {
        int target;
        if (!extendSelection && SelectionLength > 0)
        {
            target = direction < 0 ? SelectionStart : SelectionStart + SelectionLength;
        }
        else if (moveByWord)
        {
            target = direction < 0 ? FindPreviousWordBoundary(_selectionCaret) : FindNextWordBoundary(_selectionCaret);
        }
        else
        {
            target = ClampCaretIndex(_selectionCaret + direction);
        }

        var anchor = extendSelection ? _selectionAnchor : target;
        SetSelectionCore(anchor, target, preservePreferredCaretX: false, ensureVisible: true);
        ResetCaretBlink();
    }

    private void MoveCaretVertical(int lineDelta, bool extendSelection)
    {
        EnsureTextLayout();

        var currentLineIndex = FindLineIndexForCaret(_selectionCaret);
        var currentLine = _lines[currentLineIndex];
        var currentX = _preferredCaretX >= 0f
            ? _preferredCaretX
            : MeasureLocalX(GetLineText(currentLine), Math.Clamp(_selectionCaret - currentLine.Start, 0, currentLine.Length));
        var targetLineIndex = Math.Clamp(currentLineIndex + lineDelta, 0, _lines.Count - 1);
        var targetLine = _lines[targetLineIndex];
        var targetColumn = GetClosestColumn(targetLine, currentX);
        var target = targetLine.Start + targetColumn;
        var anchor = extendSelection ? _selectionAnchor : target;

        SetSelectionCore(anchor, target, preservePreferredCaretX: true, ensureVisible: true);
        _preferredCaretX = currentX;
        ResetCaretBlink();
    }

    private void MoveCaretToBoundary(bool toStart, bool extendSelection, bool wholeText)
    {
        EnsureTextLayout();

        int target;
        if (wholeText)
        {
            target = toStart ? 0 : Text.Length;
        }
        else
        {
            var lineIndex = FindLineIndexForCaret(_selectionCaret);
            var line = _lines[lineIndex];
            target = toStart ? line.Start : line.Start + line.Length;
        }

        var anchor = extendSelection ? _selectionAnchor : target;
        SetSelectionCore(anchor, target, preservePreferredCaretX: false, ensureVisible: true);
        ResetCaretBlink();
    }

    private void DeleteBackward(bool deleteWord)
    {
        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        if (_selectionCaret <= 0)
            return;

        var start = deleteWord ? FindPreviousWordBoundary(_selectionCaret) : _selectionCaret - 1;
        RemoveRange(start, _selectionCaret - start);
    }

    private void DeleteForward(bool deleteWord)
    {
        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        if (_selectionCaret >= Text.Length)
            return;

        var end = deleteWord ? FindNextWordBoundary(_selectionCaret) : _selectionCaret + 1;
        RemoveRange(_selectionCaret, end - _selectionCaret);
    }

    private void RemoveRange(int start, int length)
    {
        if (length <= 0)
            return;

        var current = Text;
        var safeStart = ClampCaretIndex(start);
        var safeLength = Math.Min(length, current.Length - safeStart);
        if (safeLength <= 0)
            return;

        Text = current.Remove(safeStart, safeLength);
        SetSelectionCore(safeStart, safeStart, preservePreferredCaretX: false, ensureVisible: true);
        ResetCaretBlink();
    }

    private void ReplaceSelection(string replacement)
    {
        var sanitized = SanitizeInsertedText(replacement);
        if (sanitized.Length == 0 && SelectionLength == 0 && replacement.Length > 0)
            return;

        var current = Text;
        var selectionStart = SelectionStart;
        var selectionLength = SelectionLength;
        var updated = current.Remove(selectionStart, selectionLength).Insert(selectionStart, sanitized);
        Text = updated;
        var caret = selectionStart + sanitized.Length;
        SetSelectionCore(caret, caret, preservePreferredCaretX: false, ensureVisible: true);
        ResetCaretBlink();
    }

    private string SanitizeInsertedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (!_multiline)
            normalized = normalized.Replace('\n', ' ');
        if (!_acceptsTab)
            normalized = normalized.Replace("\t", "    ", StringComparison.Ordinal);

        if (_maxLength > 0)
        {
            var remaining = _maxLength - (Text.Length - SelectionLength);
            if (remaining <= 0)
                return string.Empty;

            if (normalized.Length > remaining)
                normalized = normalized[..remaining];
        }

        return normalized;
    }

    private string NormalizeTextForStorage(string? value)
    {
        var normalized = string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        if (!_multiline)
            normalized = normalized.Replace('\n', ' ');

        if (_maxLength > 0 && normalized.Length > _maxLength)
            normalized = normalized[.._maxLength];

        return normalized;
    }

    private void SelectWordAt(int index)
    {
        var text = Text;
        if (text.Length == 0)
        {
            SetSelectionCore(0, 0, preservePreferredCaretX: false, ensureVisible: false);
            return;
        }

        var pivot = Math.Clamp(index, 0, text.Length - 1);
        if (pivot > 0 && pivot == text.Length)
            pivot--;

        if (char.IsWhiteSpace(text[pivot]))
        {
            var start = pivot;
            var end = pivot;
            while (start > 0 && char.IsWhiteSpace(text[start - 1]) && text[start - 1] != '\n')
                start--;
            while (end < text.Length && char.IsWhiteSpace(text[end]) && text[end] != '\n')
                end++;
            SetSelectionCore(start, end, preservePreferredCaretX: false, ensureVisible: false);
            return;
        }

        var wordStart = pivot;
        var wordEnd = pivot + 1;
        while (wordStart > 0 && IsWordCharacter(text[wordStart - 1]) == IsWordCharacter(text[pivot]) && !char.IsWhiteSpace(text[wordStart - 1]))
            wordStart--;
        while (wordEnd < text.Length && IsWordCharacter(text[wordEnd]) == IsWordCharacter(text[pivot]) && !char.IsWhiteSpace(text[wordEnd]))
            wordEnd++;

        SetSelectionCore(wordStart, wordEnd, preservePreferredCaretX: false, ensureVisible: false);
    }

    private static bool IsWordCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private int FindPreviousWordBoundary(int index)
    {
        var text = Text;
        var current = Math.Clamp(index, 0, text.Length);
        if (current == 0)
            return 0;

        current--;
        while (current > 0 && char.IsWhiteSpace(text[current]))
            current--;
        while (current > 0 && !char.IsWhiteSpace(text[current - 1]))
            current--;
        return current;
    }

    private int FindNextWordBoundary(int index)
    {
        var text = Text;
        var current = Math.Clamp(index, 0, text.Length);
        while (current < text.Length && !char.IsWhiteSpace(text[current]))
            current++;
        while (current < text.Length && char.IsWhiteSpace(text[current]))
            current++;
        return current;
    }

    private int GetClosestColumn(TextLineLayout line, float targetX)
    {
        var lineText = GetLineText(line);
        if (line.Length == 0 || targetX <= 0f)
            return 0;

        var lowerColumn = 0;
        var upperColumn = line.Length;

        while (lowerColumn < upperColumn)
        {
            var midColumn = (lowerColumn + upperColumn + 1) / 2;
            var midX = MeasureLocalX(lineText, midColumn);
            if (midX <= targetX)
                lowerColumn = midColumn;
            else
                upperColumn = midColumn - 1;
        }

        var nextColumn = Math.Min(line.Length, lowerColumn + 1);
        if (nextColumn == lowerColumn)
            return lowerColumn;

        var lowerX = MeasureLocalX(lineText, lowerColumn);
        var nextX = MeasureLocalX(lineText, nextColumn);
        return Math.Abs(nextX - targetX) < Math.Abs(targetX - lowerX)
            ? nextColumn
            : lowerColumn;
    }

    private int GetPageLineDelta()
    {
        var viewport = GetTextViewport();
        return Math.Max(1, (int)Math.Floor(viewport.Height / Math.Max(1f, _lineHeight)) - 1);
    }

    private int FindLineIndexForCaret(int caretIndex)
    {
        var clampedCaret = ClampCaretIndex(caretIndex);
        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (clampedCaret <= line.Start + line.Length)
                return i;
        }

        return _lines.Count - 1;
    }

    private string GetLineText(TextLineLayout line)
    {
        if (line.Length <= 0)
            return string.Empty;

        var text = GetDisplayText();
        return text.Substring(line.Start, line.Length);
    }

    private void SetSelectionCore(int anchor, int caret, bool preservePreferredCaretX, bool ensureVisible)
    {
        var boundedAnchor = ClampCaretIndex(anchor);
        var boundedCaret = ClampCaretIndex(caret);
        if (_selectionAnchor == boundedAnchor && _selectionCaret == boundedCaret)
        {
            if (ensureVisible)
                EnsureCaretVisible();
            return;
        }

        _selectionAnchor = boundedAnchor;
        _selectionCaret = boundedCaret;
        if (!preservePreferredCaretX)
            _preferredCaretX = -1f;

        if (ensureVisible)
            EnsureCaretVisible();

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void EnsureCaretVisible()
    {
        EnsureTextLayout();

        var viewport = GetTextViewport();
        if (viewport.Width <= 0f || viewport.Height <= 0f)
            return;

        var topInset = GetContentTopInset(viewport);
        var caretRect = GetCaretRect();

        if (_hScrollBar != null && (_hScrollBar.Visible || _hScrollBar.Maximum > 0))
        {
            var targetX = _hScrollBar.Value;
            var visibleLeft = GetHorizontalScrollOffset();
            var visibleRight = visibleLeft + viewport.Width;
            if (caretRect.Left < visibleLeft)
                targetX = Math.Max(0f, caretRect.Left);
            else if (caretRect.Right > visibleRight)
                targetX = Math.Min(_hScrollBar.Maximum, caretRect.Right - viewport.Width + 2f * ScaleFactor);

            _hScrollBar.Value = targetX;
        }

        if (_vScrollBar != null && (_vScrollBar.Visible || _vScrollBar.Maximum > 0))
        {
            var targetY = _vScrollBar.Value;
            var visibleTop = GetVerticalScrollOffset();
            var visibleBottom = visibleTop + viewport.Height;
            var caretTop = Math.Max(0f, caretRect.Top - topInset);
            var caretBottom = Math.Max(caretTop, caretRect.Bottom - topInset);
            if (caretTop < visibleTop)
                targetY = Math.Max(0f, caretTop);
            else if (caretBottom > visibleBottom)
                targetY = Math.Min(_vScrollBar.Maximum, caretBottom - viewport.Height + 2f * ScaleFactor);

            _vScrollBar.Value = targetY;
        }
    }

    private void EnsureInputFocus()
    {
        var window = GetParentWindow();
        if (window != null)
        {
            if (!ReferenceEquals(window.FocusedElement, this) || !Focused)
                Focus();

            return;
        }

        if (!Focused)
            Focus();
    }

    private void ClampSelection()
    {
        _selectionAnchor = ClampCaretIndex(_selectionAnchor);
        _selectionCaret = ClampCaretIndex(_selectionCaret);
    }

    private int ClampCaretIndex(int index)
    {
        return Math.Clamp(index, 0, Text.Length);
    }

    protected void InvalidateTextLayout()
    {
        _layoutDirty = true;
        InvalidateMeasure();
        Invalidate();
    }

    private void InvalidateDisplayedText()
    {
        _displayTextDirty = true;
        InvalidateTextLayout();
    }

    private void ResetCaretBlink()
    {
        _caretOpacity = 1f;

        if (!CanAnimateCaret())
        {
            Invalidate();
            return;
        }

        UpdateCaretBlinkAnimationRate();
        _caretBlinkAnimation.StartNewAnimation(AnimationDirection.Out);
        Invalidate();
    }

    private void StopCaretBlink()
    {
        _caretBlinkAnimation.Stop();
        _caretOpacity = 0f;
        Invalidate();
    }

    private void DrawFocusCue(SKCanvas canvas)
    {
        if (!Focused || Width <= 0f || Height <= 0f)
            return;

        var inset = Math.Max(1f, 1.5f * ScaleFactor + Border.Left * 0.5f);
        var rect = SKRect.Create(
            inset,
            inset,
            Math.Max(1f, Width - inset * 2f),
            Math.Max(1f, Height - inset * 2f));
        var radius = Math.Max(6f * ScaleFactor, 7f * ScaleFactor);
        canvas.DrawRoundRect(rect, radius, radius, _focusCuePaint);
    }

    private bool CanAnimateCaret()
    {
        return Focused && Visible && Enabled && !IsDisposed && !Disposing;
    }

    private void UpdateCaretBlinkAnimationRate()
    {
        var blinkDuration = Math.Max(320d, SystemInformation.CaretBlinkTime);
        var increment = Math.Clamp(16d / blinkDuration, 0.005d, 1d);
        _caretBlinkAnimation.Increment = increment;
        _caretBlinkAnimation.SecondaryIncrement = increment;
    }

    private string GetDisplayText()
    {
        if (!_displayTextDirty)
            return _displayText;

        var source = base.Text;
        if (!_passwordMode || string.IsNullOrEmpty(source))
        {
            _displayText = source;
            _displayTextDirty = false;
            return _displayText;
        }

        var passwordChar = ResolvePasswordChar(_passwordChar);
        _displayText = string.Create(source.Length, (source, passwordChar), static (buffer, state) =>
        {
            for (var i = 0; i < state.source.Length; i++)
            {
                var current = state.source[i];
                buffer[i] = current == '\n' ? '\n' : state.passwordChar;
            }
        });

        _displayTextDirty = false;
        return _displayText;
    }

    private static char ResolvePasswordChar(char value)
    {
        return value == '\0' || value == '\n' || value == '\r' || char.IsControl(value)
            ? '*'
            : value;
    }

    private static SKColor ApplyAlpha(SKColor color, float opacity)
    {
        var alpha = (byte)Math.Clamp(Math.Round(color.Alpha * Math.Clamp(opacity, 0f, 1f)), 0d, 255d);
        return color.WithAlpha(alpha);
    }

    private void HandleDefaultContextMenuOpening(object? sender, CancelEventArgs e)
    {
        EnsureInputFocus();
        GetParentWindow()?.UpdateCursor(_defaultContextMenu);
        RefreshDefaultContextMenuState();
    }

    private void RefreshDefaultContextMenuState()
    {
        _cutMenuItem.Enabled = CanCut();
        _copyMenuItem.Enabled = CanCopy();
        _pasteMenuItem.Enabled = CanPaste();
        _deleteMenuItem.Enabled = CanDeleteSelection();
        _clearMenuItem.Enabled = CanClearText();
        _selectAllMenuItem.Enabled = CanSelectAllText();
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * Math.Clamp(amount, 0f, 1f);
    }

    private void InvalidateCachedLayoutFont()
    {
        _layoutFont?.Dispose();
        _layoutFont = null;
        _layoutFontSource?.Dispose();
        _layoutFontSource = null;
        _layoutFontScale = 0f;
        _layoutTextZoomFactor = DefaultTextZoomFactor;
    }

    private readonly struct TextLineLayout
    {
        public TextLineLayout(int start, int length, int breakLength, float width)
        {
            Start = start;
            Length = length;
            BreakLength = breakLength;
            Width = width;
        }

        public int Start { get; }

        public int Length { get; }

        public int BreakLength { get; }

        public float Width { get; }
    }
}