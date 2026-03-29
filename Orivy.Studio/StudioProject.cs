using SkiaSharp;

namespace Orivy.Studio;

public sealed class StudioProject
{
    public string Name { get; set; }
    public string Description { get; set; }
    public SKColor PrimaryColor { get; set; }
    public bool HasResponsiveGrid { get; set; }

    public StudioProject(string name, string description)
    {
        Name = name;
        Description = description;
        PrimaryColor = new SKColor(12, 94, 116);
        HasResponsiveGrid = true;
    }
}