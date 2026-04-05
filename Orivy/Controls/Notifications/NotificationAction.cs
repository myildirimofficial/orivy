using System;

namespace Orivy.Controls;

public sealed class NotificationAction
{
    private readonly Action _onClick;

    public NotificationAction(string label, Action? onClick = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty.", nameof(label));

        Label    = label;
        _onClick = onClick ?? (() => { });
    }

    public string Label     { get; }
    internal bool IsPrimary { get; set; }

    internal void Invoke() => _onClick();
}
