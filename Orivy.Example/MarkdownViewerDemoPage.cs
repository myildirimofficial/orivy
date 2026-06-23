using Orivy.Controls;
using Orivy.Controls.RichText;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class MarkdownViewerDemoPage : Container
{
    private RichTextBox viewer = null!;
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
        Padding = new(3);
        Radius = new(0);
        Border = new(0);

        editor = new()
        {
            Name = "markdownEditor",
            Dock = DockStyle.Fill,
            Margin = new(0, 12, 0, 0),
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
           
            PlaceholderText = "Write markdown here...",
        };
        editor.TextChanged += (_, _) => viewer.Text = editor.Text;

        viewer = new()
        {
            Name = "markdownViewer",
            Dock = DockStyle.Fill,
            Mode = RichTextMode.MarkdownPreview,
            Margin = new(0),
            Text = editor.Text,
            AutoScroll = true,
            Multiline = true,
            //AutoScrollMargin = new(0, 24),
        };

        var splitter = new SplitContainer()
        {
            Name = "markdownSplitter",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Height = 24,
            Margin = new(0, 12, 0, 0),
        };
        splitter.Panel1.Controls.Add(viewer);
        splitter.Panel2.Controls.Add(editor);

        Controls.Add(splitter);
    }
}
