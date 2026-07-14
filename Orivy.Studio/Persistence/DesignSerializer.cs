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
    public int Version { get; set; } = 1;
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
        {
            dto.Controls.Add(new DesignDocumentDto.NodeDto
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
            });
        }

        return JsonSerializer.Serialize(dto, Options);
    }

    /// <summary>Loads a project into the surface, replacing current content. Returns skipped-type names.</summary>
    public static IReadOnlyList<string> Load(DesignSurface surface, string json)
    {
        var dto = JsonSerializer.Deserialize<DesignDocumentDto>(json, Options)
                  ?? throw new InvalidOperationException("Empty or invalid project file.");

        var catalog = ControlCatalog.Discover().ToDictionary(e => e.DisplayName, StringComparer.Ordinal);
        var skipped = new List<string>();

        // Rebuild outside the undo stack, then reset history: a load is a new session baseline.
        foreach (var existing in surface.DesignedControls.ToList())
            surface.DesignRoot.Controls.Remove(existing);
        surface.Locked.Clear();
        surface.Selection.Clear();

        if (dto.RootWidth > 0 && dto.RootHeight > 0)
            surface.DesignRoot.Size = new SKSize(dto.RootWidth, dto.RootHeight);

        foreach (var node in dto.Controls)
        {
            if (!catalog.TryGetValue(node.Type, out var entry))
            {
                skipped.Add(node.Type);
                continue;
            }

            var control = entry.CreateInstance();
            control.Name = node.Name;
            control.Text = node.Text;
            control.Location = new SKPoint(node.X, node.Y);
            control.Size = new SKSize(node.W, node.H);
            control.ZOrder = node.Z;
            control.Visible = node.Visible;
            surface.DesignRoot.Controls.Add(control);
            if (node.Locked)
                surface.Locked.Add(control);
        }

        surface.Commands.Clear();
        return skipped;
    }
}
