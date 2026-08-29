using Orivy.Controls;
using Orivy.Controls.RichText;
using SkiaSharp;
using System;
using System.IO;

namespace Orivy.Studio.Documents;

/// <summary>
/// A plain-text/code editor tab. Any file that isn't a <c>.orivy.json</c> design — a hand-written or
/// Orivy-generated <c>Designer.cs</c>, or really any other text file in the browsed folder — opens
/// here for direct editing and saving, with no attempt to parse or understand it (that's what
/// <c>File ▸ Import Designer Code…</c> is for, as a separate, explicit action).
/// </summary>
public sealed class TextFileDocument : Container, IStudioDocument
{
    private readonly RichTextBox _editor;
    private string _documentName;
    private bool _dirty;
    private bool _suppressDirty;

    public TextFileDocument(string documentName)
    {
        _documentName = documentName;

        _editor = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = false,
            Margin = new Thickness(16),
            Font = new SKFont(SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default, 11f),
        };
        _editor.TextChanged += (_, _) =>
        {
            if (_suppressDirty || _dirty)
                return;
            _dirty = true;
            UpdateTabText();
            DirtyChanged?.Invoke();
        };

        Controls.Add(_editor);
        UpdateTabText();
    }

    public string? FilePath { get; set; }

    public bool IsDirty => _dirty;

    public event Action? DirtyChanged;

    public string DocumentName => _documentName;

    /// <summary>The editor's current text — read to save, written to load (without marking dirty).</summary>
    public string Content
    {
        get => _editor.Text;
        set
        {
            _suppressDirty = true;
            try { _editor.Text = value; }
            finally { _suppressDirty = false; }
        }
    }

    /// <summary>Renames the document (e.g. after a reload from a different path) without touching
    /// its dirty state.</summary>
    public void Rename(string documentName)
    {
        _documentName = documentName;
        UpdateTabText();
    }

    public void Save()
    {
        if (FilePath == null)
            throw new InvalidOperationException("This document has no file path to save to yet.");

        File.WriteAllText(FilePath, _editor.Text);
        MarkClean();
    }

    public void MarkClean()
    {
        if (!_dirty)
            return;

        _dirty = false;
        UpdateTabText();
        DirtyChanged?.Invoke();
    }

    // A trailing dot-marker distinguishes an unsaved tab without disturbing DocumentName, which
    // stays a clean file name for anything that needs it verbatim (status messages, dialogs).
    private void UpdateTabText() => Text = _dirty ? $"{_documentName} •" : _documentName;

    /// <summary>Closing a tab never cascades dispose to its children in this framework — without this
    /// override the editor's own native Skia font/paints would leak every time a text tab is closed.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _editor.Dispose();

        base.Dispose(disposing);
    }
}
