using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

// ============================================================================
// Minimal mermaid "flowchart"/"graph" renderer: parses node/edge declarations
// and lays them out level-by-level (a simplified Sugiyama-style layout), then
// produces a MermaidDiagramBox with pre-computed geometry so drawing is trivial.
// Other mermaid diagram kinds (sequenceDiagram, classDiagram, ...) are not
// supported and fall back to a plain fenced code block rendering.
// ============================================================================

internal static class MermaidRenderer
{
    private static readonly Regex DirectiveLine = new(@"^\s*(graph|flowchart)\s+(TD|TB|LR|RL|BT)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EdgeLabel = new(@"^\s*\|([^|]*)\|", RegexOptions.Compiled);
    private static readonly Regex NodeToken = new(
        @"^([A-Za-z0-9_\-]+)\s*(?:\[(.*)\]|\(\((.*)\)\)|\((.*)\)|\{\{(.*)\}\}|\{(.*)\})?$",
        RegexOptions.Compiled);

    // Longest-first so a 4-char token like "-.->" is preferred over its 3-char prefix "-.-" at the same position.
    private static readonly string[] ArrowTokens = { "-.->", "-.-", "-->", "---", "==>", "===" };

    private enum Direction { TopDown, LeftRight, RightLeft, BottomUp }

    private sealed class MNode
    {
        public string Id = "";
        public string Label = "";
        public MermaidNodeShape Shape = MermaidNodeShape.Rectangle;
        public int Level = -1;
        public SKRect Bounds;
    }

    private sealed class MEdge
    {
        public string FromId = "";
        public string ToId = "";
        public string? Label;
        public bool Directed;
        public bool Dashed;
    }

    /// <summary>Attempts to parse <paramref name="cb"/> as a mermaid flowchart and lay it out. Returns null if unsupported.</summary>
    public static MermaidDiagramBox? TryBuild(CodeBlockBlock cb, MarkdownTheme theme, MarkdownFontCache fonts, float x, float y, float width, float scale)
    {
        if (!string.Equals(cb.Language, "mermaid", StringComparison.OrdinalIgnoreCase)) return null;

        string[] lines = cb.Code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        Direction direction = Direction.TopDown;
        bool foundDirective = false;
        var nodes = new Dictionary<string, MNode>(StringComparer.Ordinal);
        var edges = new List<MEdge>();

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("%%", StringComparison.Ordinal)) continue;

            if (!foundDirective)
            {
                var dm = DirectiveLine.Match(line);
                if (dm.Success)
                {
                    foundDirective = true;
                    direction = dm.Groups[2].Value.ToUpperInvariant() switch
                    {
                        "LR" => Direction.LeftRight,
                        "RL" => Direction.RightLeft,
                        "BT" => Direction.BottomUp,
                        _ => Direction.TopDown,
                    };
                    continue;
                }

                // First non-blank/non-comment line isn't a flowchart/graph directive: unsupported diagram kind.
                return null;
            }

            // Skip constructs we don't render but shouldn't misinterpret as nodes/edges.
            string lower = line.ToLowerInvariant();
            if (lower.StartsWith("subgraph", StringComparison.Ordinal) || lower == "end" ||
                lower.StartsWith("style ", StringComparison.Ordinal) || lower.StartsWith("classdef", StringComparison.Ordinal) ||
                lower.StartsWith("class ", StringComparison.Ordinal) || lower.StartsWith("click ", StringComparison.Ordinal) ||
                lower.StartsWith("linkstyle", StringComparison.Ordinal))
            {
                continue;
            }

            ParseLine(line, nodes, edges);
        }

        if (!foundDirective || nodes.Count == 0) return null;

