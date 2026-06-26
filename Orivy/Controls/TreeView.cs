using Orivy.Animation;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Orivy.Controls;

public class TreeView : ElementBase
{
    private sealed class NodeState
    {
        public AnimationManager Animation { get; } = new(true)
        {
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true,
            Increment = 16d / 150d,
            SecondaryIncrement = 16d / 130d
        };
    }

    private readonly Dictionary<TreeNode, NodeState> _states = new();
    private readonly List<VisibleNode> _visibleNodes = new();
    private readonly SKPaint _rowPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _textPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _linePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPath _chevronPath = new();

    private TreeNode? _selectedNode;
    private TreeNode? _hoveredNode;
    private float _itemHeight = 30f;
    private float _indent = 18f;
    private bool _horizontalScrollEnabled;

    public TreeView()
    {
        AutoScroll = true;
        CanSelect = true;
        TabStop = true;
        Size = new SKSize(220, 260);
        Padding = new Thickness(6);
        Radius = new Radius(10);
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        Border = new Thickness(1);
        BorderColor = ColorScheme.Outline.WithAlpha(90);
        ColorScheme.ThemeChanged += OnTreeThemeChanged;
    }

    public List<TreeNode> Nodes { get; } = new();

    [DefaultValue(30f)]
    public float ItemHeight
    {
        get => _itemHeight;
        set
        {
            var normalized = Math.Max(20f, value);
            if (Math.Abs(_itemHeight - normalized) < 0.001f)
                return;

            _itemHeight = normalized;
            Invalidate();
        }
    }

    [DefaultValue(18f)]
    public float Indent
    {
        get => _indent;
        set
        {
            var normalized = Math.Max(8f, value);
            if (Math.Abs(_indent - normalized) < 0.001f)
                return;

            _indent = normalized;
            Invalidate();
        }
    }

