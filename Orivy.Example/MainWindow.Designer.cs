using Orivy;
using Orivy.Animation;
using Orivy.Binding;
using Orivy.Controls;
using Orivy.Validations;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orivy.Example;

internal partial class MainWindow
{
    internal void InitializeComponent()
    {
        this.SuspendLayout();
        
        this.panel3 = new DesignerControlsDemoPage();

        this.panel4 = new VisualStylesDemoPage();

        this.panel5 = new ScrollLabDemoPage();

        this.panel6 = new GridListDemoPage();

        tabView = new()
        {
            Name = "tabView",
            Dock = Orivy.DockStyle.Fill,
            TabMode = TabViewMode.TitleBar,
            DrawTabIcons = true,
            TransitionEffect = TabViewTransitionEffect.ScaleFade,
            TransitionAnimationType = AnimationType.QuarticEaseOut,
            TransitionDurationMs = 350,
            LockInputDuringTransition = true,
        };

        var designerTabIcon = CreateExampleIcon(new SKColor(0xF5, 0x9E, 0x0B), ExampleIconKind.Pulse);
        var stylesTabIcon = CreateExampleIcon(new SKColor(0xEC, 0x48, 0x99), ExampleIconKind.Healthy);
        var scrollTabIcon = CreateExampleIcon(new SKColor(0x14, 0xB8, 0xA6), ExampleIconKind.Pulse);
        var gridTabIcon = CreateExampleIcon(new SKColor(0x8B, 0x5C, 0xF6), ExampleIconKind.Warning);

        this.panel3.Image = designerTabIcon;
        this.panel4.Image = stylesTabIcon;
        this.panel5.Image = scrollTabIcon;
        this.panel6.Image = gridTabIcon;

        // build example menu strip demonstrating top-level menus and submenus
        this.menuStrip = new MenuStrip
        {
            Name = "menuStrip",
            Dock = DockStyle.Top,
            ShowSubmenuArrow = false,
        };

        
        // use extension helpers for concise syntax
        var fileMenu = this.menuStrip.AddMenuItem("File");
        fileMenu.AddMenuItem("Open", (_, _) => ShowOpenFileSelectionDialog(), Keys.Control | Keys.O);
        fileMenu.AddMenuItem("Save As", (_, _) => ShowSaveFileSelectionDialog(), Keys.Control | Keys.S);
        fileMenu.AddMenuItem("Open Folder", (_, _) => ShowOpenFolderSelectionDialog(), Keys.Control | Keys.Shift | Keys.O);
        fileMenu.AddSeparator();
        fileMenu.AddMenuItem("Exit", (s, e) => this.Close(), Keys.Control | Keys.X);

        var helpMenu = this.menuStrip.AddMenuItem("Help");
        helpMenu.AddMenuItem("About", (s, e) =>
        {
            Debug.WriteLine("Orivy Example\nA simple demo of the Orivy UI framework.\n\nhttps://github.com/mahmutyildirim/orivy");
        });

        var transitionsMenu = this.menuStrip.AddMenuItem("Transitions");
        InitializeTransitionMenu(transitionsMenu);

        var windowMenu = this.menuStrip.AddMenuItem("Window");
        InitializeWindowMenu(windowMenu);

        // --- ExtendMenu: drop-down that appears when the extend button in
        // the title bar is clicked. ExtendBox must be true to show the button.
        this.extendMenu = new ContextMenuStrip();
        
        this.extendMenu.AddMenuItem("Settings", (s, e) => Debug.WriteLine("Settings clicked"), Keys.Control | Keys.O);
        this.extendMenu.AddMenuItem("Check for Updates", (s, e) => Debug.WriteLine("Update check"));
        this.extendMenu.AddSeparator();
        InitializeWindowMenu(this.extendMenu.AddMenuItem("Window"));
        var extendTransitionsMenu = this.extendMenu.AddMenuItem("Page Transition");
        InitializeTransitionMenu(extendTransitionsMenu);

        // assign a real icon so the title bar shows one; the menu glyph option
        // below can be toggled to switch behaviour
        this.Icon = System.Drawing.SystemIcons.Application;
        // Uncomment to replace the icon with a tiny menu glyph:
        // this.ShowMenuInsteadOfIcon = true;

        // wire up ExtendBox + ExtendMenu
        this.ExtendBox = true;
        this.extendMenu.UseAccordionSubmenus = true;
        this.ExtendMenu = this.extendMenu;
        this.ShowMenuInsteadOfIcon = true;
        this.FormMenu = this.extendMenu;

        tabView.Controls.Add(panel3);
        tabView.Controls.Add(panel4);
        tabView.Controls.Add(panel5);
        tabView.Controls.Add(panel6);

        var bindingPage = new BindingDemoPage();
        tabView.Controls.Add(bindingPage);
        var notificationsPage = new NotificationsDemoPage();
        tabView.Controls.Add(notificationsPage);
        _embeddedTabsPage = new EmbeddedTabsDemoPage(tabView);
        tabView.Controls.Add(_embeddedTabsPage);
        var buttonGroupPage = new ButtonGroupDemoPage();
        var buttonGroupTabIcon = CreateExampleIcon(new SKColor(0x22, 0xC5, 0x5E), ExampleIconKind.Healthy);
        buttonGroupPage.Image = buttonGroupTabIcon;
        tabView.Controls.Add(buttonGroupPage);
        var modernControlsPage = new ModernControlsDemoPage();
        var modernControlsTabIcon = CreateExampleIcon(new SKColor(0x06, 0xB6, 0xD4), ExampleIconKind.Pulse);
        modernControlsPage.Image = modernControlsTabIcon;
        tabView.Controls.Add(modernControlsPage);
        var layoutControlsPage = new LayoutControlsDemoPage();
        var layoutControlsTabIcon = CreateExampleIcon(new SKColor(0xA3, 0xA3, 0xA3), ExampleIconKind.Warning);
        layoutControlsPage.Image = layoutControlsTabIcon;
        tabView.Controls.Add(layoutControlsPage);
        var cardDemoPage = new CardDemoPage();
        var cardDemoTabIcon = CreateExampleIcon(new SKColor(0xF9, 0x73, 0x16), ExampleIconKind.Healthy);
        cardDemoPage.Image = cardDemoTabIcon;
        tabView.Controls.Add(cardDemoPage);
        var markdownViewerPage = new MarkdownViewerDemoPage();
        var markdownViewerTabIcon = CreateExampleIcon(new SKColor(0x10, 0xB9, 0x81), ExampleIconKind.Pulse);
        markdownViewerPage.Image = markdownViewerTabIcon;
        tabView.Controls.Add(markdownViewerPage);



        extendMenu.ShowShortcutKeys = true;
        menuStrip.ShowShortcutKeys = true;

        // 
        // MainWindow
        // 
        this.Name = "MainWindow";
        this.Text = "Orivy Example";
        this.Width = 1100;
        this.Height = 650;
        this.DwmMargin = 1000;
        this.tabView.HorizontalTabOverflowScrolling = true;
        //this.Padding = new(10);
        this.WindowThemeType = WindowThemeType.Tabbed;
        RefreshWindowThemeMenuChecks();
        this.ContextMenuStrip = this.extendMenu;
        this.TabView = tabView;
        this.StartPosition = Orivy.FormStartPosition.CenterScreen;
        this.ShowPerfOverlay = true;
        this.Controls.Add(tabView);
        this.Controls.Add(this.menuStrip);
        this.menuStrip.BringToFront();
        this.ResumeLayout(false);
    }

    private Container panel3;
    private Container panel4;
    private Container panel5;
    private Container panel6;

    private MenuStrip menuStrip;
    private ContextMenuStrip extendMenu;
    private TabView tabView;
    private EmbeddedTabsDemoPage _embeddedTabsPage;

}
