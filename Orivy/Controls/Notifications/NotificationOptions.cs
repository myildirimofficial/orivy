using Orivy;
using System;

namespace Orivy.Controls;

public sealed class NotificationOptions
{
    public int DurationMs { get; set; } = 4000;
    public bool ShowProgressBar { get; set; } = true;
    public float? Progress { get; set; }
    public NotificationAction[] Actions { get; set; } = Array.Empty<NotificationAction>();
    public NotificationToastPalette? CustomPalette { get; set; }
    public ContentAlignment? Position { get; set; }
    public NotificationToastLayoutMode? LayoutMode { get; set; }
    public NotificationToastPresentationMode PresentationMode { get; set; } = NotificationToastPresentationMode.Toast;
}