    public TreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value))
                return;

            if (_selectedNode != null)
                _selectedNode.Selected = false;

            _selectedNode = value;

            if (_selectedNode != null)
                _selectedNode.Selected = true;

            SelectedNodeChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    [DefaultValue(false)]
    public bool HorizontalScrollEnabled
    {
        get => _horizontalScrollEnabled;
        set
        {
            if (_horizontalScrollEnabled == value)
                return;

            _horizontalScrollEnabled = value;
            UpdateTreeScrollMetrics();
            Invalidate();
        }
    }

    public event EventHandler? SelectedNodeChanged;
    public event EventHandler? NodeExpandedChanged;

    protected override bool ShouldRenderDefaultText => false;

    public void ExpandNode(TreeNode node) => SetExpanded(node, true);

    public void CollapseNode(TreeNode node) => SetExpanded(node, false);

    public void ToggleNode(TreeNode node) => SetExpanded(node, !node.Expanded);

    public override void  OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        _visibleNodes.Clear();
        using var font = CreateRenderFont(Font);
        var display = DisplayRectangle;
        var save = canvas.Save();
        canvas.ClipRect(display);

        var y = display.Top - GetVerticalScrollOffset();
        for (var i = 0; i < Nodes.Count; i++)
            DrawNode(canvas, font, Nodes[i], 0, ref y, 1f);

        canvas.RestoreToCount(save);
    }

    public override void  OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        BuildVisibleNodeHitList();

        for (var i = 0; i < _visibleNodes.Count; i++)
        {
            var visible = _visibleNodes[i];
            if (!visible.Bounds.Contains(e.Location))
                continue;

            SelectedNode = visible.Node;

            if (visible.Node.Nodes.Count > 0)
                ToggleNode(visible.Node);

            return;
        }
    }

    public override void  OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        BuildVisibleNodeHitList();

        TreeNode? hovered = null;
        for (var i = 0; i < _visibleNodes.Count; i++)
        {
            if (_visibleNodes[i].Bounds.Contains(e.Location))
            {
                hovered = _visibleNodes[i].Node;
                break;
            }
        }

        if (ReferenceEquals(_hoveredNode, hovered))
            return;

        _hoveredNode = hovered;
        Invalidate();
    }

    public override void  OnMouseLeave(EventArgs e)
    {
        if (_hoveredNode != null)
        {
            _hoveredNode = null;
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    public override void  OnLayout(LayoutEventArgs e)
    {
        UpdateTreeScrollMetrics();
        base.OnLayout(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnTreeThemeChanged;
            foreach (var state in _states.Values)
                state.Animation.Dispose();

            _rowPaint.Dispose();
            _textPaint.Dispose();
            _linePaint.Dispose();
            _chevronPath.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetExpanded(TreeNode node, bool expanded)
    {
        if (node.Expanded == expanded)
            return;

        node.Expanded = expanded;
        var state = GetState(node);
        state.Animation.StartNewAnimation(expanded ? AnimationDirection.In : AnimationDirection.Out);
        UpdateTreeScrollMetrics();
        NodeExpandedChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void DrawNode(SKCanvas canvas, SKFont font, TreeNode node, int depth, ref float y, float parentReveal)
    {
        if (parentReveal <= 0.001f)
            return;

        var display = DisplayRectangle;
        var itemHeight = ItemHeight * ScaleFactor;
        var fullRow = new SKRect(display.Left, y, display.Right, y + itemHeight);
        var row = new SKRect(display.Left + 2f * ScaleFactor, y + 1f * ScaleFactor, display.Right - 2f * ScaleFactor, y + itemHeight - 1f * ScaleFactor);
        var indent = Indent * ScaleFactor;
        var leftPadding = 8f * ScaleFactor;
        var chevron = new SKRect(
            display.Left + leftPadding + depth * indent,
            y + 7f * ScaleFactor,
            display.Left + leftPadding + depth * indent + 18f * ScaleFactor,
            y + itemHeight - 7f * ScaleFactor);
        var rowVisible = fullRow.Bottom >= display.Top && fullRow.Top <= display.Bottom;
        if (rowVisible)
            _visibleNodes.Add(new VisibleNode(node, fullRow, chevron));

        if (rowVisible && ReferenceEquals(_hoveredNode, node) && !node.Selected)
        {
            _rowPaint.Color = ColorScheme.Primary.WithAlpha(16);
            canvas.DrawRoundRect(row, 8f * ScaleFactor, 8f * ScaleFactor, _rowPaint);
        }

        if (rowVisible && node.Selected)
        {
            _rowPaint.Color = ColorScheme.Primary.WithAlpha(38);
            canvas.DrawRoundRect(row, 8f * ScaleFactor, 8f * ScaleFactor, _rowPaint);
        }

        if (rowVisible && node.Nodes.Count > 0)
            DrawChevron(canvas, chevron, GetReveal(node));

        if (rowVisible)
        {
            var textLeft = display.Left + leftPadding + depth * indent + 24f * ScaleFactor;
            var textRect = new SKRect(textLeft, fullRow.Top, fullRow.Right - 8f, fullRow.Bottom);
            _textPaint.Color = Enabled ? ForeColor : ColorScheme.Outline;
            TextRenderer.DrawText(canvas, node.Text, textRect, _textPaint, font, ContentAlignment.MiddleLeft, true, false, TextWrap.None);
        }

        y += itemHeight;

        var reveal = GetReveal(node) * parentReveal;
        if (reveal <= 0.001f)
            return;

        var childStart = y;
        var childHeight = MeasureVisibleHeight(node.Nodes, 1f) * reveal;
        if (childHeight <= 0.001f)
            return;

        var save = canvas.Save();
        canvas.ClipRect(new SKRect(display.Left, childStart, display.Right, childStart + childHeight));
        var childY = childStart;
        for (var i = 0; i < node.Nodes.Count; i++)
            DrawNode(canvas, font, node.Nodes[i], depth + 1, ref childY, 1f);
        canvas.RestoreToCount(save);
        y = childStart + childHeight;
    }

    private void DrawChevron(SKCanvas canvas, SKRect rect, float progress)
    {
        _linePaint.Color = ColorScheme.ForeColor.WithAlpha(155);
        _linePaint.StrokeWidth = Math.Max(1.35f, 1.35f * ScaleFactor);
        _linePaint.StrokeJoin = SKStrokeJoin.Round;

        _chevronPath.Reset();
        var halfWidth = 3.7f * ScaleFactor;
        var halfHeight = 4.15f * ScaleFactor;
        var tipOffset = 0.95f * ScaleFactor;
        _chevronPath.MoveTo(-halfWidth, -halfHeight);
        _chevronPath.LineTo(tipOffset, 0f);
        _chevronPath.LineTo(-halfWidth, halfHeight);

        var save = canvas.Save();
        canvas.Translate(rect.MidX, rect.MidY);
        canvas.RotateDegrees(90f * progress);
        canvas.DrawPath(_chevronPath, _linePaint);
        canvas.RestoreToCount(save);
    }

    private float GetReveal(TreeNode node)
    {
        var state = GetState(node);
        if (state.Animation.IsAnimating())
            return Math.Clamp((float)state.Animation.GetProgress(), 0f, 1f);

        return node.Expanded ? 1f : 0f;
    }

    private NodeState GetState(TreeNode node)
    {
        if (_states.TryGetValue(node, out var state))
            return state;

        state = new NodeState();
        state.Animation.SetProgress(node.Expanded ? 1d : 0d);
        state.Animation.OnAnimationProgress += _ =>
        {
            UpdateTreeScrollMetrics();
            Invalidate();
        };
        state.Animation.OnAnimationFinished += _ =>
        {
            UpdateTreeScrollMetrics();
            Invalidate();
        };
        _states[node] = state;
        return state;
    }

    private void BuildVisibleNodeHitList()
    {
        _visibleNodes.Clear();
        var display = DisplayRectangle;
        var y = display.Top - GetVerticalScrollOffset();
        for (var i = 0; i < Nodes.Count; i++)
            AddVisibleNodeHit(Nodes[i], 0, ref y, 1f, display);
    }

    private void AddVisibleNodeHit(TreeNode node, int depth, ref float y, float parentReveal, SKRect display)
    {
        if (parentReveal <= 0.001f)
            return;

        var itemHeight = ItemHeight * ScaleFactor;
        var fullRow = new SKRect(display.Left, y, display.Right, y + itemHeight);
        if (fullRow.Bottom >= display.Top && fullRow.Top <= display.Bottom)
        {
            var indent = Indent * ScaleFactor;
            var leftPadding = 8f * ScaleFactor;
            var chevron = new SKRect(
                display.Left + leftPadding + depth * indent,
                y + 7f * ScaleFactor,
                display.Left + leftPadding + depth * indent + 18f * ScaleFactor,
                y + itemHeight - 7f * ScaleFactor);
            _visibleNodes.Add(new VisibleNode(node, fullRow, chevron));
        }

        y += itemHeight;
        var reveal = GetReveal(node) * parentReveal;
        if (reveal <= 0.001f)
            return;

        var childStart = y;
        var childHeight = MeasureVisibleHeight(node.Nodes, 1f) * reveal;
        if (childHeight <= 0.001f)
            return;

        var childDisplay = new SKRect(display.Left, display.Top, display.Right, Math.Min(display.Bottom, childStart + childHeight));
        var childY = childStart;
        for (var i = 0; i < node.Nodes.Count; i++)
            AddVisibleNodeHit(node.Nodes[i], depth + 1, ref childY, 1f, childDisplay);
        y = childStart + childHeight;
    }

    private void UpdateTreeScrollMetrics()
    {
        var contentHeight = Math.Max(0f, MeasureVisibleHeight(Nodes, 1f));
        var contentWidth = HorizontalScrollEnabled
            ? Math.Max(0f, MeasureMaxDepth(Nodes, 0) * Indent * ScaleFactor + 180f * ScaleFactor)
            : 0f;
        var size = new SKSize(contentWidth, contentHeight + Padding.Top + Padding.Bottom);

        if (AutoScrollMinSize != size)
            AutoScrollMinSize = size;

        UpdateScrollBars();
    }

    private float MeasureVisibleHeight(IReadOnlyList<TreeNode> nodes, float parentReveal)
    {
        if (parentReveal <= 0.001f)
            return 0f;

        var height = 0f;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            height += ItemHeight * ScaleFactor;
            height += MeasureVisibleHeight(node.Nodes, 1f) * GetReveal(node);
        }

        return height * parentReveal;
    }

    private int MeasureMaxDepth(IReadOnlyList<TreeNode> nodes, int depth)
    {
        var max = depth;
        for (var i = 0; i < nodes.Count; i++)
        {
            max = Math.Max(max, depth + 1);
            if (nodes[i].Nodes.Count > 0 && GetReveal(nodes[i]) > 0.001f)
                max = Math.Max(max, MeasureMaxDepth(nodes[i].Nodes, depth + 1));
        }

        return max;
    }

    private float GetVerticalScrollOffset()
    {
        return _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;
    }

    private void OnTreeThemeChanged(object? sender, EventArgs e)
    {
        BackColor = ColorScheme.Surface;
        ForeColor = ColorScheme.ForeColor;
        BorderColor = ColorScheme.Outline.WithAlpha(90);
        Invalidate();
    }

    private readonly record struct VisibleNode(TreeNode Node, SKRect Bounds, SKRect ChevronBounds);
}
