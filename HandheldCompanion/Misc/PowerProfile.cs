<<<<<<< HEAD
﻿using HandheldCompanion.Managers;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Misc
{
    [Serializable]
    public class PowerProfile
    {
        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;

                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    foreach (UIElement uIElement in uIElements.Values)
                        uIElement.textBlock1.Text = value;
                });
            }
        }

        private string _Description;
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value;

                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    foreach (UIElement uIElement in uIElements.Values)
                        uIElement.textBlock2.Text = value;
                });
            }
        }

        public string FileName { get; set; }
        public bool Default {  get; set; }

        public Version Version { get; set; } = new();
        public Guid Guid { get; set; } = Guid.NewGuid();

        public bool TDPOverrideEnabled { get; set; }
        public double[] TDPOverrideValues { get; set; }

        public bool CPUOverrideEnabled { get; set; }
        public double CPUOverrideValue { get; set; }

        public bool GPUOverrideEnabled { get; set; }
        public double GPUOverrideValue { get; set; }

        public bool AutoTDPEnabled { get; set; }
        public float AutoTDPRequestedFPS { get; set; } = 30.0f;

        public bool EPPOverrideEnabled { get; set; }
        public uint EPPOverrideValue { get; set; } = 50;

        public bool CPUCoreEnabled { get; set; }
        public int CPUCoreCount { get; set; } = Environment.ProcessorCount;

        public bool CPUBoostEnabled { get; set; }

        public FanProfile FanProfile { get; set; } = new();

        public int OEMPowerMode { get; set; } = 0xFF;
        public Guid OSPowerMode { get; set; } = PowerMode.BetterPerformance;

        private Dictionary<Page, UIElement> uIElements = new();

        public PowerProfile()
        { }

        public PowerProfile(string name, string description)
        {
            Name = name;
            Description = description;

            // Remove any invalid characters from the input
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string output = Regex.Replace(name, "[" + invalidChars + "]", string.Empty);
            output = output.Trim();

            FileName = output;
        }

        public string GetFileName()
        {
            return $"{FileName}.json";
        }

        public bool IsDefault()
        {
            return Default;
        }

        public void DrawUI(Page page)
        {
            if (uIElements.ContainsKey(page))
                return;

            // Add to dictionnary
            UIElement uIElement = new(this, page);
            uIElement.textBlock1.Text = Name;
            uIElement.textBlock2.Text = Description;

            uIElements[page] = uIElement;
        }

        private struct UIElement
        {
            // UI Elements, move me!
            public Button button;
            public Grid grid;
            public DockPanel dockPanel;
            public RadioButtons radioButtons;
            public RadioButton radioButton;
            public SimpleStackPanel simpleStackPanel;
            public TextBlock textBlock1 = new();
            public TextBlock textBlock2 = new();

            public UIElement(PowerProfile powerProfile, Page page)
            {
                // Create a button
                button = new Button();
                button.Height = 66;
                button.Margin = new Thickness(-16);
                button.Padding = new Thickness(50, 12, 12, 12);
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                button.Background = Brushes.Transparent;
                button.BorderBrush = Brushes.Transparent;

                // Stored current profile, might be useful
                button.Tag = powerProfile;

                // Create a grid
                grid = new Grid();

                // Define the Columns
                var colDef1 = new ColumnDefinition
                {
                    Width = new GridLength(10, GridUnitType.Star),
                };
                grid.ColumnDefinitions.Add(colDef1);

                var colDef2 = new ColumnDefinition
                {
                    MinWidth = 40
                };
                grid.ColumnDefinitions.Add(colDef2);

                // Create a dock panel
                dockPanel = new DockPanel();
                dockPanel.HorizontalAlignment = HorizontalAlignment.Left;

                // Create a radio buttons
                radioButtons = new RadioButtons();
                radioButtons.HorizontalAlignment = HorizontalAlignment.Center;

                // Create a radio button
                radioButton = new RadioButton();
                radioButton.GroupName = $"PowerProfile{page.Name}";

                // Create a simple stack panel
                simpleStackPanel = new();
                simpleStackPanel.Margin = new Thickness(0, -10, 0, 0);

                // Create a text block for the controller layout
                textBlock1.Style = (Style)Application.Current.Resources["BodyTextBlockStyle"];

                // Create a text block for the controller layout description
                textBlock2.TextWrapping = TextWrapping.NoWrap;
                textBlock2.Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"];
                textBlock2.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                // Add the text blocks to the simple stack panel
                simpleStackPanel.Children.Add(textBlock1);
                simpleStackPanel.Children.Add(textBlock2);

                // Add the simple stack panel to the radio button control
                radioButton.Content = simpleStackPanel;
                radioButton.Checked += RadioButton_Checked;
                radioButton.Unchecked += RadioButton_Unchecked;

                // Add the radio button to the radio buttons control
                radioButtons.Items.Add(radioButton);

                // Add the radio buttons control to the dock panel
                dockPanel.Children.Add(radioButtons);

                // Create a font icon
                FontIcon fontIcon = new FontIcon();
                fontIcon.Margin = new Thickness(0, 0, 7, 0);
                fontIcon.HorizontalAlignment = HorizontalAlignment.Right;
                fontIcon.FontSize = 12;
                fontIcon.Glyph = "\uE974";
                fontIcon.FontFamily = new("Segoe Fluent Icons");
                Grid.SetColumn(fontIcon, 1);

                // Add the dock panel and the font icon to the grid
                grid.Children.Add(dockPanel);
                grid.Children.Add(fontIcon);

                // Add the grid to the button
                button.Content = grid;
            }

            private void RadioButton_Checked(object sender, RoutedEventArgs e)
            {
                // do something
            }

            private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
            {
                // do something
            }
        }

        public Button GetButton(Page page)
        {
            return uIElements[page].button;
        }

        public RadioButton GetRadioButton(Page page)
        {
            return uIElements[page].radioButton;
        }

        public void Check(Page page)
        {
            uIElements[page].radioButton.IsChecked = true;
        }

        public void Uncheck(Page page)
        {
            uIElements[page].radioButton.IsChecked = false;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
=======
﻿using System;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Misc;

public enum PowerIndexType
{
    AC,
    DC
}

// For a reference on additional subgroup and setting GUIDs, run the command powercfg.exe /qh
// This will list all hidden settings with both the names and GUIDs, descriptions, current values, and allowed values.

public static class PowerSubGroup
{
    public static Guid SUB_PROCESSOR = new("54533251-82be-4824-96c1-47b60b740d00");
}

public static class PowerSetting
{
    public static Guid PERFBOOSTMODE = new("be337238-0d82-4146-a960-4f3749d470c7"); // Processor performance boost mode

    public static Guid
        PROCFREQMAX =
            new("75b0ae3f-bce0-45a7-8c89-c9611c25e100"); // Maximum processor frequency in MHz, 0 for no limit (default)

    public static Guid
        CPMINCORES =
            new("0cc5b647-c1df-4637-891a-dec35c318583"); // Processor performance core parking min cores, expressed as a percent from 0 - 100

    public static Guid
        CPMAXCORES =
            new("ea062031-0e34-4ff1-9b6d-eb1059334028"); // Processor performance core parking max cores, expressed as a percent from 0 - 100

    public static Guid
        PERFEPP = new(
            "36687f9e-e3a5-4dbf-b1dc-15eb381c6863"); // Processor energy performance preference policy, expressed as a percent from 0 - 100

    public static Guid
        PERFEPP1 = new(
            "36687f9e-e3a5-4dbf-b1dc-15eb381c6864"); // Processor energy performance preference policy for Processor Power Efficiency Class 1, expressed as a percent from 0 - 100
}

public enum PerfBoostMode
{
    Disabled = 0,
    Enabled = 1,
    Aggressive = 2,
    EfficientEnabled = 3,
    EfficientAggressive = 4,
    AggressiveAtGuaranteed = 5,
    EfficientAggressiveAtGuaranteed = 6
}

public static class PowerScheme
{
    // Wrapper for the actual PowerGetActiveScheme. Converts GUID to the built-in type on output and handles the LocalFree call.
    /// <summary>
    ///     Retrieves the active power scheme and returns a GUID that identifies the scheme.
    /// </summary>
    /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="ActivePolicyGuid">A pointer that receives a GUID structure.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    private static uint PowerGetActiveScheme(nint UserRootPowerKey, out Guid ActivePolicyGuid)
    {
        var activePolicyGuidPtr = nint.Zero;
        ActivePolicyGuid = Guid.Empty;

        var result = PowerGetActiveScheme(UserRootPowerKey, out activePolicyGuidPtr);

        if (result == 0 && activePolicyGuidPtr != nint.Zero)
        {
            ActivePolicyGuid = (Guid)Marshal.PtrToStructure(activePolicyGuidPtr, typeof(Guid));
            LocalFree(activePolicyGuidPtr);
        }

        return result;
    }

    public static bool GetActiveScheme(out Guid ActivePolicyGuid)
    {
        return PowerGetActiveScheme(nint.Zero, out ActivePolicyGuid) == 0;
    }

    public static bool SetActiveScheme(Guid SchemeGuid)
    {
        return PowerSetActiveScheme(nint.Zero, SchemeGuid) == 0;
    }

    public static bool GetValue(PowerIndexType powerType, Guid SchemeGuid, Guid SubGroupOfPowerSettingsGuid,
        Guid PowerSettingGuid, out uint value)
    {
        switch (powerType)
        {
            case PowerIndexType.AC:
                return PowerReadACValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    out value) == 0;
            case PowerIndexType.DC:
                return PowerReadDCValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    out value) == 0;
        }

        value = 0;
        return false;
    }

    public static bool SetValue(PowerIndexType powerType, Guid SchemeGuid, Guid SubGroupOfPowerSettingsGuid,
        Guid PowerSettingGuid, uint value)
    {
        switch (powerType)
        {
            case PowerIndexType.AC:
                return PowerWriteACValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    value) == 0;
            case PowerIndexType.DC:
                return PowerWriteDCValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    value) == 0;
        }

        return false;
    }

    public static bool SetAttribute(Guid SubGroupOfPowerSettingsGuid, Guid PowerSettingGuid, bool hide)
    {
        return PowerWriteSettingAttributes(SubGroupOfPowerSettingsGuid, PowerSettingGuid, (uint)(hide ? 1 : 0)) == 0;
    }

    public static uint[] ReadPowerCfg(Guid SubGroup, Guid Settings)
    {
        var results = new uint[2];

        if (GetActiveScheme(out var currentScheme))
        {
            // read AC/DC values
            GetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings,
                out results[(int)PowerIndexType.AC]);
            GetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings,
                out results[(int)PowerIndexType.DC]);
        }

        return results;
    }

    public static void WritePowerCfg(Guid SubGroup, Guid Settings, uint ACValue, uint DCValue)
    {
        if (GetActiveScheme(out var currentScheme))
        {
            // unhide attribute
            SetAttribute(SubGroup, Settings, false);

            // set value(s)
            SetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings, ACValue);
            SetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings, DCValue);

            // activate scheme
            SetActiveScheme(currentScheme);
        }
    }

    #region imports

    /// <summary>
    ///     Retrieves the active power scheme and returns a GUID that identifies the scheme.
    /// </summary>
    /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="ActivePolicyGuid">
    ///     A pointer that receives a pointer to a GUID structure. Use the LocalFree function to
    ///     free this memory.
    /// </param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
    private static extern uint PowerGetActiveScheme(nint UserRootPowerKey, out nint ActivePolicyGuid);

    /// <summary>
    ///     Sets the active power scheme for the current user.
    /// </summary>
    /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
    private static extern uint PowerSetActiveScheme(nint UserRootPowerKey, in Guid SchemeGuid);

    /// <summary>
    ///     Retrieves the AC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="AcValueIndex">A pointer to a variable that receives the AC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerReadACValueIndex")]
    private static extern uint PowerReadACValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, out uint AcValueIndex);

    /// <summary>
    ///     Retrieves the DC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="DcValueIndex">A pointer to a variable that receives the DC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerReadDCValueIndex")]
    private static extern uint PowerReadDCValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, out uint DcValueIndex);

    /// <summary>
    ///     Sets the AC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="AcValueIndex">The AC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
    private static extern uint PowerWriteACValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, uint AcValueIndex);

    /// <summary>
    ///     Sets the DC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="DcValueIndex">The DC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerWriteDCValueIndex")]
    private static extern uint PowerWriteDCValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, uint DcValueIndex);

    /// <summary>
    ///     Frees the specified local memory object and invalidates its handle.
    /// </summary>
    /// <param name="hMem">A handle to the local memory object.</param>
    /// <returns>
    ///     If the function succeeds, the return value is zero, and if the function fails, the return value is equal to a
    ///     handle to the local memory object.
    /// </returns>
    [DllImport("kernel32.dll", EntryPoint = "LocalFree")]
    private static extern nint LocalFree(nint hMem);

    [DllImport("powrprof.dll", EntryPoint = "PowerWriteSettingAttributes")]
    private static extern uint PowerWriteSettingAttributes(in Guid SubGroupOfPowerSettingsGuid,
        in Guid PowerSettingGuid, uint Attributes);

    #endregion
}
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
