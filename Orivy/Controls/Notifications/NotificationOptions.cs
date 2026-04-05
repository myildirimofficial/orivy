using System;

namespace Orivy.Controls;

public sealed class NotificationOptions
{
    public int DurationMs { get; set; } = 4000;
    public bool ShowProgressBar { get; set; } = true;
    public float? Progress { get; set; }
    public NotificationAction[] Actions { get; set; } = Array.Empty<NotificationAction>();
}