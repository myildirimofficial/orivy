using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class MarkdownViewerDemoPage : Container
{
    private MarkdownViewer viewer = null!;
    private TextBox editor = null!;

    public MarkdownViewerDemoPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Markdown Viewer";
        Name = "markdownViewerDemoPage";
        Dock = DockStyle.Fill;
        Padding = new(24);
        Radius = new(0);
        Border = new(0);

        editor = new TextBox
        {
            Name = "markdownEditor",
            Dock = DockStyle.Bottom,
            Height = 180,
            Margin = new(0, 12, 0, 0),
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            PlaceholderText = "Write markdown here...",
        };
        editor.TextChanged += (_, _) => viewer.Markdown = editor.Text;

        viewer = new MarkdownViewer
        {
            Name = "markdownViewer",
            Dock = DockStyle.Fill,
            Margin = new(0),
            Markdown = editor.Text,
            AutoScroll = true,
            AutoScrollMargin = new(0, 24),
        };

        Controls.Add(viewer);
        Controls.Add(editor);
    }
}