        AssignLevels(nodes, edges);
        return Layout(nodes, edges, direction, theme, fonts, x, y, width, scale);
    }

    private static void ParseLine(string line, Dictionary<string, MNode> nodes, List<MEdge> edges)
    {
        var arrowMatches = FindTopLevelArrows(line);
        if (arrowMatches.Count == 0)
        {
            if (TryParseNodeToken(line, out var node)) RegisterNode(nodes, node);
            return;
        }

        int pos = 0;
        string? prevId = null;
        string? pendingLabel = null;
        bool directed = true, dashed = false;

        foreach (var arrow in arrowMatches)
        {
            string beforeText = line[pos..arrow.Index].Trim();
            if (!TryParseNodeToken(beforeText, out var node)) return;
            RegisterNode(nodes, node);

            if (prevId != null)
            {
                edges.Add(new MEdge { FromId = prevId, ToId = node.Id, Label = pendingLabel, Directed = directed, Dashed = dashed });
            }

            prevId = node.Id;
            ClassifyArrow(arrow.Value, out directed, out dashed);
            pos = arrow.Index + arrow.Length;

            var labelMatch = EdgeLabel.Match(line[pos..]);
            if (labelMatch.Success)
            {
                pendingLabel = labelMatch.Groups[1].Value.Trim();
                pos += labelMatch.Length;
            }
            else
            {
                pendingLabel = null;
            }
        }

        string tailText = line[pos..].Trim();
        if (prevId != null && TryParseNodeToken(tailText, out var tailNode))
        {
            RegisterNode(nodes, tailNode);
            edges.Add(new MEdge { FromId = prevId, ToId = tailNode.Id, Label = pendingLabel, Directed = directed, Dashed = dashed });
        }
    }

    private readonly struct ArrowHit
    {
        public readonly int Index;
        public readonly int Length;
        public readonly string Value;
        public ArrowHit(int index, int length, string value) { Index = index; Length = length; Value = value; }
    }

    /// <summary>
    ///  Scans for arrow tokens (-->, ---, -.->, ==&gt;, ...) while tracking [ ( { bracket depth, so dashes or
    ///  other arrow-like characters inside a node's shape/label (e.g. "A[Multi-Step]") are never mistaken
    ///  for an edge arrow. This is the main source of intermittently blank/partial diagrams: a plain regex
    ///  scan over the whole line would match arrow-like substrings inside bracketed labels too.
    /// </summary>
    private static List<ArrowHit> FindTopLevelArrows(string line)
    {
        var hits = new List<ArrowHit>();
        int depth = 0;
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (c is '[' or '(' or '{') { depth++; i++; continue; }
            if (c is ']' or ')' or '}') { depth = Math.Max(0, depth - 1); i++; continue; }

            if (depth == 0)
            {
                string? matched = null;
                foreach (var token in ArrowTokens)
                {
                    if (i + token.Length <= line.Length && string.CompareOrdinal(line, i, token, 0, token.Length) == 0)
                    {
                        matched = token;
                        break;
                    }
                }
                if (matched != null)
                {
                    hits.Add(new ArrowHit(i, matched.Length, matched));
                    i += matched.Length;
                    continue;
                }
            }
            i++;
        }
        return hits;
    }

    private static void ClassifyArrow(string arrow, out bool directed, out bool dashed)
    {
        dashed = arrow.Contains('.');
        directed = arrow.EndsWith(">", StringComparison.Ordinal);
    }

    private static bool TryParseNodeToken(string text, out MNode node)
    {
        node = new MNode();
        if (text.Length == 0) return false;

        var m = NodeToken.Match(text);
        if (!m.Success) return false;

        node.Id = m.Groups[1].Value;
        string? label = null;
        var shape = MermaidNodeShape.Rectangle;
        if (m.Groups[2].Success) { label = m.Groups[2].Value; shape = MermaidNodeShape.Rectangle; }
        else if (m.Groups[3].Success) { label = m.Groups[3].Value; shape = MermaidNodeShape.Circle; }
        else if (m.Groups[4].Success) { label = m.Groups[4].Value; shape = MermaidNodeShape.Rounded; }
        else if (m.Groups[5].Success) { label = m.Groups[5].Value; shape = MermaidNodeShape.Rectangle; } // hexagon fallback
        else if (m.Groups[6].Success) { label = m.Groups[6].Value; shape = MermaidNodeShape.Diamond; }

        node.Label = string.IsNullOrWhiteSpace(label) ? node.Id : label!.Trim();
        node.Shape = shape;
        return true;
    }

    private static void RegisterNode(Dictionary<string, MNode> nodes, MNode parsed)
    {
        if (nodes.TryGetValue(parsed.Id, out var existing))
        {
            // Keep the first non-trivial label/shape seen for this id (later bare references shouldn't overwrite it).
            if (existing.Label == existing.Id && parsed.Label != parsed.Id)
            {
                existing.Label = parsed.Label;
                existing.Shape = parsed.Shape;
            }
            return;
        }
        nodes[parsed.Id] = parsed;
    }

    /// <summary>
    ///  Assigns a level (row/column index) to every node for the layered layout. Cyclic flowcharts are very
    ///  common (e.g. a "retry"/"no" edge looping back to an earlier step); a naive relaxation that walks
    ///  every edge, including ones that close a cycle, keeps pushing levels higher every time it goes
    ///  around the loop, bounded only by an iteration guard. That produced enormous level values (tens of
    ///  levels for a 7-node diagram), laying most nodes out far below the diagram's reserved area — which
    ///  looked like the diagram "didn't render in place" and instead appeared far down the page. Fixed by
    ///  detecting back edges via DFS first and excluding them from level propagation, so the graph used for
    ///  leveling is always a DAG and the relaxation always terminates quickly/correctly.
    /// </summary>
    private static void AssignLevels(Dictionary<string, MNode> nodes, List<MEdge> edges)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (!adjacency.TryGetValue(e.FromId, out var targets)) adjacency[e.FromId] = targets = new List<string>();
            targets.Add(e.ToId);
        }

        var backEdges = new HashSet<(string From, string To)>();
        var visitState = new Dictionary<string, int>(StringComparer.Ordinal); // 0 = unvisited (absent), 1 = in progress, 2 = done
        var stack = new Stack<(string Id, int ChildIndex)>();

        foreach (var start in nodes.Keys)
        {
            if (visitState.ContainsKey(start)) continue;

            stack.Push((start, 0));
            visitState[start] = 1;
            while (stack.Count > 0)
            {
                var (id, childIndex) = stack.Pop();
                var targets = adjacency.TryGetValue(id, out var t) ? t : null;

                int i = childIndex;
                bool pushedChild = false;
                if (targets != null)
                {
                    for (; i < targets.Count; i++)
                    {
                        string targetId = targets[i];
                        if (!nodes.ContainsKey(targetId)) continue;

                        visitState.TryGetValue(targetId, out int state);
                        if (state == 1) { backEdges.Add((id, targetId)); continue; }
                        if (state == 0)
                        {
                            stack.Push((id, i + 1));
                            stack.Push((targetId, 0));
                            visitState[targetId] = 1;
                            pushedChild = true;
                            break;
                        }
                    }
                }

                if (!pushedChild) visitState[id] = 2;
            }
        }

        // Forward-only adjacency (back edges excluded) is guaranteed to be a DAG, so this relaxation
        // always terminates and produces correct longest-path levels.
        var forwardAdjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var hasIncoming = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (backEdges.Contains((e.FromId, e.ToId))) continue;
            if (!forwardAdjacency.TryGetValue(e.FromId, out var targets)) forwardAdjacency[e.FromId] = targets = new List<string>();
            targets.Add(e.ToId);
            hasIncoming.Add(e.ToId);
        }

        var queue = new Queue<MNode>();
        int guard = (nodes.Count + edges.Count) * 2 + 16;

        void Relax()
        {
            while (queue.Count > 0 && guard-- > 0)
            {
                var current = queue.Dequeue();
                if (!forwardAdjacency.TryGetValue(current.Id, out var targets)) continue;
                foreach (var targetId in targets)
                {
                    if (!nodes.TryGetValue(targetId, out var target)) continue;
                    int candidate = current.Level + 1;
                    if (candidate > target.Level)
                    {
                        target.Level = candidate;
                        queue.Enqueue(target);
                    }
                }
            }
        }

        foreach (var n in nodes.Values)
        {
            if (!hasIncoming.Contains(n.Id))
            {
                n.Level = 0;
                queue.Enqueue(n);
            }
        }
        Relax();

        // Any node still unreached belongs to a disconnected component. Seed each such component's first
        // still-unleveled node as its own root and relax again, repeating until every node has a level.
        foreach (var n in nodes.Values)
        {
            if (n.Level >= 0) continue;
            n.Level = 0;
            queue.Enqueue(n);
            Relax();
        }

        foreach (var n in nodes.Values)
        {
            if (n.Level < 0) n.Level = 0;
        }
    }


    private static MermaidDiagramBox Layout(
        Dictionary<string, MNode> nodes, List<MEdge> edges, Direction direction,
        MarkdownTheme theme, MarkdownFontCache fonts, float x, float y, float width, float scale)
    {
        float fontSize = Math.Max(11f, theme.SmallFontSize * scale);
        var font = fonts.GetFont(theme, false, fontSize, false, false);
        float ascent = -font.Metrics.Ascent;
        float descent = font.Metrics.Descent;

        float padX = 14f * scale;
        float padY = 10f * scale;
        float minNodeWidth = 48f * scale;
        float nodeHeight = MathF.Max(36f * scale, ascent + descent + 2 * padY);
        float siblingGap = 24f * scale;
        float levelGap = 56f * scale;

        var levels = new List<List<MNode>>();
        foreach (var n in nodes.Values)
        {
            while (levels.Count <= n.Level) levels.Add(new List<MNode>());
            levels[n.Level].Add(n);
        }

        bool horizontalDirection = direction is Direction.LeftRight or Direction.RightLeft;

        // Measure each node's box size.
        foreach (var n in nodes.Values)
        {
            float textW = fonts.MeasureText(n.Label, theme, false, fontSize, false, false);
            float boxW = MathF.Max(minNodeWidth, textW + 2 * padX);
            float boxH = nodeHeight;
            if (n.Shape == MermaidNodeShape.Diamond) { boxW += boxW * 0.4f; boxH += boxH * 0.35f; }
            else if (n.Shape == MermaidNodeShape.Circle) { float d = MathF.Max(boxW, boxH); boxW = boxH = d; }
            n.Bounds = new SKRect(0, 0, boxW, boxH);
        }

        // Position nodes level by level along the "cross" axis, levels spread along the "main" axis.
        float mainOffset = 0f;
        var levelMainSize = new float[levels.Count];
        for (int levelIndex = 0; levelIndex < levels.Count; levelIndex++)
        {
            var levelNodes = levels[levelIndex];
            float crossExtent = 0f;
            foreach (var n in levelNodes) crossExtent += (horizontalDirection ? n.Bounds.Height : n.Bounds.Width) + siblingGap;
            if (levelNodes.Count > 0) crossExtent -= siblingGap;

            float crossOffset = -crossExtent / 2f;
            float levelMain = 0f;
            foreach (var n in levelNodes)
            {
                float w = n.Bounds.Width, h = n.Bounds.Height;
                levelMain = MathF.Max(levelMain, horizontalDirection ? w : h);
                if (horizontalDirection)
                {
                    n.Bounds = new SKRect(mainOffset, crossOffset, mainOffset + w, crossOffset + h);
                    crossOffset += h + siblingGap;
                }
                else
                {
                    n.Bounds = new SKRect(crossOffset, mainOffset, crossOffset + w, mainOffset + h);
                    crossOffset += w + siblingGap;
                }
            }
            levelMainSize[levelIndex] = levelMain;
            mainOffset += levelMain + levelGap;
        }

        // Normalize into absolute coordinates (content area starting at x/y, centered within `width`).
        float minCrossVal = float.MaxValue, maxCrossVal = float.MinValue, maxMainVal = 0f;
        foreach (var n in nodes.Values)
        {
            float crossMin = horizontalDirection ? n.Bounds.Top : n.Bounds.Left;
            float crossMax = horizontalDirection ? n.Bounds.Bottom : n.Bounds.Right;
            minCrossVal = MathF.Min(minCrossVal, crossMin);
            maxCrossVal = MathF.Max(maxCrossVal, crossMax);
            maxMainVal = MathF.Max(maxMainVal, horizontalDirection ? n.Bounds.Right : n.Bounds.Bottom);
        }

        float crossExtentTotal = maxCrossVal - minCrossVal;
        bool reverseMain = direction is Direction.RightLeft or Direction.BottomUp;

        // The cross axis is X for vertical (TD/BT) diagrams and Y for horizontal (LR/RL) diagrams.
        // Centering against `width` only makes sense for the X cross axis; for the Y cross axis we
        // simply top-align at the layout cursor `y` (there is no fixed height to center within).
        // Anchoring the Y cross axis to `x` instead of `y` was the root cause of LR/RL diagrams being
        // drawn at the wrong absolute vertical position — often nowhere near their reserved, correctly
        // sized area in the document flow — which is what made the diagram appear to render blank.
        float crossStart = horizontalDirection
            ? y - minCrossVal
            : x + MathF.Max(0f, (width - crossExtentTotal) / 2f) - minCrossVal;

        foreach (var n in nodes.Values)
        {
            float main = horizontalDirection ? n.Bounds.Left : n.Bounds.Top;
            float mainSize = horizontalDirection ? n.Bounds.Width : n.Bounds.Height;
            float cross = horizontalDirection ? n.Bounds.Top : n.Bounds.Left;
            float crossSize = horizontalDirection ? n.Bounds.Height : n.Bounds.Width;

            float resolvedMain = reverseMain ? (maxMainVal - main - mainSize) : main;

            float absMain = (horizontalDirection ? x : y) + resolvedMain;
            float absCross = crossStart + cross;

            n.Bounds = horizontalDirection
                ? new SKRect(absMain, absCross, absMain + mainSize, absCross + crossSize)
                : new SKRect(absCross, absMain, absCross + crossSize, absMain + mainSize);
        }

        var box = new MermaidDiagramBox();
        float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;

        foreach (var n in nodes.Values)
        {
            box.Nodes.Add(new MermaidNodeBox
            {
                Bounds = n.Bounds,
                Label = n.Label,
                Shape = n.Shape,
                Font = font,
                TextColor = theme.BodyColor,
                FillColor = theme.CodeBackground,
                BorderColor = theme.BorderColor,
            });
            minY = MathF.Min(minY, n.Bounds.Top); maxY = MathF.Max(maxY, n.Bounds.Bottom);
            minX = MathF.Min(minX, n.Bounds.Left); maxX = MathF.Max(maxX, n.Bounds.Right);
        }

        foreach (var e in edges)
        {
            if (!nodes.TryGetValue(e.FromId, out var from) || !nodes.TryGetValue(e.ToId, out var to)) continue;

            // Back/loop edges (target level <= source level, e.g. a "retry"/"no" branch looping back to an
            // earlier step) are routed around the outside of the diagram instead of straight through
            // unrelated nodes in between — a straight line would visually overlap other edges/nodes and put
            // its label in a confusing spot (this is exactly what a plain center-to-center line produced).
            bool isBackEdge = to.Level <= from.Level;
            List<SKPoint> points;

            if (isBackEdge)
            {
                float bypassMargin = 22f * scale;
                if (!horizontalDirection)
                {
                    float bypassX = maxX + bypassMargin;
                    var fromAnchor = new SKPoint(from.Bounds.Right, from.Bounds.MidY);
                    var toAnchor = new SKPoint(to.Bounds.Right, to.Bounds.MidY);
                    points = new List<SKPoint>
                    {
                        fromAnchor,
                        new SKPoint(bypassX, fromAnchor.Y),
                        new SKPoint(bypassX, toAnchor.Y),
                        toAnchor,
                    };
                }
                else
                {
                    float bypassY = maxY + bypassMargin;
                    var fromAnchor = new SKPoint(from.Bounds.MidX, from.Bounds.Bottom);
                    var toAnchor = new SKPoint(to.Bounds.MidX, to.Bounds.Bottom);
                    points = new List<SKPoint>
                    {
                        fromAnchor,
                        new SKPoint(fromAnchor.X, bypassY),
                        new SKPoint(toAnchor.X, bypassY),
                        toAnchor,
                    };
                }
            }
            else
            {
                SKPoint fromCenter = new(from.Bounds.MidX, from.Bounds.MidY);
                SKPoint toCenter = new(to.Bounds.MidX, to.Bounds.MidY);
                points = new List<SKPoint>
                {
                    ClipToBounds(from.Bounds, fromCenter, toCenter),
                    ClipToBounds(to.Bounds, toCenter, fromCenter),
                };
            }

            var edgeBox = new MermaidEdgeBox
            {
                Points = points,
                Directed = e.Directed,
                Dashed = e.Dashed,
                Label = e.Label,
                Color = theme.MutedColor,
                TextColor = theme.BodyColor,
            };

            if (!string.IsNullOrEmpty(e.Label))
            {
                edgeBox.LabelFont = font;
                var mid1 = points[points.Count / 2 - 1];
                var mid2 = points[points.Count / 2];
                edgeBox.LabelPosition = new SKPoint((mid1.X + mid2.X) / 2f, (mid1.Y + mid2.Y) / 2f);
            }

            box.Edges.Add(edgeBox);

            if (isBackEdge)
            {
                foreach (var p in points)
                {
                    minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X);
                    minY = MathF.Min(minY, p.Y); maxY = MathF.Max(maxY, p.Y);
                }
            }
        }

        float totalWidth = MathF.Max(nodeHeight, maxX - minX);
        float totalHeight = MathF.Max(nodeHeight, maxY - minY);
        box.Bounds = new SKRect(x, minY, x + MathF.Max(width, totalWidth), minY + totalHeight + 4f * scale);
        return box;
    }

    private static SKPoint ClipToBounds(SKRect rect, SKPoint center, SKPoint target)
    {
        float dx = target.X - center.X;
        float dy = target.Y - center.Y;
        if (dx == 0f && dy == 0f) return center;

        float halfW = rect.Width / 2f;
        float halfH = rect.Height / 2f;
        float scaleX = dx != 0f ? halfW / MathF.Abs(dx) : float.MaxValue;
        float scaleY = dy != 0f ? halfH / MathF.Abs(dy) : float.MaxValue;
        float scale = MathF.Min(scaleX, scaleY);
        return new SKPoint(center.X + dx * scale, center.Y + dy * scale);
    }
}
