using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using System;
using System.Linq;
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

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ControllerManager.InputsUpdated += ControllerManager_InputsUpdated;
    }

    private ButtonState prevState = new();
    private void ControllerManager_InputsUpdated(ControllerState Inputs)
    {
        if (prevState.Equals(Inputs.ButtonState))
            return;

        // update previous state
        prevState = Inputs.ButtonState.Clone() as ButtonState;

        // if screen button is pressed, turn on bottom screen
        if (Inputs.ButtonState.Buttons.Contains(ButtonFlags.OEM5))
        {
            bool enabled = SettingsManager.GetBoolean("AYANEOFlipScreenEnabled");
            if (!enabled)
                SettingsManager.SetProperty("AYANEOFlipScreenEnabled", true);
        }
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "AYANEOFlipScreenEnabled":
                {
                    bool enabled = Convert.ToBoolean(value);
                    switch (enabled)
                    {
                        case true:
                            short brightness = (short)SettingsManager.GetDouble("AYANEOFlipScreenBrightness");
                            CEcControl_SetSecDispBrightness(brightness);
                            break;

                        case false:
                            CEcControl_SetSecDispBrightness(0);
                            OverlayQuickTools.GetCurrent().ToggleVisibility();
                            break;
                    }
                }
                break;
            case "AYANEOFlipScreenBrightness":
                {
                    bool enabled = SettingsManager.GetBoolean("AYANEOFlipScreenEnabled");
                    if (enabled)
                    {
                        short brightness = (short)Convert.ToDouble(value);
                        CEcControl_SetSecDispBrightness(brightness);
                    }
                }
                break;
        }
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