namespace Orivy.Studio;

public interface ICodeGenerator
{
    string Name { get; }
    string Description { get; }
    string OutputFileName { get; }

    string GenerateFile(StudioProject project);
}