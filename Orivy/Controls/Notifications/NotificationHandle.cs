using System;

namespace Orivy.Controls;

public sealed class NotificationHandle
{
    private NotificationToast? _toast;

    internal NotificationHandle(NotificationToast toast)
    {
        _toast = toast;
    }

    public bool IsActive => _toast != null;

    public void Dismiss()
    {
        _toast?.BeginDismiss();
    }

    public void SetProgress(float progress)
    {
        if (progress < 0f || progress > 1f)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 1.");

        _toast?.SetManualProgress(progress);
    }

    public void ClearProgress()
    {
        _toast?.SetManualProgress(null);
    }

    public void SetProgressVisible(bool visible)
    {
        _toast?.SetProgressVisible(visible);
    }

    internal void Detach()
    {
        _toast = null;
    }
}