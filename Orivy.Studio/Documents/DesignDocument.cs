using Orivy;
using Orivy.Controls;

namespace Orivy.Studio.Documents;

/// <summary>
/// One open design document (a "Window" being designed). It is a <see cref="Container"/> so it can
/// live as a page inside the shell's <see cref="TabView"/>, and it hosts a single
/// <see cref="DesignSurface"/> filling the page. The tab title is this container's <c>Text</c>.
/// </summary>
public sealed class DesignDocument : Container
{
    public DesignDocument(string title)
    {
        Text = title;
        Surface = new DesignSurface { Dock = DockStyle.Fill };
        Controls.Add(Surface);
    }

    public DesignSurface Surface { get; }

    /// <summary>Backing project file path, or null if never saved.</summary>
    public string? FilePath { get; set; }
}
