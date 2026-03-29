using System.Text;

namespace Orivy.Studio;

public sealed class UIComponentGenerator : ICodeGenerator
{
    public string Name => "UIComponentGenerator";
    public string Description => "Generate UI element definitions from the studio model.";
    public string OutputFileName => "Studio.UIComponent.cs";

    public string GenerateFile(StudioProject project)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Auto-generated UI component file");
        builder.AppendLine($"// Project: {project.Name}");
        builder.AppendLine("using Orivy.Controls;");
        builder.AppendLine();
        builder.AppendLine("namespace Orivy.Studio.Generated;");
        builder.AppendLine();
        builder.AppendLine("public static class UIComponentFactory");
        builder.AppendLine("{");
        builder.AppendLine("    public static Element CreateMainCanvas() => new Element");
        builder.AppendLine("    {");
        builder.AppendLine("        Name = \"MainCanvas\", ");
        builder.AppendLine("        BackColor = SKColors.WhiteSmoke, ");
        builder.AppendLine("        Dock = DockStyle.Fill");
        builder.AppendLine("    }; ");
        builder.AppendLine("}");

        return builder.ToString();
    }
}
