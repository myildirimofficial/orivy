using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orivy.Studio.Persistence;

/// <summary>Serializable snapshot of the design document (.orivy JSON project file).</summary>
public sealed class DesignDocumentDto
{
    public int Version { get; set; } = 2;
    public float RootWidth { get; set; }
    public float RootHeight { get; set; }
    public List<NodeDto> Controls { get; set; } = new();

    public sealed class NodeDto
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }
        public int Z { get; set; }
        public bool Visible { get; set; } = true;
        public bool Locked { get; set; }
        public string Dock { get; set; } = "None";
        public string Anchor { get; set; } = "Top, Left";

        /// <summary>True for a control-group container (see <see cref="DesignSurface.Groups"/>).</summary>
        public bool IsGroup { get; set; }

        /// <summary>Nested members, present only for a group container. Locations are relative to
        /// the group, matching how they live at runtime as real child controls.</summary>
        public List<NodeDto> Children { get; set; } = new();
    }
}

/// <summary>Saves/loads the design surface to a JSON project file.</summary>
public static class DesignSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Save(DesignSurface surface)
    {
        var dto = new DesignDocumentDto
        {
            RootWidth = surface.DesignRoot.Width,
            RootHeight = surface.DesignRoot.Height,
        };

        foreach (var control in surface.DesignedControls)
            dto.Controls.Add(ToNode(control, surface));

        return JsonSerializer.Serialize(dto, Options);
    }

    private static DesignDocumentDto.NodeDto ToNode(ElementBase control, DesignSurface surface)
    {
        var node = new DesignDocumentDto.NodeDto
        {
            Type = control.GetType().Name,
            Name = control.Name,
            Text = control.Text ?? string.Empty,
            X = control.Location.X,
            Y = control.Location.Y,
            W = control.Width,
            H = control.Height,
            Z = control.ZOrder,
            Visible = control.Visible,
            Locked = surface.Locked.Contains(control),
            Dock = control.Dock.ToString(),
            Anchor = control.Anchor.ToString(),
            IsGroup = surface.Groups.Contains(control),
        };

        foreach (var c in control.Controls)
            if (c is ElementBase child and not ScrollBar)
                node.Children.Add(ToNode(child, surface));

        return node;
    }

    /// <summary>Loads a project into the surface, replacing current content. Returns skipped-type names.</summary>
    public static IReadOnlyList<string> Load(DesignSurface surface, string json)
    {
        var dto = JsonSerializer.Deserialize<DesignDocumentDto>(json, Options)
                  ?? throw new InvalidOperationException("Empty or invalid project file.");

        var catalog = ControlCatalog.Discover().ToDictionary(e => e.DisplayName, StringComparer.Ordinal);
        var skipped = new List<string>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        // Rebuild outside the undo stack, then reset history: a load is a new session baseline.
        foreach (var existing in surface.DesignedControls.ToList())
            surface.DesignRoot.Controls.Remove(existing);
        surface.Locked.Clear();
        surface.Groups.Clear();
        surface.Selection.Clear();

        if (dto.RootWidth > 0 && dto.RootHeight > 0)
            surface.DesignRoot.Size = new SKSize(dto.RootWidth, dto.RootHeight);

        foreach (var node in dto.Controls)
        {
            var control = FromNode(node, catalog, surface, skipped, usedNames);
            if (control != null)
                surface.DesignRoot.Controls.Add(control);
        }

        surface.Commands.Clear();
        return skipped;
    }

    private static ElementBase? FromNode(
        DesignDocumentDto.NodeDto node,
        Dictionary<string, ControlEntry> catalog,
        DesignSurface surface,
        List<string> skipped,
        HashSet<string> usedNames)
    {
        if (!catalog.TryGetValue(node.Type, out var entry))
        {
            skipped.Add(node.Type);
            return null;
        }

        var control = entry.CreateInstance();
        DesignSurface.PrepareForDesign(control);
        // A hand-edited project file isn't guaranteed to carry a valid or unique identifier the way
        // interactively-placed controls always are — see DesignNameValidator's remarks.
        control.Name = DesignNameValidator.Normalize(node.Name, node.Type, usedNames);
        control.Text = node.Text;
        if (Enum.TryParse<DockStyle>(node.Dock, out var dock))
            control.Dock = dock;
        if (Enum.TryParse<AnchorStyles>(node.Anchor, out var anchor))
            control.Anchor = anchor;
        control.Location = new SKPoint(node.X, node.Y);
        control.Size = new SKSize(node.W, node.H);
        control.ZOrder = node.Z;
        control.Visible = node.Visible;
        if (node.Locked)
            surface.Locked.Add(control);
        if (node.IsGroup)
            surface.Groups.Add(control);

        foreach (var childNode in node.Children)
        {
            var child = FromNode(childNode, catalog, surface, skipped, usedNames);
            if (child != null)
                control.Controls.Add(child);
        }

        return control;
    }
}
