using SkiaSharp;
using System.Collections.Generic;

namespace Orivy.Controls;

public class TreeNode
{
    public TreeNode(string text = "")
    {
        Text = text;
    }

    public string Text { get; set; }

    public object? Tag { get; set; }

    public SKImage? Image { get; set; }

    public bool Expanded { get; internal set; }

    public bool Selected { get; internal set; }

    public List<TreeNode> Nodes { get; } = new();

    public TreeNode Add(string text)
    {
        var node = new TreeNode(text);
        Nodes.Add(node);
        return node;
    }
}
