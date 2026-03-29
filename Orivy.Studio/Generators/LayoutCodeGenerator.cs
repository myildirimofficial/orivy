using System.Text;

namespace Orivy.Studio;

public sealed class LayoutCodeGenerator : ICodeGenerator
{
    public string Name => "LayoutCodeGenerator";
    public string Description => "Generate layout definitions for the studio canvas.";
    public string OutputFileName => "Studio.Layout.cs";

    public string GenerateFile(StudioProject project)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Auto-generated layout file");
        builder.AppendLine($"// Project: {project.Name}");
        builder.AppendLine();
        builder.AppendLine("namespace Orivy.Studio.Generated;");
        builder.AppendLine();
        builder.AppendLine("public static class LayoutFactory");
        builder.AppendLine("{");
        builder.AppendLine("    public static void ApplyDefaultLayout(Element page)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (page == null) return;");
        builder.AppendLine("        page.Padding = new Thickness(14);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }
}
