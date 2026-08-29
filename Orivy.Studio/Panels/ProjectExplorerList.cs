using Orivy;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Orivy.Studio.Panels;

/// <summary>
/// A general-purpose folder browser (no project/solution file needed — just pick a folder), built on
/// Orivy's own <see cref="Controls.TreeView"/> control instead of a hand-rolled owner-drawn list.
/// Shows every file in the folder, not just <c>*.orivy.json</c> design files: a <c>.orivy.json</c>
/// opens in the visual designer, anything else (a hand-written or Orivy-generated <c>Designer.cs</c>,
/// or any other text file) opens as plain text for direct editing — see
/// <see cref="StudioWindow.OpenPath"/>. The usual noise directories (<c>bin</c>, <c>obj</c>,
/// <c>.git</c>, <c>.vs</c>) are skipped since there's no project file to scope the tree to otherwise.
/// </summary>
public sealed class ProjectExplorerList : Element
{
    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea", "node_modules",
    };

    private sealed class FileTag
    {
        public required string FullPath;
    }

    private readonly TreeView _tree = new() { Dock = DockStyle.Fill };
    private readonly Element _emptyState;
    private string? _rootFolder;
    private string? _activePath;
    private SKImage? _folderIcon;
    private SKImage? _fileIcon;
    private bool _suppressFileOpenEvent;

    public ProjectExplorerList()
    {
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);
        BackColor = SKColors.Transparent;

        _tree.Border = new Thickness(1);
        _tree.Radius = new Radius(12);
        _tree.Visible = false;
        _tree.SelectedNodeChanged += OnSelectedNodeChanged;
        _tree.ConfigureVisualStyles(styles => styles.Base(b => b.Background(ColorScheme.Surface.WithAlpha(178))));

        _emptyState = new Element
        {
            Dock = DockStyle.Fill, Border = new Thickness(1), Radius = new Radius(12),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _emptyState.ConfigureVisualStyles(styles => styles.Base(b => b
            .Background(ColorScheme.Surface.WithAlpha(178))
            .BorderColor(ColorScheme.Outline.WithAlpha(60))
            .Foreground(ColorScheme.ForeColor.WithAlpha(120))));

        Controls.Add(_emptyState);
        Controls.Add(_tree);

        RefreshIcons();
        ColorScheme.ThemeChanged += (_, _) => { RefreshIcons(); Rescan(); };
    }

    protected override bool HandlesMouseWheelInput => true;

    /// <summary>Raised when the user single-clicks a file node (VS Solution-Explorer-style: clicking
    /// a file navigates to/previews it immediately — no double-click required). The path's extension
    /// decides how the shell opens it.</summary>
    public event Action<string>? FileOpenRequested;

    /// <summary>The folder currently being browsed, or null if none has been opened yet.</summary>
    public string? RootFolder
    {
        get => _rootFolder;
        set
        {
            _rootFolder = value;
            Rescan();
        }
    }

    /// <summary>Highlights the node for this file path (the active document's backing file), if present.</summary>
    public void SetActiveFile(string? fullPath)
    {
        _activePath = fullPath;
        var match = FindNodeByPath(_tree.Nodes, fullPath);
        if (match == null)
            return;

        // Selecting a node fires SelectedNodeChanged, which normally raises FileOpenRequested — but
        // this call is the *result* of a file already being opened (StudioWindow syncing the tree's
        // highlight to match), not a new user click asking to open one. Without this guard, every
        // programmatic sync would immediately re-request opening the same file it's reflecting.
        _suppressFileOpenEvent = true;
        try { _tree.SelectedNode = match; }
        finally { _suppressFileOpenEvent = false; }
    }

    private static TreeNode? FindNodeByPath(List<TreeNode> nodes, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (var node in nodes)
        {
            if (node.Tag is FileTag tag && string.Equals(tag.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return node;

            var nested = FindNodeByPath(node.Nodes, path);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public void Rescan()
    {
        _tree.Nodes.Clear();
        _tree.SelectedNode = null;

        if (string.IsNullOrEmpty(_rootFolder) || !Directory.Exists(_rootFolder))
        {
            _tree.Visible = false;
            _emptyState.Visible = true;
            _emptyState.Text = "No folder open";
            Invalidate();
            return;
        }

        var root = BuildNode(_rootFolder, isRoot: true);
        if (root == null)
        {
            _tree.Visible = false;
            _emptyState.Visible = true;
            _emptyState.Text = "Folder is empty";
            Invalidate();
            return;
        }

        _tree.Nodes.Add(root);
        _tree.ExpandNode(root);
        _tree.Visible = true;
        _emptyState.Visible = false;

        if (_activePath != null)
            SetActiveFile(_activePath);

        Invalidate();
    }

    /// <summary>Builds a tree node for every file/folder under <paramref name="folder"/> (skipping
    /// build/VCS noise directories), or null if it turns out to be entirely empty.</summary>
    private TreeNode? BuildNode(string folder, bool isRoot = false)
    {
        List<TreeNode> childNodes = new();

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(folder).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                if (IgnoredDirectoryNames.Contains(Path.GetFileName(dir)))
                    continue;

                var childNode = BuildNode(dir);
                if (childNode != null)
                    childNodes.Add(childNode);
            }

            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(file);
                childNodes.Add(new TreeNode(name) { Tag = new FileTag { FullPath = file }, Image = _fileIcon });
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        if (childNodes.Count == 0 && !isRoot)
            return null;

        var folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var node = new TreeNode(string.IsNullOrEmpty(folderName) ? folder : folderName) { Image = _folderIcon };
        foreach (var child in childNodes)
            node.Nodes.Add(child);

        return node;
    }

    private void OnSelectedNodeChanged(object? sender, EventArgs e)
    {
        if (!_suppressFileOpenEvent && _tree.SelectedNode?.Tag is FileTag tag)
            FileOpenRequested?.Invoke(tag.FullPath);
    }

    private void RefreshIcons()
    {
        var color = ColorScheme.ForeColor.WithAlpha(190);
        var oldFolder = _folderIcon;
        var oldFile = _fileIcon;
        _folderIcon = ToolbarIcons.CreateImage("folder", 18f * ScaleFactor * 2f, color);
        _fileIcon = ToolbarIcons.CreateImage("file", 18f * ScaleFactor * 2f, new SKColor(59, 130, 246));
        oldFolder?.Dispose();
        oldFile?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _folderIcon?.Dispose();
            _fileIcon?.Dispose();
        }

        base.Dispose(disposing);
    }
}
