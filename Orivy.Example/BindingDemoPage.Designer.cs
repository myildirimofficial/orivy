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

internal sealed partial class BindingDemoPage
{
    private void InitializeComponent()
    {
        Text = "Binding Lab";
        _tabIcon = ExampleHelper.CreateIcon(new SKColor(0x10, 0xB9, 0x81), ExampleIconKind.Healthy);
        Image = _tabIcon;

        _bindingPanel = new Container
        {
            Name = "bindingPanel",
            Text = "Binding Lab",
            Padding = new Thickness(24),
            Dock = DockStyle.Fill,
            Radius = new Radius(0),
            Border = new Thickness(0),
            AutoScroll = true,
            AutoScrollMargin = new SKSize(0, 24)
        }.SetDataContext(_bindingDemoViewModel);

        var header = CreateBindingCard(
            "bindingHeader",
            "Binding Lab\nOrivy keeps state flow local to the control tree. Bindings start from the consumer control, and user actions stay on the existing event system instead of forcing command objects everywhere.",
            110,
            new Thickness(0, 0, 0, 16),
            ColorScheme.SurfaceContainerHigh);

        var snippet = CreateBindingCard(
            "bindingSnippet",
            "panel.SetDataContext(viewModel)\nhero.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.HeroText).OneWay()\npresetCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedPreset).TwoWay()\ndeployButton.When<BindingDemoViewModel>(\"Click\", (vm, _) => vm.MarkDeployed(vm.DeploymentPayload))",
            132,
            new Thickness(0, 0, 0, 14),
            ColorScheme.SurfaceVariant);

        var hero = CreateBindingCard("bindingHero", string.Empty, 108, new Thickness(0, 0, 0, 14), ColorScheme.Primary.WithAlpha(230));
        hero.ForeColor = SKColors.White;
        hero.Border = new Thickness(0);
        hero.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.HeroText).OneWay();
        hero.Link(view => view.BackColor).FromData((BindingDemoViewModel vm) => vm.AccentColor).OneWay();

