using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.SettingsPreview;

internal sealed partial class SettingsPreviewWindow
{
    private readonly List<SKImage> _ownedImages = new();
    private SplitContainer shell = null!;
    private Element sidebar = null!;
    private Element content = null!;
    private ButtonGroup<string> navigation = null!;

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Settings";
        Name = "SettingsPreviewWindow";
        Width = 1180;
        Height = 760;
        MinimumSize = new SKSize(920, 620);
        FormStartPosition = FormStartPosition.CenterScreen;
        WindowThemeType = WindowThemeType.Mica;
        RenderBackend = Rendering.RenderBackend.Software;

        shell = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 282,
            SplitterWidth = 1,
            PanelMinSize = 238,
            Border = new Thickness(0, 0, 1, 0)
        };

        sidebar = BuildSidebar();
        content = BuildContent();

        shell.Panel1.Controls.Add(sidebar);
        shell.Panel2.Controls.Add(content);
        Controls.Add(shell);

        ResumeLayout(false);
    }

    private Element BuildSidebar()
    {
        var panel = new Element
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(22, 18, 18, 18),
            BackColor = new SKColor(0xF3, 0xF3, 0xF3, 50),
            Border = new Thickness(0)
        };

        var footer = new Card
        {
            Dock = DockStyle.Bottom,
            Height = 84,
            Padding = new Thickness(14),
            Margin = new Thickness(0, 14, 0, 0),
            Title = "Windows Update",
            Description = "Last checked today",
            HeaderGap = 0,
            Radius = new Radius(10)
        };
        footer.Controls.Add(new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 8,
            Value = 72,
            TextMode = ProgressBarTextMode.None,
            Mode = ProgressBarMode.Gradient,
            ForeColor = new SKColor(0x00, 0x78, 0xD4)
        });

        navigation = new ButtonGroup<string>
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Alignment = ContentAlignment.TopLeft,
            Gap = 6,
            Scrollable = false,
            BackColor = SKColors.Transparent
        };
        navigation.SetItems(new[]
        {
            "Home", "System", "Bluetooth & devices", "Network & internet",
            "Personalization", "Apps", "Accounts", "Privacy & security", "Windows Update"
        });
        navigation.ConfigureButton = (button, value) =>
        {
            button.Height = 40;
            button.MinimumSize = new SKSize(224, 40);
            button.Padding = new Thickness(12, 0, 12, 0);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Radius = new Radius(8);
            button.BackColor = SKColors.Transparent;
            button.Border = new Thickness(0);
            button.Shadow = BoxShadow.None;
            button.ForeColor = new SKColor(0x1F, 0x1F, 0x1F);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.ToolTipText = value;
        };
        navigation.SetSelectedValue("System", false);

        var search = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 38,
            Margin = new Thickness(0, 0, 0, 18),
            PlaceholderText = "Find a setting",
            Text = "",
            Radius = new Radius(8)
        };

        var profile = new Element
        {
            Dock = DockStyle.Top,
            Height = 74,
            Margin = new Thickness(0, 0, 0, 14),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };
        profile.Controls.Add(new Element
        {
            BackgroundImage = Own(SettingsPreviewHelper.CreateAvatar(44)),
            BackgroundImageLayout = ImageLayout.Stretch,
            Location = new SKPoint(0, 14),
            Size = new SKSize(44, 44),
            BackColor = new SKColor(0x00, 0x78, 0xD4),
            Border = new Thickness(1),
            BorderColor = SKColors.White.WithAlpha(180),
            Radius = new Radius(22)
        });
        profile.Controls.Add(new Element
        {
            Text = "Mahmut Yildirim\nLocal account",
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new SKPoint(58, 8),
            Size = new SKSize(166, 58),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            ForeColor = new SKColor(0x1F, 0x1F, 0x1F),
            WrapMode = TextWrap.None
        });

        panel.Controls.Add(navigation);
        panel.Controls.Add(search);
        panel.Controls.Add(profile);
        panel.Controls.Add(footer);
        return panel;
    }

    private Element BuildContent()
    {
        const int contentWidth = 1580;
        var y = 0;
        var page = new Element
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(28, 24, 28, 28),
            AutoScroll = true,
            AutoScrollMargin = new SKSize(0, 36),
            AutoScrollMinSize = new SKSize(contentWidth, 1720),
        };

        var header = BuildHeader();
        var deviceGrid = BuildDeviceGrid();
        var controlsGrid = BuildControlsGrid();
        var dataCard = BuildDataCard();
        var layoutCard = BuildLayoutCard();
        var bottomGrid = BuildBottomGrid();

        PlaceSection(header, contentWidth, ref y, 20);
        PlaceSection(deviceGrid, contentWidth, ref y, 20);
        PlaceSection(controlsGrid, contentWidth, ref y, 20);
        PlaceSection(dataCard, contentWidth, ref y, 20);
        PlaceSection(layoutCard, contentWidth, ref y, 20);
        PlaceSection(bottomGrid, contentWidth, ref y, 0);
        page.AutoScrollMinSize = new SKSize(contentWidth, y + 36);

        page.Controls.Add(header);
        page.Controls.Add(deviceGrid);
        page.Controls.Add(controlsGrid);
        page.Controls.Add(dataCard);
        page.Controls.Add(layoutCard);
        page.Controls.Add(bottomGrid);
        return page;
    }

    private Element BuildHeader()
    {
        var header = new Element
        {
            Height = 178,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            CanSelect = false,
            TabStop = false
        };

        var breadcrumb = new Breadcrumb
        {
            Location = new SKPoint(0, 0),
            Size = new SKSize(520, 32),
            AutoSize = false
        };
        breadcrumb.Controls.Add(CreateBreadcrumbItem("Settings"));
        breadcrumb.Controls.Add(CreateBreadcrumbItem("System"));

        var title = new Element
        {
            Text = "System",
            Location = new SKPoint(0, 34),
            Size = new SKSize(500, 42),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            CanSelect = false,
            TabStop = false,
            ForeColor = new SKColor(0x1F, 0x1F, 0x1F),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.Default, 28) { Embolden = true }
        };

        var hero = new Card
        {
            Location = new SKPoint(0, 74),
            Size = new SKSize(620, 104),
            Padding = new Thickness(18),
            Title = "Surface Laptop Studio",
            Description = "Windows 11 Pro - preview layout using Orivy controls",
            Radius = new Radius(14),
            BackgroundImageLayout = ImageLayout.Cover,
            BackgroundImagePosition = BackgroundImagePosition.Center,
            HeaderPlacement = CardHeaderPlacement.Overlay,
            BackgroundImageCaptionDesignMode = BackgroundImageCaptionDesignMode.Hidden
        };
        hero.Controls.Add(new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 10,
            Value = 84,
            TextMode = ProgressBarTextMode.None,
            Mode = ProgressBarMode.Gradient,
            ForeColor = new SKColor(0x00, 0x78, 0xD4)
        });

        var actions = new Card
        {
            Location = new SKPoint(638, 74),
            Size = new SKSize(352, 104),
            Padding = new Thickness(14),
            Title = "Microsoft 365",
            Description = "Manage cloud storage and services.",
            HeaderGap = 8,
            Radius = new Radius(14)
        };
        var dropDownMenu = new ContextMenuStrip { AutoClose = true };
        var accountButton = new Button
        {
            Text = "Manage",
            Dock = DockStyle.Bottom,
            Height = 36,
            DropDownMenu = dropDownMenu,
            ShowDropDownArrow = true,
            SplitDropDownButton = true,
            ToolTipText = "Click the text area to keep the current action; click the chevron for more choices.",
            Shadow = BoxShadow.None
        };
        AddDropDownSelection(dropDownMenu, accountButton, "Account settings");
        AddDropDownSelection(dropDownMenu, accountButton, "View subscription");
        dropDownMenu.AddSeparator();
        AddDropDownSelection(dropDownMenu, accountButton, "Send feedback");
        actions.Controls.Add(accountButton);

        header.Controls.Add(actions);
        header.Controls.Add(hero);
        header.Controls.Add(title);
        header.Controls.Add(breadcrumb);
        return header;
    }

    private Grid BuildDeviceGrid()
    {
        var grid = CreateSectionGrid("deviceGrid", 2, 3, rowHeight: 124, marginBottom: 18);

        grid.Add(CreateSettingTile("Display", "Monitors, brightness, night light", PreviewIconKind.Display, new SKColor(0x00, 0x78, 0xD4)), 0, 0);
        grid.Add(CreateSettingTile("Sound", "Volume levels and input devices", PreviewIconKind.System, new SKColor(0x7C, 0x3A, 0xED)), 0, 1);
        grid.Add(CreateSettingTile("Notifications", "Alerts, banners and quiet hours", PreviewIconKind.Apps, new SKColor(0xF5, 0x9E, 0x0B)), 0, 2);
        grid.Add(CreateSettingTile("Storage", "128 GB free of 512 GB", PreviewIconKind.Storage, new SKColor(0x10, 0xB9, 0x81)), 1, 0);
        grid.Add(CreateSettingTile("Power", "Battery, sleep and energy saver", PreviewIconKind.Security, new SKColor(0xEF, 0x44, 0x44)), 1, 1);
        grid.Add(CreateSettingTile("Nearby sharing", "Discoverability and transfers", PreviewIconKind.Bluetooth, new SKColor(0x06, 0xB6, 0xD4)), 1, 2);

        return grid;
    }

    private Grid BuildControlsGrid()
    {
        var grid = CreateSectionGrid("controlsGrid", 2, 2, rowHeight: 396, marginBottom: 18);
        grid.Add(BuildControlsCard(), 0, 0);
        grid.Add(BuildInputCard(), 0, 1);
        grid.Add(BuildPersonalizationCard(), 1, 0);
        grid.Add(BuildProgressCard(), 1, 1);
        return grid;
    }

    private Card BuildControlsCard()
    {
        var card = CreateCard("Recommended settings", "Common choices with a calm Settings-style layout.", 356);
        card.HeaderGap = 16;
        var batteryRow = CreateSettingRow("Battery saver", "Reduce background activity", new SwitchButton
        {
            Checked = true,
            Size = new SKSize(58, 28),
            TransitionMode = SwitchButtonTransitionMode.Cupertino,
            PressStretch = 1.18f
        });
        var suggestionsRow = CreateSettingRow("Show suggestions", "Tips and recommended actions", new CheckBox
        {
            Checked = true,
            Size = new SKSize(32, 30)
        });
        var balancedRow = CreateSettingRow("Balanced", "Recommended power mode", new RadioButton
        {
            Checked = true,
            GroupName = "powerMode",
            Size = new SKSize(32, 30)
        });
        var performanceRow = CreateSettingRow("Best performance", "Use more power for speed", new RadioButton
        {
            GroupName = "powerMode",
            Size = new SKSize(32, 30)
        });
        card.Controls.Add(performanceRow);
        card.Controls.Add(balancedRow);
        card.Controls.Add(suggestionsRow);
        card.Controls.Add(batteryRow);
        return card;
    }

    private Card BuildInputCard()
    {
        var card = CreateCard("Input devices", "ComboBox, TextBox, NumericUpDown, DatePicker and TimePicker together.", 372);
        card.HeaderGap = 16;

        var combo = new ComboBox
        {
            Dock = DockStyle.Top,
            Height = 38,
            Margin = new Thickness(0, 0, 0, 10),
            PlaceholderText = "Choose device"
        };
        combo.Items.AddRange(new object[]
        {
            new ComboBoxItem("Surface Keyboard"),
            new ComboBoxItem("Precision Touchpad"),
            new ComboBoxItem("Bluetooth Mouse")
        });
        combo.SelectedIndex = 0;

        card.Controls.Add(new TrackBar
        {
            Dock = DockStyle.Top,
            Height = 50,
            Value = 68,
            ShowValue = true,
            Margin = new Thickness(0, 0, 0, 8),
            ToolTipText = "Pointer speed"
        });
        card.Controls.Add(new NumericUpDown
        {
            Dock = DockStyle.Top,
            Height = 38,
            Value = 125,
            Minimum = 25,
            Maximum = 300,
            Increment = 25,
            Suffix = "%",
            ButtonVisibility = NumericUpDownButtonVisibility.HoverOrFocused,
            Margin = new Thickness(0, 0, 0, 10)
        });
        var scheduleRow = new Element
        {
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Thickness(0, 0, 0, 10),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };
        scheduleRow.Controls.Add(new DatePicker
        {
            Location = new SKPoint(0, 2),
            Size = new SKSize(250, 38),
            Value = DateTime.Today.AddDays(1).AddHours(22).AddMinutes(30),
            ShowTimePicker = true,
            DateTimeFormat = "MMM d, yyyy HH:mm",
            TextBoxMode = true,
            ToolTipText = "Restart date and time"
        });
        card.Controls.Add(scheduleRow);
        card.Controls.Add(new TextBox
        {
            Dock = DockStyle.Top,
            Height = 38,
            PlaceholderText = "Device name",
            Text = "Mahmut-PC",
            Margin = new Thickness(0, 0, 0, 10)
        });
        card.Controls.Add(combo);
        return card;
    }

    private Card BuildPersonalizationCard()
    {
        var card = CreateCard("Personalization", "ButtonGroup shows a compact segmented picker.", 300);
        var group = new ButtonGroup<string>
        {
            Dock = DockStyle.Top,
            Height = 42,
            Gap = 0,
            Scrollable = false,
            Margin = new Thickness(0, 0, 0, 12)
        };
        group.SetItems(new[] { "Light", "Dark", "Auto" });
        group.ConfigureButton = (button, _) =>
        {
            button.Height = 36;
            button.MinimumSize = new SKSize(84, 36);
            button.CheckOnClick = true;
            button.Shadow = BoxShadow.None;
        };
        group.SetSelectedValue("Light", false);

        var colorRow = new FlowLayout
        {
            Dock = DockStyle.Top,
            Height = 42,
            AutoScroll = false,
            HorizontalGap = 10,
            VerticalGap = 10,
            WrapContents = false
        };
        colorRow.Controls.Add(CreateAccentButton(new SKColor(0x00, 0x78, 0xD4)));
        colorRow.Controls.Add(CreateAccentButton(new SKColor(0x7C, 0x3A, 0xED)));
        colorRow.Controls.Add(CreateAccentButton(new SKColor(0x10, 0xB9, 0x81)));
        colorRow.Controls.Add(CreateAccentButton(new SKColor(0xF5, 0x9E, 0x0B)));
        colorRow.Controls.Add(CreateAccentButton(new SKColor(0xEF, 0x44, 0x44)));

        card.Controls.Add(new Separator { Dock = DockStyle.Top, Height = 16 });
        card.Controls.Add(colorRow);
        card.Controls.Add(group);
        return card;
    }

    private static Element CreateSettingRow(string title, string description, ElementBase editor)
    {
        var row = new Element
        {
            Dock = DockStyle.Top,
            Height = 58,
            Margin = new Thickness(0, 0, 0, 12),
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(70),
            Radius = new Radius(10),
            Padding = new Thickness(14, 7, 12, 7)
        };
        var editorHost = new Element
        {
            Dock = DockStyle.Right,
            Width = 96,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            CanSelect = false,
            TabStop = false
        };
        editor.Dock = DockStyle.None;
        editor.Anchor = AnchorStyles.None;
        editor.Location = new SKPoint(
            Math.Max(0f, (editorHost.Width - editor.Width) / 2f),
            Math.Max(0f, (58f - editor.Height) / 2f));
        editor.Shadow = BoxShadow.None;
        editorHost.Controls.Add(editor);

        row.Controls.Add(new Element
        {
            Text = title,
            Location = new SKPoint(14, 8),
            Size = new SKSize(470, 20),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            CanSelect = false,
            TabStop = false,
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.Default, 13) { Embolden = true },
            WrapMode = TextWrap.None
        });
        row.Controls.Add(new Element
        {
            Text = description,
            Location = new SKPoint(14, 31),
            Size = new SKSize(470, 18),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            CanSelect = false,
            TabStop = false,
            ForeColor = ColorScheme.ForeColor.WithAlpha(145),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.Default, 11),
            WrapMode = TextWrap.None
        });

        row.Controls.Add(editorHost);
        return row;
    }

    private Card BuildProgressCard()
    {
        var card = CreateCard("Storage", "ProgressBar modes: range, segmented, circular and hatch fill.", 300);
        card.Controls.Add(new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 18,
            Value = 64,
            TextMode = ProgressBarTextMode.PercentWhenWide,
            Mode = ProgressBarMode.Segmented,
            Margin = new Thickness(0, 0, 0, 12)
        });
        card.Controls.Add(new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 18,
            Value = 42,
            TextMode = ProgressBarTextMode.ValueRange,
            Mode = ProgressBarMode.Gradient,
            UseHatchFill = true,
            HatchStyle = HatchStyle.ForwardDiagonal,
            ForeColor = new SKColor(0x00, 0x78, 0xD4),
            Margin = new Thickness(0, 0, 0, 16)
        });
        var ringHost = new Element { Dock = DockStyle.Top, Height = 92, BackColor = SKColors.Transparent, Border = new Thickness(0) };
        ringHost.Controls.Add(new ProgressBar
        {
            Mode = ProgressBarMode.Ring,
            Size = new SKSize(74, 74),
            Location = new SKPoint(0, 2),
            Value = 73,
            TextMode = ProgressBarTextMode.Percent
        });
        ringHost.Controls.Add(new Element
        {
            Text = "System drive\n73% used",
            Location = new SKPoint(94, 8),
            Size = new SKSize(230, 58),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(ringHost);
        return card;
    }

    private Card BuildDataCard()
    {
        var card = CreateCard("Recent activity", "GridList previews a dense Settings-style table.", 306);

        var list = new GridList
        {
            Dock = DockStyle.Fill,
            RowHeight = 34,
            HeaderHeight = 36,
            StickyHeader = true,
            GroupingEnabled = true,
            CheckBoxes = true,
            Radius = new Radius(10)
        };
        list.Columns.Add(new GridListColumn { HeaderText = "Setting", Name = "setting", Width = 220, SizeMode = GridListColumnSizeMode.Auto });
        list.Columns.Add(new GridListColumn { HeaderText = "State", Name = "state", Width = 110, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        list.Columns.Add(new GridListColumn { HeaderText = "Description", Name = "description", Width = 460, SizeMode = GridListColumnSizeMode.Fill, Sortable = false });

        AddGridListRow(list, "Security", "Core isolation", "On", "Memory integrity is enabled.", true);
        AddGridListRow(list, "Security", "Firewall", "On", "Domain, private and public profiles are protected.", true);
        AddGridListRow(list, "System", "Storage Sense", "Auto", "Temporary files are cleaned monthly.", true);
        AddGridListRow(list, "System", "Night light", "Off", "Scheduled from sunset to sunrise.", false);
        AddGridListRow(list, "Network", "Metered connection", "Off", "Wi-Fi usage is not constrained.", false);
        AddGridListRow(list, "Network", "DNS over HTTPS", "On", "Encrypted resolver is configured.", true);

        card.Controls.Add(list);
        return card;
    }

    private Card BuildLayoutCard()
    {
        var card = CreateCard("Layout controls", "SplitContainer hosts TreeView and an embedded TabView preview.", 340);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 250,
            SplitterWidth = 8,
            PanelMinSize = 160,
            BackColor = SKColors.Transparent
        };

        var tree = new TreeView
        {
            Dock = DockStyle.Fill,
            ItemHeight = 32,
            Indent = 22,
            Radius = new Radius(10)
        };
        var system = new TreeNode("System");
        system.Add("Display");
        system.Add("Sound");
        system.Add("Power");
        var network = new TreeNode("Network");
        network.Add("Wi-Fi");
        network.Add("VPN");
        tree.Nodes.Add(system);
        tree.Nodes.Add(network);
        tree.ExpandNode(system);
        tree.ExpandNode(network);

        var detailPanel = new Element
        {
            Dock = DockStyle.Fill,
            Padding = new Thickness(14),
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80),
            Radius = new Radius(10)
        };
        detailPanel.Controls.Add(CreateLayoutPreviewRow("About", "Preview-only clone built with Orivy controls."));
        detailPanel.Controls.Add(CreateLayoutPreviewRow("Advanced", "Tree selection can drive any hosted panel."));
        detailPanel.Controls.Add(CreateLayoutPreviewRow("Overview", "SplitContainer keeps the tree and content readable."));

        split.Panel1.Controls.Add(tree);
        split.Panel2.Controls.Add(detailPanel);
        card.Controls.Add(split);
        return card;
    }

    private Grid BuildBottomGrid()
    {
        var grid = CreateSectionGrid("bottomGrid", 1, 3, rowHeight: 260, marginBottom: 0);
        grid.Add(CreateInfoCard("Windows Security", "Account protection, firewall, device health", PreviewIconKind.Security, new SKColor(0x10, 0xB9, 0x81)), 0, 0);
        grid.Add(BuildDisclosureCard(), 0, 1);
        grid.Add(CreateInfoCard("Troubleshoot", "Recommended fixes and recent diagnostics", PreviewIconKind.Update, new SKColor(0xF5, 0x9E, 0x0B)), 0, 2);
        return grid;
    }

    private Card BuildDisclosureCard()
    {
        var card = CreateCard("Privacy dashboard", "Clear status rows without nesting-heavy preview controls.", 250);
        card.HeaderGap = 16;
        card.Controls.Add(CreateStatusRow("Diagnostics", "Optional data disabled", "Off", new SKColor(239, 68, 68)));
        card.Controls.Add(CreateStatusRow("Location", "Only Maps can access location", "Limited", new SKColor(245, 158, 11)));
        card.Controls.Add(CreateStatusRow("Camera", "Desktop apps allowed", "On", new SKColor(16, 185, 129)));
        return card;
    }

    private static Element CreateStatusRow(string title, string description, string state, SKColor accent)
    {
        var row = new Element
        {
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Thickness(0, 0, 0, 8),
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(70),
            Radius = new Radius(9)
        };
        row.Controls.Add(new Element
        {
            Text = state,
            Location = new SKPoint(302, 9),
            Size = new SKSize(72, 24),
            BackColor = accent.WithAlpha(24),
            ForeColor = accent.Brightness(-0.18f),
            Border = new Thickness(1),
            BorderColor = accent.WithAlpha(80),
            Radius = new Radius(12),
            TextAlign = ContentAlignment.MiddleCenter
        });
        row.Controls.Add(new Element
        {
            Text = $"{title}\n{description}",
            Location = new SKPoint(12, 4),
            Size = new SKSize(280, 34),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft,
            WrapMode = TextWrap.None
        });
        return row;
    }

    private static Element CreateLayoutPreviewRow(string title, string description)
    {
        return new Element
        {
            Text = $"{title}\n{description}",
            Dock = DockStyle.Top,
            Height = 56,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(12, 4, 12, 4),
            BackColor = ColorScheme.Surface,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(70),
            Radius = new Radius(9),
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft,
            WrapMode = TextWrap.None
        };
    }

    private Grid CreateSectionGrid(string name, int rows, int columns, int rowHeight, int marginBottom)
    {
        return new Grid
        {
            Name = name,
            Height = rows * rowHeight + (rows - 1) * 14,
            RowCount = rows,
            ColumnCount = columns,
            RowGap = 14,
            ColumnGap = 14,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };
    }

    private Card CreateSettingTile(string title, string description, PreviewIconKind iconKind, SKColor accent)
    {
        var card = CreateCard(title, description, 120);
        card.Padding = new Thickness(16, 16, 16, 14);
        card.HeaderGap = 4;
        card.Controls.Add(new Element
        {
            Text = GetTileMetaText(iconKind),
            Dock = DockStyle.Bottom,
            Height = 24,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(140),
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(new Element
        {
            Dock = DockStyle.Left,
            Width = 46,
            Text = GetIconFallback(iconKind),
            BackColor = accent.WithAlpha(24),
            ForeColor = accent,
            Border = new Thickness(1),
            BorderColor = accent.WithAlpha(80),
            Radius = new Radius(12),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new SKFont(SKTypeface.Default, 15) { Embolden = true },
            Margin = new Thickness(0, 0, 14, 0)
        });
        return card;
    }

    private Card CreateInfoCard(string title, string description, PreviewIconKind iconKind, SKColor accent)
    {
        var card = CreateCard(title, description, 180);
        card.Padding = new Thickness(18);
        card.Controls.Add(new Button
        {
            Text = "Open",
            Dock = DockStyle.Bottom,
            Height = 34,
            Shadow = BoxShadow.None,
            BadgeText = title.Contains("Security", StringComparison.OrdinalIgnoreCase) ? "!" : "",
            ToolTipText = "Preview only"
        });
        card.Controls.Add(new Element
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = GetIconFallback(iconKind),
            BackColor = accent.WithAlpha(24),
            ForeColor = accent,
            Border = new Thickness(1),
            BorderColor = accent.WithAlpha(80),
            Radius = new Radius(12),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new SKFont(SKTypeface.Default, 15) { Embolden = true },
            Margin = new Thickness(0, 0, 0, 12)
        });
        return card;
    }

    private Card CreateCard(string title, string description, int height)
    {
        return new Card
        {
            Height = height,
            Width = 100,
            Padding = new Thickness(18),
            Title = title,
            Description = description,
            HeaderGap = 12,
            Radius = new Radius(12),
            BackColor = SKColors.White,
            BorderColor = new SKColor(0xD8, 0xD8, 0xD8),
            Shadow = new BoxShadow(0, 1, 2, 0, SKColors.Black.WithAlpha(10))
        };
    }

    private static void PlaceSection(ElementBase section, int width, ref int y, int gap)
    {
        section.Dock = DockStyle.None;
        section.Location = new SKPoint(0, y);
        section.Width = width;
        y += (int)MathF.Ceiling(section.Height) + gap;
    }

    private Button CreateAccentButton(SKColor color)
    {
        return new Button
        {
            Text = "",
            Size = new SKSize(36, 36),
            MinimumSize = new SKSize(36, 36),
            BackColor = color,
            BorderColor = color.Brightness(-0.12f),
            Radius = new Radius(18),
            Shadow = BoxShadow.None,
            ToolTipText = "Accent color"
        };
    }

    private static ElementBase CreateBreadcrumbItem(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new SKSize(42, 28),
            Padding = new Thickness(9, 5, 9, 5),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(7),
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleCenter,
            WrapMode = TextWrap.None,
            Shadow = BoxShadow.None
        };
    }

    private static string GetIconFallback(PreviewIconKind kind)
    {
        return kind switch
        {
            PreviewIconKind.Display => "D",
            PreviewIconKind.System => "S",
            PreviewIconKind.Apps => "N",
            PreviewIconKind.Storage => "GB",
            PreviewIconKind.Security => "P",
            PreviewIconKind.Bluetooth => "B",
            PreviewIconKind.Privacy => "PR",
            PreviewIconKind.Update => "U",
            _ => "O"
        };
    }

    private static string GetTileMetaText(PreviewIconKind kind)
    {
        return kind switch
        {
            PreviewIconKind.Display => "Brightness 80%",
            PreviewIconKind.System => "Output device active",
            PreviewIconKind.Apps => "Quiet hours off",
            PreviewIconKind.Storage => "128 GB available",
            PreviewIconKind.Security => "Battery 91%",
            PreviewIconKind.Bluetooth => "Discoverable",
            _ => "Ready"
        };
    }

    private static void AddDropDownSelection(ContextMenuStrip menu, Button owner, string text)
    {
        var item = menu.AddMenuItem(text);
        item.Click += (_, _) =>
        {
            owner.Text = text;
            owner.Invalidate();
        };
    }

    private static Collapse CreateSettingsCollapse(string title, string body)
    {
        var collapse = new Collapse
        {
            HeaderText = title,
            IsExpanded = title == "Permissions",
            Height = 84,
            Radius = new Radius(10)
        };
        collapse.Controls.Add(new Element
        {
            Text = body,
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            ForeColor = ColorScheme.ForeColor.WithAlpha(170),
            TextAlign = ContentAlignment.MiddleLeft
        });
        return collapse;
    }

    private Element CreateTabPage(string title, string body)
    {
        var page = new Element
        {
            Text = title,
            Padding = new Thickness(18),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Image = Own(SettingsPreviewHelper.CreateIcon(ColorScheme.Primary, PreviewIconKind.System, 22))
        };
        page.Controls.Add(new Element
        {
            Dock = DockStyle.Top,
            Height = 74,
            Text = body,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80),
            Radius = new Radius(10),
            Padding = new Thickness(16)
        });
        return page;
    }

    private static void AddGridListRow(GridList list, string group, string setting, string state, string description, bool check)
    {
        var item = new GridListItem { GroupKey = group, GroupText = group };
        item.Cells.Add(new GridListCell { Text = setting });
        item.Cells.Add(new GridListCell { Text = state, CheckState = check ? CheckState.Checked : CheckState.Unchecked });
        item.Cells.Add(new GridListCell { Text = description });
        list.Items.Add(item);
    }

    private SKImage Own(SKImage image)
    {
        _ownedImages.Add(image);
        return image;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            for (var i = 0; i < _ownedImages.Count; i++)
                _ownedImages[i].Dispose();
            _ownedImages.Clear();
        }

        base.Dispose(disposing);
    }
}
