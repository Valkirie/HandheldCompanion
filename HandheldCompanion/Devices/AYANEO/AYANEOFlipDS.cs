using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using System;
using System.Linq;
using System.Windows;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEOFlipDS : AYANEOFlipKB
{
    public AYANEOFlipDS()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_flip_ds";
        this.ProductModel = "AYANEO Flip DS";

        // TODO: Check if there really is no RGB but looks like it
        this.Capabilities -= DeviceCapabilities.DynamicLighting;
        this.Capabilities -= DeviceCapabilities.DynamicLightingBrightness;

        // TODO: Add OEMChords for "Dual-Screen Keys" key here
        this.OEMChords.Add(new KeyboardChord("Custom Key Screen",
            [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18],
            [KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey],
            false, ButtonFlags.OEM5
        ));
    }

    public override void Initialize(bool FirstStart, bool NewUpdate)
    {
        if (FirstStart)
        {
            // set Quicktools to Maximize on bottom screen
            ManagerFactory.settingsManager.SetProperty("QuickToolsLocation", 2);
            ManagerFactory.settingsManager.SetProperty("QuickToolsDeviceName", "AYANEOQHD");
        }

        if (NewUpdate)
        {
            string currentVersion = MainWindow.CurrentVersion.ToString();
            switch (currentVersion)
            {
                case "0.24.0.13":
                    ManagerFactory.settingsManager.SetProperty("QuickKeyboardVisibility", "True");
                    ManagerFactory.settingsManager.SetProperty("QuickTrackpadVisibility", "True");
                    break;
            }
        }
    }

    public override void OpenEvents()
    {
        base.OpenEvents();

        // manage events
        ControllerManager.InputsUpdated += ControllerManager_InputsUpdated;
    }

    private ButtonState prevState = new();
    private void ControllerManager_InputsUpdated(ControllerState Inputs)
    {
        if (prevState.Equals(Inputs.ButtonState))
            return;

        // update previous state
        ButtonState.Overwrite(Inputs.ButtonState, prevState);

        // if screen button is pressed, turn on bottom screen
        if (Inputs.ButtonState.Buttons.Contains(ButtonFlags.OEM5))
        {
            bool enabled = ManagerFactory.settingsManager.GetBoolean("AYANEOFlipScreenEnabled");
            ManagerFactory.settingsManager.SetProperty("AYANEOFlipScreenEnabled", !enabled);
        }
    }

    protected override void QuerySettings()
    {
        // raise events
        SettingsManager_SettingValueChanged("AYANEOFlipScreenEnabled", ManagerFactory.settingsManager.GetString("AYANEOFlipScreenEnabled"), false);
        SettingsManager_SettingValueChanged("AYANEOFlipScreenBrightness", ManagerFactory.settingsManager.GetString("AYANEOFlipScreenBrightness"), false);

        base.QuerySettings();
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "AYANEOFlipScreenEnabled":
                {
                    bool enabled = Convert.ToBoolean(value);
                    switch (enabled)
                    {
                        case true:
                            OverlayQuickTools.GetCurrent().SetVisibility(Visibility.Visible);
                            short brightness = (short)ManagerFactory.settingsManager.GetDouble("AYANEOFlipScreenBrightness");
                            CEcControl_SetSecDispBrightness(brightness);
                            break;

                        case false:
                            CEcControl_SetSecDispBrightness(0);
                            OverlayQuickTools.GetCurrent().SetVisibility(Visibility.Collapsed);
                            break;
                    }
                }
                break;
            case "AYANEOFlipScreenBrightness":
                {
                    bool enabled = ManagerFactory.settingsManager.GetBoolean("AYANEOFlipScreenEnabled");
                    if (enabled)
                    {
                        short brightness = (short)Convert.ToDouble(value);
                        CEcControl_SetSecDispBrightness(brightness);
                    }
                }
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM5:
                return "\u23CD";
        }

        return base.GetGlyph(button);
    }

    public void CEcControl_SetSecDispBrightness(short brightness)
    {
        this.ECRAMWrite(0x4e, (byte)((brightness * 0xff) / 100));
    }
}