        var summary = CreateBindingCard("bindingSummary", string.Empty, 92, new Thickness(0, 0, 0, 14), ColorScheme.SurfaceContainer);
        summary.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.SummaryText).OneWay();

        var alertCard = CreateBindingCard("bindingAlertCard", string.Empty, 78, new Thickness(0, 0, 0, 14), new SKColor(185, 28, 28));
        alertCard.ForeColor = SKColors.White;
        alertCard.Border = new Thickness(0);
        alertCard.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.AlertText).OneWay();
        alertCard.Link(view => view.Visible).FromData((BindingDemoViewModel vm) => vm.AlertVisible).OneWay();

        var statusCard = CreateBindingCard("bindingStatus", string.Empty, 94, new Thickness(0, 0, 0, 16), ColorScheme.SurfaceContainerHigh);
        statusCard.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.StatusText).OneWay();

        var selectionRow = new Container
        {
            Name = "bindingSelectionRow",
            Dock = DockStyle.Top,
            Height = 52,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Black.WithAlpha(12),
            Border = new Thickness(0)
        };

        var teamCombo = new ComboBox
        {
            Name = "bindingTeamCombo",
            Dock = DockStyle.Right,
            Width = 240,
            Margin = new Thickness(12, 0, 0, 0)
        };
        teamCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Teams).OneWay();
        teamCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedTeam).TwoWay();

        var presetCombo = new ComboBox
        {
            Name = "bindingPresetCombo",
            Dock = DockStyle.Fill
        };
        presetCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Presets).OneWay();
        presetCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedPreset).TwoWay();
        selectionRow.Controls.Add(teamCombo);
        selectionRow.Controls.Add(presetCombo);

        var actionRow = new Container
        {
            Name = "bindingActionRow",
            Height = 42,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var deployButton = new Button
        {
            Name = "bindingDeployButton",
            Dock = DockStyle.Right,
            Width = 190,
            Margin = new Thickness(12, 0, 0, 0)
        };
        deployButton.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.DeployActionText).OneWay();
        deployButton.Link(view => view.Enabled).FromData((BindingDemoViewModel vm) => vm.CanDeploy).OneWay();
        deployButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.MarkDeployed(vm.DeploymentPayload));

        var addTeamButton = new Button
        {
            Name = "bindingAddTeamButton",
            Dock = DockStyle.Right,
            Width = 150,
            Margin = new Thickness(12, 0, 0, 0),
            Text = "Add Team"
        };
        addTeamButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.AddTeam());

        var toggleAlertButton = new Button
        {
            Name = "bindingToggleAlertButton",
            Dock = DockStyle.Right,
            Width = 170,
            Margin = new Thickness(12, 0, 0, 0),
            Text = "Toggle Signal"
        };
        toggleAlertButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.ToggleAlert());

        var cyclePresetButton = new Button
        {
            Name = "bindingCyclePresetButton",
            Dock = DockStyle.Fill,
            Text = "Cycle Preset"
        };
        cyclePresetButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.CyclePreset());

        var taskShell = new Container
        {
            Name = "bindingTaskShell",
            Dock = DockStyle.Top,
            Height = 266,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Black.WithAlpha(12),
            Border = new Thickness(0)
        };

        var taskDetail = CreateBindingCard("bindingTaskDetail", string.Empty, 266, new Thickness(14, 0, 0, 0), ColorScheme.SurfaceContainerHigh);
        taskDetail.Dock = DockStyle.Right;
        taskDetail.Width = 300;
        taskDetail.Height = 266;
        taskDetail.Margin = new Thickness(14, 0, 0, 0);
        taskDetail.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.TaskDetailText).OneWay();

        var taskGrid = new GridList
        {
            Name = "bindingTaskGrid",
            Dock = DockStyle.Fill,
            HeaderVisible = true,
            StickyHeader = true,
            ShowGridLines = true,
            GroupingEnabled = false,
            MultiSelect = true,
            AutoScroll = false
        };
        taskGrid.Columns.Add(new GridListColumn { Name = "name", HeaderText = "Task", Width = 180f, MinWidth = 120f, SizeMode = GridListColumnSizeMode.Auto });
        taskGrid.Columns.Add(new GridListColumn { Name = "state", HeaderText = "State", Width = 92f, MinWidth = 72f, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        taskGrid.Columns.Add(new GridListColumn { Name = "lane", HeaderText = "Lane", Width = 110f, MinWidth = 84f, SizeMode = GridListColumnSizeMode.Auto });
        taskGrid.Columns.Add(new GridListColumn { Name = "summary", HeaderText = "Summary", Width = 260f, MinWidth = 180f, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.5f, Sortable = false });
        taskGrid.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Tasks).OneWay();
        taskGrid.Link(view => view.SelectedIndex).FromData((BindingDemoViewModel vm) => vm.SelectedTaskIndex).TwoWay();

        taskShell.Controls.Add(taskDetail);
        taskShell.Controls.Add(taskGrid);

        var taskActionRow = new Container
        {
            Name = "bindingTaskActionRow",
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var advanceTaskButton = new Button
        {
            Name = "bindingAdvanceTaskButton",
            Dock = DockStyle.Right,
            Width = 180,
            Margin = new Thickness(12, 0, 0, 0),
            Text = "Advance Task"
        };
        advanceTaskButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.AdvanceSelectedTask());

        var addTaskButton = new Button
        {
            Name = "bindingAddTaskButton",
            Dock = DockStyle.Fill,
            Text = "Add Task"
        };
        addTaskButton.When<BindingDemoViewModel>("Click", (vm, _) => vm.AddTask());

        taskActionRow.Controls.Add(advanceTaskButton);
        taskActionRow.Controls.Add(addTaskButton);

        var taskFooter = CreateBindingCard("bindingTaskFooter", string.Empty, 84, new Thickness(0, 0, 0, 14), ColorScheme.SurfaceVariant);
        taskFooter.Link(view => view.Text).FromData((BindingDemoViewModel vm) => vm.TaskFooterText).OneWay();

        var colorShell = new Container
        {
            Name = "bindingColorShell",
            Dock = DockStyle.Top,
            Height = 228,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var colorDetail = CreateBindingCard(
            "bindingColorDetail",
            "Color Sync\nThe ColorPicker is bound two-way. Move any channel and the hero surface above updates immediately because both are driven by the same view-model color property.",
            228,
            new Thickness(14, 0, 0, 0),
            ColorScheme.SurfaceContainerHigh);
        colorDetail.Dock = DockStyle.Right;
        colorDetail.Width = 300;
        colorDetail.Height = 228;

        var colorPicker = new ColorPicker
        {
            Name = "bindingColorPicker",
            Dock = DockStyle.Fill,
            Margin = new Thickness(0),
            Radius = new Radius(16),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha(110),
            BackColor = ColorScheme.SurfaceContainer
        };
        colorPicker.Link(view => view.SelectedColor).FromData((BindingDemoViewModel vm) => vm.PickerAccentColor).TwoWay();

        colorShell.Controls.Add(colorDetail);
        colorShell.Controls.Add(colorPicker);

        var validationShell = new Container
        {
            Name = "bindingValidationShell",
            Dock = DockStyle.Top,
            Height = 306,
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        var validationGuide = CreateBindingCard(
            "bindingValidationGuide",
            "Validation Flow\nThese controls use the existing ValidationRule system. Error cards are bound directly to control ValidationText and HasValidationError, so no extra glue object is required.",
            306,
            new Thickness(14, 0, 0, 0),
            ColorScheme.SurfaceContainerHigh);
        validationGuide.Dock = DockStyle.Right;
        validationGuide.Width = 320;
        validationGuide.Height = 306;

        var validationStack = new Container
        {
            Name = "bindingValidationStack",
            Dock = DockStyle.Fill,
            Padding = new Thickness(0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0)
        };

        bindingValidationStatusCard = CreateBindingCard(
            "bindingValidationStatus",
            "Validation Submit\nChoose a team and a preset to satisfy the rules.",
            76,
            new Thickness(0, 0, 0, 12),
            ColorScheme.SurfaceVariant);

        bindingValidationRegionError = CreateBindingCard(
            "bindingValidationRegionError",
            string.Empty,
            52,
            new Thickness(0, 0, 0, 10),
            new SKColor(185, 28, 28));
        bindingValidationRegionError.ForeColor = SKColors.White;
        bindingValidationRegionError.Border = new Thickness(0);

        bindingValidationPresetError = CreateBindingCard(
            "bindingValidationPresetError",
            string.Empty,
            60,
            new Thickness(0, 0, 0, 10),
            new SKColor(127, 29, 29));
        bindingValidationPresetError.ForeColor = SKColors.White;
        bindingValidationPresetError.Border = new Thickness(0);

        bindingValidationSubmitButton = new Button
        {
            Name = "bindingValidationSubmitButton",
            Text = "Validate Workflow",
            Dock = DockStyle.Top,
            Height = 42,
            Margin = new Thickness(0, 0, 0, 10)
        };
        bindingValidationSubmitButton.Click += BindingValidationSubmitButton_Click;

        bindingValidationPresetCombo = new ComboBox
        {
            Name = "bindingValidationPresetCombo",
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Thickness(0, 0, 0, 10)
        };
        bindingValidationPresetCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Presets).OneWay();
        bindingValidationPresetCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedPreset).TwoWay();

        bindingValidationTeamCombo = new ComboBox
        {
            Name = "bindingValidationTeamCombo",
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Thickness(0, 0, 0, 10)
        };
        bindingValidationTeamCombo.Link(view => view.Items).FromData((BindingDemoViewModel vm) => vm.Teams).OneWay();
        bindingValidationTeamCombo.Link(view => view.SelectedItem).FromData((BindingDemoViewModel vm) => vm.SelectedTeam).TwoWay();

        bindingValidationTeamCombo.AddValidationRule(new RequiredFieldValidationRule
        {
            ErrorMessage = "Choose a team before deployment validation can pass."
        });
        bindingValidationPresetCombo.AddValidationRule(new RequiredFieldValidationRule
        {
            ErrorMessage = "Choose a preset before deployment validation can pass."
        });
        bindingValidationPresetCombo.AddValidationRule(new CustomValidationRule(
            _ =>
            {
                var teamName = bindingValidationTeamCombo.SelectedItem as string ?? string.Empty;
                var presetName = bindingValidationPresetCombo.SelectedItem as string ?? string.Empty;

                if (presetName.IndexOf("Incident", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    teamName.IndexOf("Ops", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return (false, "Incident presets are reserved for Ops teams in this example workflow.");
                }

                return (true, string.Empty);
            }));

        bindingValidationTeamCombo.SelectedItemChanged += (_, _) =>
        {
            bindingValidationTeamCombo.ValidateNow();
            bindingValidationPresetCombo.ValidateNow();
        };
        bindingValidationPresetCombo.SelectedItemChanged += (_, _) => bindingValidationPresetCombo.ValidateNow();

        bindingValidationRegionError.Link(view => view.Text).From(bindingValidationTeamCombo, (ComboBox combo) => combo.ValidationText).OneWay();
        bindingValidationRegionError.Link(view => view.Visible).From(bindingValidationTeamCombo, (ComboBox combo) => combo.HasValidationError).OneWay();
        bindingValidationPresetError.Link(view => view.Text).From(bindingValidationPresetCombo, (ComboBox combo) => combo.ValidationText).OneWay();
        bindingValidationPresetError.Link(view => view.Visible).From(bindingValidationPresetCombo, (ComboBox combo) => combo.HasValidationError).OneWay();

        validationStack.Controls.Add(bindingValidationStatusCard);
        validationStack.Controls.Add(bindingValidationRegionError);
        validationStack.Controls.Add(bindingValidationPresetError);
        validationStack.Controls.Add(bindingValidationSubmitButton);
        validationStack.Controls.Add(bindingValidationPresetCombo);
        validationStack.Controls.Add(bindingValidationTeamCombo);

        validationShell.Controls.Add(validationGuide);
        validationShell.Controls.Add(validationStack);

        actionRow.Controls.Add(deployButton);
        actionRow.Controls.Add(addTeamButton);
        actionRow.Controls.Add(toggleAlertButton);
        actionRow.Controls.Add(cyclePresetButton);

        _bindingPanel.Controls.Add(statusCard);
        _bindingPanel.Controls.Add(validationShell);
        _bindingPanel.Controls.Add(colorShell);
        _bindingPanel.Controls.Add(taskFooter);
        _bindingPanel.Controls.Add(taskActionRow);
        _bindingPanel.Controls.Add(taskShell);
        _bindingPanel.Controls.Add(actionRow);
        _bindingPanel.Controls.Add(selectionRow);
        _bindingPanel.Controls.Add(alertCard);
        _bindingPanel.Controls.Add(summary);
        _bindingPanel.Controls.Add(hero);
        _bindingPanel.Controls.Add(snippet);
        _bindingPanel.Controls.Add(header);

        Controls.Add(_bindingPanel);
        _bindingPanel.BringToFront();
        PerformLayout();
        Invalidate();
    
    }
}
