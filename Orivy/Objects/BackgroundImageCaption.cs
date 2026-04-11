using System;

namespace Orivy;

public sealed class BackgroundImageCaption
{
    public static BackgroundImageCaption Empty { get; } = new(string.Empty, string.Empty);

    public BackgroundImageCaption(string caption, string summary = "")
    {
        Caption = caption ?? string.Empty;
        Summary = summary ?? string.Empty;
    }

    public string Caption { get; }

    public string Summary { get; }

    public bool IsEmpty => Caption.Length == 0 && Summary.Length == 0;

    public override string ToString()
    {
        if (Summary.Length == 0)
            return Caption;

        if (Caption.Length == 0)
            return Summary;

        return $"{Caption}{Environment.NewLine}{Summary}";
    }
}