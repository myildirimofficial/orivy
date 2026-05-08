using Orivy.Controls;
using Orivy.Windowing.Desktop.Windows;
using System;
using System.IO;
using System.Runtime.Versioning;

namespace Orivy.Example;

[SupportedOSPlatform("windows")]
internal partial class MainWindow
{
    private void ShowOpenFileSelectionDialog()
    {
        var dialog = new FileSelectionDialog
        {
            Title = "Select a file",
            InitialDirectory = ResolveDialogInitialDirectory(),
            DefaultExtension = "png"
        };
        dialog.Filters.Add(new FileDialogFilter("Images", "*.png", "*.jpg", "*.jpeg", "*.webp"));
        dialog.Filters.Add(new FileDialogFilter("Markdown", "*.md", "*.txt"));
        dialog.Filters.Add(new FileDialogFilter("All Files", "*.*"));

        var selections = dialog.ShowDialog(this);
        if (selections.Length == 0)
            return;

        NotificationToast.Show(
            "File Selected",
            $"{Path.GetFileName(selections[0])}\n{selections[0]}",
            NotificationKind.Success,
            4200);
    }

    private void ShowSaveFileSelectionDialog()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save a file",
            InitialDirectory = ResolveDialogInitialDirectory(),
            DefaultExtension = "txt",
            FileName = "orivy-sample.txt"
        };
        dialog.Filters.Add(new FileDialogFilter("Text", "*.txt", "*.md"));
        dialog.Filters.Add(new FileDialogFilter("Images", "*.png", "*.jpg", "*.jpeg"));
        dialog.Filters.Add(new FileDialogFilter("All Files", "*.*"));

        var selection = dialog.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(selection))
            return;

        NotificationToast.Show(
            "Save Target Selected",
            $"{Path.GetFileName(selection)}\n{selection}",
            NotificationKind.Success,
            4200);
    }

    private void ShowOpenFolderSelectionDialog()
    {
        var dialog = new FolderSelectionDialog
        {
            Title = "Select a folder",
            InitialDirectory = ResolveDialogInitialDirectory()
        };

        var selection = dialog.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(selection))
            return;

        NotificationToast.Show(
            "Folder Selected",
            $"{Path.GetFileName(selection.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}\n{selection}",
            NotificationKind.Info,
            4200);
    }

    private static string ResolveDialogInitialDirectory()
    {
        var assetDirectory = Path.Combine(AppContext.BaseDirectory, "assets");
        if (Directory.Exists(assetDirectory))
            return assetDirectory;

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}