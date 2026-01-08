using System;
using System.Windows;
using System.Windows.Controls;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

public partial class LayoutItemPage : Page
{
    // page vars
    private ActionSettingsPage actionSettingsPage;


    public MappingViewModel? CurrentMapping { get; private set; }

    public LayoutItemPage()
    {
        InitializeComponent();
    }

    public LayoutItemPage(string Tag, object parent) : this()
    {
        this.Tag = Tag;
    }

    // Initialize pages
    public void Initialize()
    {
        if (actionSettingsPage is not null)
            return;

        actionSettingsPage = new ActionSettingsPage();
        ContentFrame.Navigate(actionSettingsPage);
    }

    public void SetMapping(MappingViewModel mapping)
    {
        CurrentMapping = mapping;
        
        // Get profile name
        string profileName = MainWindow.layoutPage.currentTemplate.Product;
        if (string.IsNullOrEmpty(profileName))
            profileName = Properties.Resources.LayoutPage_LaytouDesktop;
        
        // Get input name from parent stack (works for Button, Trigger, and Axis)
        string inputName = "Unknown Input";
        if (mapping is ButtonMappingViewModel buttonMapping)
            inputName = buttonMapping.ParentStack?.Name ?? "Unknown Button";
        else if (mapping is TriggerMappingViewModel triggerMapping)
            inputName = triggerMapping.ParentStack?.Name ?? "Unknown Trigger";
        else if (mapping is AxisMappingViewModel axisMapping)
            inputName = axisMapping.ParentStack?.Name ?? "Unknown Axis";
        
        // Update title: "<ProfileName>: <input to be configured>"
        ActionTitle.Text = $"{profileName}: {inputName}";
        
        // Update description
        if (mapping?.Action is not null && mapping.SelectedTarget is not null)
        {
            string actionType = mapping.ActionTypeIndex switch
            {
                0 => "Disabled",
                1 => "Button",
                2 => "Joystick",
                3 => "Keyboard",
                4 => "Mouse",
                5 => "Trigger",
                6 => "Shift",
                7 => "Inherit",
                _ => "Unknown"
            };
            ActionDescription.Text = $"{actionType}: {mapping.SelectedTarget.Content}";
        }
        else
        {
            ActionDescription.Text = "Configure action settings";
        }
        
        // Update the settings page with the mapping
        if (actionSettingsPage is not null)
        {
            actionSettingsPage.SetMapping(mapping);
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Set focus to the page so UIGamepad can track it
        Focus();
    }
}
