using Orivy;
using Orivy.Animation;
using Orivy.Controls;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class NotificationsDemoPage
{
    private Container notificationPage = null!;

    private Button notifBtnInfo = null!;
    private Button notifBtnSuccess = null!;
    private Button notifBtnWarning = null!;
    private Button notifBtnError = null!;
    private Button notifBtnAllFour = null!;
    private Button notifBtnDismissAll = null!;
    private Button notifBtnLongMessage = null!;
    private Button notifBtnLongDuration = null!;
    private Button notifBtnConfirm = null!;
    private Button notifBtnActions = null!;
    private Button notifBtnManualProgress = null!;
    private Button notifBtnProgressToggle = null!;
    private Button notifBtnThemeAuto = null!;
    private Button notifBtnThemeLight = null!;
    private Button notifBtnThemeDark = null!;
    private Button notifBtnThemeCustom = null!;
    private Button notifBtnTopLeft = null!;
    private Button notifBtnTopCenter = null!;
    private Button notifBtnTopRight = null!;
    private Button notifBtnBottomLeft = null!;
    private Button notifBtnBottomCenter = null!;
    private Button notifBtnBottomRight = null!;
    private Button notifBtnCenter = null!;
    private Button notifBtnStackMode = null!;
    private Button notifBtnDialog = null!;
    private Button notifBtnMessageBox = null!;

    private void InitializeComponent()
    {
        Text = "Notifications";
        _tabIcon = ExampleHelper.CreateIcon(new SKColor(0xEF, 0x44, 0x44), ExampleIconKind.Warning);
        Image = _tabIcon;

        notificationPage = new Container
        {
            Name = "notificationsPage",
            Text = "Notifications",
            Padding = new Thickness(28),
            Dock = DockStyle.Fill,
            Radius = new Radius(0),
            Border = new Thickness(0),
        };

        var header = CreateHeader();
        var basicsRow = CreateRow(out notifBtnInfo, "Info", 120, out notifBtnSuccess, "Success", 120, out notifBtnWarning, "Warning", 120, out notifBtnError, "Error", 120);
        var batchRow = CreateRow(out notifBtnAllFour, "Show All Four", 148, out notifBtnDismissAll, "Dismiss All", 120);
        var timingRow = CreateRow(out notifBtnLongMessage, "Long Message", 148, out notifBtnLongDuration, "8-Second Timer", 148);
        var actionsRow = CreateRow(out notifBtnConfirm, "Confirm Dialog", 148, out notifBtnActions, "With Actions", 148);
        var progressRow = CreateRow(out notifBtnManualProgress, "Manual Progress", 156, out notifBtnProgressToggle, "Toggle Progress", 148);
        var themeRow = CreateRow(out notifBtnThemeAuto, "Auto Theme", 122, out notifBtnThemeLight, "Light Theme", 122, out notifBtnThemeDark, "Dark Theme", 122, out notifBtnThemeCustom, "Custom Theme", 132);
        var positionLabel = CreateSectionLabel("Position", "Toast tray alignment and stack/list behavior.");
        var topPositionRow = CreateRow(out notifBtnTopLeft, "Top Left", 112, out notifBtnTopCenter, "Top Center", 120, out notifBtnTopRight, "Top Right", 112);
        var bottomPositionRow = CreateRow(out notifBtnBottomLeft, "Bottom Left", 120, out notifBtnBottomCenter, "Bottom Center", 134, out notifBtnBottomRight, "Bottom Right", 128);
        var layoutRow = CreateRow(out notifBtnCenter, "Center", 112, out notifBtnStackMode, "Stack Mode: Off", 144, out notifBtnDialog, "Dialog Toast", 128);
        var messageBoxRow = CreateRow(out notifBtnMessageBox, "MessageBox", 148, out notifBtnDismissAll, "Dismiss All", 120);

        ConfigureSemanticButton(notifBtnSuccess, new SKColor(22, 163, 74), new SKColor(34, 197, 94));
        ConfigureSemanticButton(notifBtnWarning, new SKColor(202, 138, 4), new SKColor(234, 179, 8));
        ConfigureSemanticButton(notifBtnError, ColorScheme.Error, ColorScheme.Error.Brightness(0.06f));
        ConfigureSurfaceButton(notifBtnDismissAll);
        ConfigurePrimaryButton(notifBtnConfirm);
        ConfigurePrimaryButton(notifBtnBottomRight);
        ConfigureStackButton(notifBtnStackMode);
        ConfigureDialogButton(notifBtnDialog);
        ConfigureSurfaceButton(notifBtnMessageBox);

        AddTopDown(notificationPage,
            messageBoxRow,
            layoutRow,
            bottomPositionRow,
            topPositionRow,
            positionLabel,
            themeRow,
            progressRow,
            actionsRow,
            timingRow,
            batchRow,
            basicsRow,
            header);

        Controls.Add(notificationPage);
        notificationPage.BringToFront();
        RefreshNotificationStackModeButton();
        PerformLayout();
        Invalidate();

        WireEvents();
    }

    private static Card CreateHeader()
    {
        var header = new Card
        {
            Title = "Notification Surface",
            Description = "Alert-style toasts, global stack mode, dialog presentation, center positioning, inline actions, theme modes and the manual progress API are all demonstrated on this page.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 20),
            Radius = new Radius(16),
        };

        return header;
    }

    private static Element CreateSectionLabel(string title, string body)
    {
        return new Element
        {
            Text = $"{title}\n{body}",
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(0, 4, 0, 8),
            Radius = new Radius(0),
            Border = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(ColorScheme.IsDarkMode ? (byte)180 : (byte)160),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 10f),
        };
    }

    private static Container CreateRow(params Button[] buttons)
    {
        var row = new Container
        {
            Dock = DockStyle.Top,
            Height = 46,
            Margin = new Thickness(0, 0, 0, 10),
            Radius = new Radius(0),
            Border = new Thickness(0),
            BackColor = SKColors.Transparent,
        };

        AddButtons(row, buttons);
        return row;
    }

    private static Container CreateRow(out Button first, string firstText, int firstWidth, out Button second, string secondText, int secondWidth)
    {
        first = CreateButton(firstText, firstWidth);
        second = CreateButton(secondText, secondWidth, trailingMargin: false);
        return CreateRow(first, second);
    }

    private static Container CreateRow(out Button first, string firstText, int firstWidth, out Button second, string secondText, int secondWidth, out Button third, string thirdText, int thirdWidth)
    {
        first = CreateButton(firstText, firstWidth);
        second = CreateButton(secondText, secondWidth);
        third = CreateButton(thirdText, thirdWidth, trailingMargin: false);
        return CreateRow(first, second, third);
    }

    private static Container CreateRow(out Button first, string firstText, int firstWidth, out Button second, string secondText, int secondWidth, out Button third, string thirdText, int thirdWidth, out Button fourth, string fourthText, int fourthWidth)
    {
        first = CreateButton(firstText, firstWidth);
        second = CreateButton(secondText, secondWidth);
        third = CreateButton(thirdText, thirdWidth);
        fourth = CreateButton(fourthText, fourthWidth, trailingMargin: false);
        return CreateRow(first, second, third, fourth);
    }

    private static Button CreateButton(string text, int width, bool trailingMargin = true)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Left,
            Width = width,
            Height = 38,
            Margin = trailingMargin ? new Thickness(0, 0, 10, 0) : new Thickness(0),
        };
    }

    private static void AddButtons(Container row, params Button[] buttons)
    {
        for (var i = buttons.Length - 1; i >= 0; i--)
            row.Controls.Add(buttons[i]);
    }

    private static void AddTopDown(Container parent, params ElementBase[] controls)
    {
        for (var i = 0; i < controls.Length; i++)
            parent.Controls.Add(controls[i]);
    }

    private static void ConfigureSemanticButton(Button button, SKColor baseColor, SKColor hoverColor)
    {
        button.ConfigureVisualStyles(s => s
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(baseColor)
                .Foreground(SKColors.White)
                .Border(1)
                .BorderColor(baseColor.Brightness(-0.18f))
                .Radius(12)
                .Shadow(new BoxShadow(0f, 6f, 14f, 0, ColorScheme.ShadowColor.WithAlpha(26))))
            .OnHover(r => r
                .Background(hoverColor)
                .BorderColor(baseColor)));
    }

    private static void ConfigureSurfaceButton(Button button)
    {
        button.ConfigureVisualStyles(s => s
            .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
            .Base(b => b
                .Background(ColorScheme.SurfaceVariant)
                .Foreground(ColorScheme.ForeColor)
                .Border(1)
                .BorderColor(ColorScheme.Outline)
                .Radius(12))
            .OnHover(r => r
                .Background(ColorScheme.SurfaceVariant.Brightness(0.06f))
                .BorderColor(ColorScheme.Primary)));
    }

    private static void ConfigurePrimaryButton(Button button)
    {
        button.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(ColorScheme.Primary)
                .Foreground(SKColors.White)));
    }

    private static void ConfigureStackButton(Button button)
    {
        button.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(new SKColor(15, 23, 42))
                .Foreground(SKColors.White)
                .Border(1)
                .BorderColor(new SKColor(51, 65, 85))
                .Radius(12))
            .OnHover(r => r
                .Background(new SKColor(30, 41, 59))
                .BorderColor(new SKColor(71, 85, 105))));
    }

    private static void ConfigureDialogButton(Button button)
    {
        button.ConfigureVisualStyles(s => s
            .Base(b => b
                .Background(ColorScheme.Primary.WithAlpha(220))
                .Foreground(SKColors.White)
                .Border(1)
                .BorderColor(ColorScheme.Primary)
                .Radius(12))
            .OnHover(r => r
                .Background(ColorScheme.Primary.Brightness(0.08f))
                .BorderColor(ColorScheme.Primary.Brightness(-0.08f))));
    }

    private void WireEvents()
    {
        notifBtnInfo.Click += NotifBtnInfo_Click;
        notifBtnSuccess.Click += NotifBtnSuccess_Click;
        notifBtnWarning.Click += NotifBtnWarning_Click;
        notifBtnError.Click += NotifBtnError_Click;
        notifBtnAllFour.Click += NotifBtnAllFour_Click;
        notifBtnDismissAll.Click += NotifBtnDismissAll_Click;
        notifBtnLongMessage.Click += NotifBtnLongMessage_Click;
        notifBtnLongDuration.Click += NotifBtnLongDuration_Click;
        notifBtnConfirm.Click += NotifBtnConfirm_Click;
        notifBtnActions.Click += NotifBtnActions_Click;
        notifBtnManualProgress.Click += NotifBtnManualProgress_Click;
        notifBtnProgressToggle.Click += NotifBtnProgressToggle_Click;
        notifBtnThemeAuto.Click += NotifBtnThemeAuto_Click;
        notifBtnThemeLight.Click += NotifBtnThemeLight_Click;
        notifBtnThemeDark.Click += NotifBtnThemeDark_Click;
        notifBtnThemeCustom.Click += NotifBtnThemeCustom_Click;
        notifBtnTopLeft.Click += NotifBtnTopLeft_Click;
        notifBtnTopCenter.Click += NotifBtnTopCenter_Click;
        notifBtnTopRight.Click += NotifBtnTopRight_Click;
        notifBtnBottomLeft.Click += NotifBtnBottomLeft_Click;
        notifBtnBottomCenter.Click += NotifBtnBottomCenter_Click;
        notifBtnBottomRight.Click += NotifBtnBottomRight_Click;
        notifBtnCenter.Click += NotifBtnCenter_Click;
        notifBtnStackMode.Click += NotifBtnStackMode_Click;
        notifBtnDialog.Click += NotifBtnDialog_Click;
        notifBtnMessageBox.Click += NotifBtnMessageBox_Click;
    }
}