using Orivy.Controls;
using System.Threading.Tasks;

namespace Orivy;

public static class MessageBox
{
    public static DialogResult Show(string message)
        => ShowAsync(null, message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(string message, string caption)
        => ShowAsync(null, message, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons)
        => ShowAsync(null, message, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => ShowAsync(null, message, caption, buttons, icon, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => ShowAsync(null, message, caption, buttons, icon, defaultButton).GetAwaiter().GetResult();

    public static DialogResult Show(WindowBase? owner, string message)
        => ShowAsync(owner, message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(WindowBase? owner, string message, string caption)
        => ShowAsync(owner, message, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(WindowBase? owner, string message, string caption, MessageBoxButtons buttons)
        => ShowAsync(owner, message, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => ShowAsync(owner, message, caption, buttons, icon, MessageBoxDefaultButton.Button1).GetAwaiter().GetResult();

    public static DialogResult Show(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => ShowAsync(owner, message, caption, buttons, icon, defaultButton).GetAwaiter().GetResult();

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

    public static Task<DialogResult> ShowAsync(WindowBase? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        var tcs = new TaskCompletionSource<DialogResult>();
        var actions = BuildActions(buttons, tcs);
        var kind = ConvertToNotificationKind(icon);

        NotificationToast.Show(caption, message, kind, 0, actions);

        return tcs.Task;
    }

    private static NotificationKind ConvertToNotificationKind(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Error => NotificationKind.Error,
            MessageBoxIcon.Warning => NotificationKind.Warning,
            MessageBoxIcon.Question => NotificationKind.Info,
            MessageBoxIcon.Information => NotificationKind.Info,
            _ => NotificationKind.Info
        };
    }

    private static NotificationAction[] BuildActions(MessageBoxButtons buttons, TaskCompletionSource<DialogResult> tcs)
    {
        switch (buttons)
        {
            case MessageBoxButtons.OK:
                return [new NotificationAction("OK", () => tcs.SetResult(DialogResult.OK))];
            case MessageBoxButtons.OKCancel:
                return [
                    new NotificationAction("OK", () => tcs.SetResult(DialogResult.OK)),
                    new NotificationAction("Cancel", () => tcs.SetResult(DialogResult.Cancel))
                ];
            case MessageBoxButtons.AbortRetryIgnore:
                return [
                    new NotificationAction("Abort", () => tcs.SetResult(DialogResult.Abort)),
                    new NotificationAction("Retry", () => tcs.SetResult(DialogResult.Retry)),
                    new NotificationAction("Ignore", () => tcs.SetResult(DialogResult.Ignore))
                ];
            case MessageBoxButtons.YesNoCancel:
                return [
                    new NotificationAction("Yes", () => tcs.SetResult(DialogResult.Yes)),
                    new NotificationAction("No", () => tcs.SetResult(DialogResult.No)),
                    new NotificationAction("Cancel", () => tcs.SetResult(DialogResult.Cancel))
                ];
            case MessageBoxButtons.YesNo:
                return [
                    new NotificationAction("Yes", () => tcs.SetResult(DialogResult.Yes)),
                    new NotificationAction("No", () => tcs.SetResult(DialogResult.No))
                ];
            case MessageBoxButtons.RetryCancel:
                return [
                    new NotificationAction("Retry", () => tcs.SetResult(DialogResult.Retry)),
                    new NotificationAction("Cancel", () => tcs.SetResult(DialogResult.Cancel))
                ];
            default:
                return [new NotificationAction("OK", () => tcs.SetResult(DialogResult.OK))];
        }
    }
}
