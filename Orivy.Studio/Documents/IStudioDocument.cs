using System;

namespace Orivy.Studio.Documents;

/// <summary>
/// A tab that represents an editable file on disk — implemented by both the visual
/// <see cref="DesignDocument"/> (which saves/loads as plain Designer C# code, the exact same format
/// <c>Export</c>/<c>Import Designer Code…</c> use — no separate project format of its own) and the
/// plain-text <see cref="TextFileDocument"/> (everything else). The shell's tab management —
/// open/close/save, and warning about unsaved changes before closing — is written against this
/// interface once instead of duplicating it per document kind.
/// </summary>
public interface IStudioDocument : IDisposable
{
    /// <summary>Backing file path, or null if this document has never been saved.</summary>
    string? FilePath { get; set; }

    /// <summary>True if there are edits since the last load/save.</summary>
    bool IsDirty { get; }

    /// <summary>A short display name for this document, independent of the tab's visible <c>Text</c>
    /// (which may carry a dirty-state suffix, and, for a design document, must NOT collide with the
    /// generated-code class name).</summary>
    string DocumentName { get; }

    /// <summary>Raised whenever <see cref="IsDirty"/> changes value, in either direction — used to
    /// "promote" the shell's single preview tab to permanent the moment it's actually edited.</summary>
    event Action? DirtyChanged;

    /// <summary>Writes this document's current content to <see cref="FilePath"/>, which must already
    /// be set, and clears the dirty flag.</summary>
    void Save();

    /// <summary>Clears the dirty flag without saving — for use right after a fresh load.</summary>
    void MarkClean();
}
