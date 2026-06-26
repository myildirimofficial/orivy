using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Threading.Tasks;

#nullable disable

namespace Orivy.Example;

internal sealed partial class NotificationsDemoPage : Container
{
    private bool _notificationStackModeEnabled;
    private NotificationHandle _manualProgressToast;
    private SKImage _tabIcon;

    public NotificationsDemoPage()
    {
        InitializeComponent();
    }

    public override void  Dispose(bool disposing)
    {
        if (disposing)
        {
            _tabIcon?.Dispose();
            _tabIcon = null;
        }

        base.Dispose(disposing);
    }

    private void NotifBtnInfo_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Information",
            "The operation completed successfully. No further action is required. This is a longer message to demonstrate text wrapping behavior in the notification layout.",
            NotificationKind.Info);

    private void NotifBtnSuccess_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Deployment Successful",
            "The build artifact has been deployed to the staging environment and all health probes are green.",
            NotificationKind.Success);

    private void NotifBtnWarning_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "High Latency Detected",
            "Response times on the Telemetry workload have exceeded the 40 ms threshold for the last three checks.",
            NotificationKind.Warning);

    private void NotifBtnError_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Connection Failed",
            "Unable to establish a connection to the remote endpoint. Check network configuration and try again.",
            NotificationKind.Error);

    private void NotifBtnAllFour_Click(object sender, EventArgs e)
    {
        NotificationToast.Show("Information", "Background sync completed with no conflicts detected.", NotificationKind.Info, 4000);
        NotificationToast.Show("Changes Saved", "Your configuration has been written to disk and is active immediately.", NotificationKind.Success, 5000);
        NotificationToast.Show("Token Expiring Soon", "Your session token will expire in 15 minutes. Save your work before it does.", NotificationKind.Warning, 6000);
        NotificationToast.Show("Render Error", "The DirectX 11 context was lost. The renderer has fallen back to software mode.", NotificationKind.Error, 7000);
    }

    private void NotifBtnDismissAll_Click(object sender, EventArgs e)
        => NotificationToast.DismissAll();

    private void NotifBtnLongMessage_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Audit Trail Delayed",
            "The retention sweep has been postponed because the archive lane is warming up.\nEstimated completion: 3�5 minutes.\nNo data will be lost during this window.",
            NotificationKind.Warning,
            6000);

    private void NotifBtnLongDuration_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Background Task Running",
            "An 8-second background task is in progress. Hover to pause the countdown.",
            NotificationKind.Info,
            8000);

    private async void NotifBtnConfirm_Click(object sender, EventArgs e)
    {
        var result = await NotificationToast.ConfirmAsync(
            "Delete Workload",
            "This will permanently remove the selected workload. This action cannot be undone.",
            NotificationKind.Warning,
            0,
            "Delete", "Cancel");

        if (result == "Delete")
            NotificationToast.Show("Deleted", "The workload has been permanently removed.", NotificationKind.Success, 3000);
        else if (result == "Cancel")
            NotificationToast.Show("Cancelled", "No changes were made.", NotificationKind.Info, 2500);
    }

    private void NotifBtnActions_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Update Available",
            "Version 2.4.1 is ready to install. It includes performance improvements and security patches.",
            NotificationKind.Info,
            0,
            new NotificationAction("Install Now", () =>
            NotificationToast.Show("Installing", "Updating to v2.4.1 in the background!", NotificationKind.Info, 3000)),
            new NotificationAction("Later"));

    private async void NotifBtnManualProgress_Click(object sender, EventArgs e)
    {
        _manualProgressToast?.Dismiss();
        _manualProgressToast = NotificationToast.Show(
            "Publishing Build",
            "Pushing artifacts to the release channel and verifying checksum state.",
            NotificationKind.Info,
            new NotificationOptions
            {
            LayoutMode = NotificationToastLayoutMode.List,
            DurationMs = 0,
            ShowProgressBar = true,
            Progress = 0f,
            Actions = [new NotificationAction("Hide", () => _manualProgressToast?.Dismiss())]
            });

        for (var i = 1; i <= 10; i++)
        {
            await Task.Delay(220);
            _manualProgressToast?.SetProgress(i / 10f);
        }

        await Task.Delay(180);
        _manualProgressToast?.Dismiss();
        _manualProgressToast = null;
        NotificationToast.Show("Release Ready", "Build publishing completed successfully.", NotificationKind.Success, 2600);
    }

    private async void NotifBtnProgressToggle_Click(object sender, EventArgs e)
    {
        var toast = NotificationToast.Show(
            "Background Indexing",
            "Collecting symbols and warming the query cache in the background.",
            NotificationKind.Info,
            new NotificationOptions
            {
            LayoutMode = NotificationToastLayoutMode.List,
            DurationMs = 0,
            ShowProgressBar = false,
            Progress = 0.18f,
            });

        await Task.Delay(700);
        toast.SetProgressVisible(true);

        for (var i = 2; i <= 9; i++)
        {
            await Task.Delay(140);
            toast.SetProgress(i / 10f);
        }

        await Task.Delay(320);
        toast.Dismiss();
    }

    private void NotifBtnThemeAuto_Click(object sender, EventArgs e)
        => ShowNotificationThemeModeExample(
            NotificationKind.Info,
            "Auto Theme",
            "This toast resolves its palette from the current application theme. Toggle dark mode and trigger it again to compare the result.");

    private void NotifBtnThemeLight_Click(object sender, EventArgs e)
        => ShowNotificationThemeModeExample(
            NotificationKind.Light,
            "Light Theme",
            "This toast forces the light palette regardless of the current window theme.");

    private void NotifBtnThemeDark_Click(object sender, EventArgs e)
        => ShowNotificationThemeModeExample(
            NotificationKind.Dark,
            "Dark Theme",
            "This toast forces the dark palette even if the rest of the sample window is currently light.");

    private void NotifBtnThemeCustom_Click(object sender, EventArgs e)
        => NotificationToast.Show(
            "Custom Theme",
            "Custom mode uses an explicit NotificationToastPalette so background, accent and foreground colors can be branded per toast.",
            NotificationKind.Custom,
            new NotificationOptions
            {
            DurationMs = 5200,
            ShowProgressBar = true,
            CustomPalette = CreateNotificationThemeModePalette(),
            Actions =
            [
                new NotificationAction("Apply Globally", ApplyCustomNotificationThemeMode),
                new NotificationAction("Reset", ResetNotificationThemeModeDefaults),
            ]
            });

    private void NotifBtnTopLeft_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.TopLeft, "Top Left");

    private void NotifBtnTopCenter_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.TopCenter, "Top Center");

    private void NotifBtnTopRight_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.TopRight, "Top Right");

    private void NotifBtnBottomLeft_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.BottomLeft, "Bottom Left");

    private void NotifBtnBottomCenter_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.BottomCenter, "Bottom Center");

    private void NotifBtnBottomRight_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.BottomRight, "Bottom Right");

    private void NotifBtnCenter_Click(object sender, EventArgs e)
        => ShowNotificationAtPosition(ContentAlignment.MiddleCenter, "Center");

    private void NotifBtnStackMode_Click(object sender, EventArgs e)
    {
        _notificationStackModeEnabled = !_notificationStackModeEnabled;
        NotificationToast.DefaultLayoutMode = _notificationStackModeEnabled
            ? NotificationToastLayoutMode.Stack
            : NotificationToastLayoutMode.List;
        RefreshNotificationStackModeButton();

        NotificationToast.Show(
            "Stack Mode Updated",
            _notificationStackModeEnabled
            ? "New notifications now use stacked presentation by default. Click the front toast to reveal the ones behind it."
            : "New notifications now use the standard list layout again.",
            NotificationKind.Info,
            3200);
    }

    private void NotifBtnDialog_Click(object sender, EventArgs e){
        var toast = NotificationToast.ShowDialog(
            "Dialog Presentation",
            "Dialog mode centers the notification, adds a scrim and keeps the action area readable for confirm-style flows.",
            NotificationKind.Info,
            new NotificationOptions
            {
            DurationMs = 0,
            ShowProgressBar = false,
            Actions =
            [
                new NotificationAction("Primary"),
                new NotificationAction("Close")
            ]
            });
    }

    private void ShowNotificationAtPosition(
        ContentAlignment position,
        string label)
    {
        var layoutLabel = _notificationStackModeEnabled ? "stack" : "list";
        NotificationToast.Show(
            label,
            $"Toasts can anchor to {label.ToLowerInvariant()} using the {layoutLabel} layout. Different positions and layouts run in separate trays.",
            NotificationKind.Info,
            new NotificationOptions
            {
            DurationMs = 4000,
            ShowProgressBar = true,
            Position = position,
            });
    }

    private void RefreshNotificationStackModeButton()
    {
        if (notifBtnStackMode == null)
            return;

        notifBtnStackMode.Text = _notificationStackModeEnabled ? "Stack Mode: On" : "Stack Mode: Off";
    }

    private void ShowNotificationThemeModeExample(NotificationKind kind, string title, string message)
    {
        NotificationToast.Show(
            title,
            message,
            kind,
            new NotificationOptions
            {
            DurationMs = 4600,
            ShowProgressBar = true,
            CustomPalette = kind == NotificationKind.Custom ? CreateNotificationThemeModePalette() : null,
            });
    }

    private static NotificationToastPalette CreateNotificationThemeModePalette()
        => new(
            new SKColor(12, 33, 60),
            new SKColor(56, 189, 248),
            new SKColor(240, 249, 255));

    private void ApplyCustomNotificationThemeMode()
    {
        NotificationToast.CustomPalette = CreateNotificationThemeModePalette();

        NotificationToast.Show(
            "Custom Default Active",
            "NotificationKind.Custom and NotificationToast.CustomPalette now point to the custom sample palette for subsequent toasts.",
            NotificationKind.Custom,
            4200);
    }

    private void ResetNotificationThemeModeDefaults()
    {
        NotificationToast.CustomPalette = null;

        NotificationToast.Show(
            "Theme Defaults Reset",
            "Notification toasts now resolve from Auto mode again.",
            NotificationKind.Info,
            3200);
    }

    private void NotifBtnMessageBox_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show("Hello from MessageBox!", "Test", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Information);
        NotificationToast.Show(
            "MessageBox Result",
            $"You clicked {result}. MessageBox is modal and blocks interaction with the underlying window until dismissed.",
            NotificationKind.Info,
            4000);
    }
}
