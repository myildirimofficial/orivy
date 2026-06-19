using Orivy.Controls;
using System.Threading.Tasks;

namespace Orivy;

public static class MessageBox
{
    public static DialogResult Show(string message)
        => Show(null, message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string message, string caption)
        => Show(null, message, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons)
        => Show(null, message, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(null, message, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => Show(null, message, caption, buttons, icon, defaultButton);

    public static DialogResult Show(WindowBase? owner, string message)
        => Show(owner, message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(WindowBase? owner, string message, string caption)
        => Show(owner, message, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(WindowBase? owner, string message, string caption, MessageBoxButtons buttons)
        => Show(owner, message, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(owner, message, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(string message)
        => ShowAsync(null, message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(string message, string caption)
        => ShowAsync(null, message, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(string message, string caption, MessageBoxButtons buttons)
        => ShowAsync(null, message, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => ShowAsync(null, message, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => ShowAsync(null, message, caption, buttons, icon, defaultButton);

    public static Task<DialogResult> ShowAsync(WindowBase? owner, string message)
        => ShowAsync(owner, message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(WindowBase? owner, string message, string caption)
        => ShowAsync(owner, message, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(WindowBase? owner, string message, string caption, MessageBoxButtons buttons)
        => ShowAsync(owner, message, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static Task<DialogResult> ShowAsync(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => ShowAsync(owner, message, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        return owner?.Notifications.ShowDialog(caption, message, buttons, icon, defaultButton) ?? 
            NotificationToast.Show(caption, message, buttons, icon);
    }

    public static Task<DialogResult> ShowAsync(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        return owner?.Notifications.ShowDialogAsync(caption, message, buttons, icon, defaultButton) ??
            NotificationToast.ShowAsync(caption, message, buttons, icon);
    }
}
