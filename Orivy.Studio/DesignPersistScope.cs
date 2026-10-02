using System;
using System.Collections.Generic;

namespace Orivy.Studio;

/// <summary>
/// Limits Save/Code sync to controls this designer file actually owns. Inner trees loaded from
/// another type's <c>.Designer.cs</c> are visible on the canvas but not selectable, persistable, or
/// nest targets from this file.
/// </summary>
internal static class DesignPersistScope
{
    internal static bool ShouldPersistControlToFile(DesignSurface surface, string controlKey)
    {
        if (controlKey.Length == 0)
            return true;

        if (surface.AddedControlNames.Contains(controlKey))
            return true;

        if (surface.ExternallyComposedControlNames.Contains(controlKey))
            return false;

        return surface.FilePersistedControlNames.Contains(controlKey);
    }
}
