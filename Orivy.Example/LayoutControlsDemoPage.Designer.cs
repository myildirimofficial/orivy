using Orivy;
using Orivy.Controls;
using SkiaSharp;

namespace Orivy.Example;

internal sealed partial class LayoutControlsDemoPage
{
    private void InitializeComponent()
    {
        Text = "Layout & Tree";
        Name = "layoutControlsDemoPage";
        Dock = DockStyle.Fill;
        Padding = new(24);
        AutoScroll = true;
        AutoScrollMargin = new(0, 24);
        Radius = new(0);
        Border = new(0);

        var header = CreateCard("Layout & Tree", "TreeView, SplitContainer, FlowLayout and Grid controls with reusable container behavior.");

        var splitCard = CreateCard("SplitContainer + TreeView", "Drag the splitter. Tree nodes expand and collapse with AnimationManager.");
        var split = new SplitContainer
        {
            Dock = DockStyle.Top,
            Height = 260,
            Margin = new(0, 0, 0, 8),
            SplitterDistance = 260,
            SplitterWidth = 7,
            Padding = new(0)
        };

        var tree = CreateTreeView();
        tree.Dock = DockStyle.Fill;
        split.Panel1.Controls.Add(tree);

        var details = new Element
        {
            Text = "Node details\nSelect items from the tree. This panel is a normal Element hosted inside Panel2.",
            Dock = DockStyle.Fill,
            Padding = new(18),
            Radius = new(12),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
        tree.SelectedNodeChanged += (_, _) =>
        {
            var selected = tree.SelectedNode?.Text ?? "None";
            details.Text = $"Node details\nSelected: {selected}";
        };
        split.Panel2.Controls.Add(details);
        AddCardContent(splitCard, split);

        var flowCard = CreateCard("FlowLayout", "Children flow and wrap automatically.");
        var flow = new FlowLayout
        {
            Dock = DockStyle.Top,
            Height = 112,
            Padding = new(4),
            HorizontalGap = 8,
            VerticalGap = 8,
            WrapContents = true,
            Border = new(0)
        };

        var labels = new[] { "Build", "Review", "Ship", "Observe", "Improve", "Document", "Automate", "Polish" };
        for (var i = 0; i < labels.Length; i++)
        {
            flow.Controls.Add(new Button
            {
                Text = labels[i],
                AutoSize = true,
                Height = 34,
                Padding = new(12, 7, 12, 7),
                Radius = new(10)
            });
        }
        AddCardContent(flowCard, flow);

        var gridCard = CreateCard("Grid", "Cells use row, column and span placement.");
        var grid = new Grid
        {
            Dock = DockStyle.Top,
            Height = 190,
            RowCount = 3,
            ColumnCount = 4,
            RowGap = 10,
            ColumnGap = 10,
            Padding = new(0)
        };
        grid.Add(CreateTile("Overview", new SKColor(0x0E, 0xA5, 0xE9)), 0, 0, 1, 2);
        grid.Add(CreateTile("Metrics", new SKColor(0x22, 0xC5, 0x5E)), 0, 2, 2, 1);
        grid.Add(CreateTile("Queue", new SKColor(0xF5, 0x9E, 0x0B)), 0, 3, 1, 1);
        grid.Add(CreateTile("Activity", new SKColor(0x8B, 0x5C, 0xF6)), 1, 0, 2, 2);
        grid.Add(CreateTile("Health", new SKColor(0xEC, 0x48, 0x99)), 2, 2, 1, 2);
        grid.Add(CreateTile("Deploy", new SKColor(0x14, 0xB8, 0xA6)), 1, 3, 1, 1);
        AddCardContent(gridCard, grid);

        Controls.Add(gridCard);
        Controls.Add(flowCard);
        Controls.Add(splitCard);
        Controls.Add(header);
    }

    private static TreeView CreateTreeView()
    {
        var tree = new TreeView
        {
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(90),
            Radius = new(12),
            BackColor = ColorScheme.Surface,
            Padding = new(8)
        };

        var workspace = new TreeNode("Workspace");
        tree.Nodes.Add(workspace);
        workspace.Add("Dashboard");
        workspace.Add("Reports");
        var settings = workspace.Add("Settings");
        settings.Add("Theme");
        settings.Add("Motion");

        var project = new TreeNode("Project");
        tree.Nodes.Add(project);
        project.Add("Roadmap");
        project.Add("Issues");
        project.Add("Releases");

        tree.ExpandNode(workspace);
        return tree;
    }

    private static Element CreateTile(string text, SKColor accent)
    {
        return new Element
        {
            Text = text,
            Padding = new(14),
            Radius = new(10),
            Border = new(1),
            BorderColor = accent.WithAlpha(120),
            BackColor = accent.WithAlpha(28),
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Element CreateCard(string title, string description)
    {
        var card = new Element
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(12),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(90)
        };

        card.Controls.Add(new Element
        {
            Text = $"{title}\n{description}",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new(0, 0, 0, 14),
            Border = new(0),
            BackColor = SKColors.Transparent,
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft
        });
        return card;
    }

    private static void AddCardContent(Element card, ElementBase content)
    {
        var header = card.Controls.Count > 0 ? card.Controls[0] : null;
        if (header != null)
            card.Controls.Remove(header);

        card.Controls.Add(content);

        if (header != null)
            card.Controls.Add(header);
    }
}
