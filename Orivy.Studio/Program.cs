using Orivy;
using System;

namespace Orivy.Studio;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.Run(new StudioWindow());
    }
}
