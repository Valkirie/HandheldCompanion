﻿using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
using Windows.System.Power;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices.AYANEO
{
    // Base class implementing FAN/RGB control for all AYANEO device using the CEc protocol
    // All devices that arent CEii or CHc (MiniPC)
    public class AYANEODeviceCEc : AYANEODevice
    {
        protected int[] rgbZones = { 1, 2, 3, 4 };
        protected bool rgbConfirmation = true;

        private bool? ledStatus;
        private int? ledBrightness;
        private Color? ledColorSticks;
        private Color? ledColorAYA;
        private LEDLevel? ledLevel;

        public AYANEODeviceCEc()
        {
            this.Capabilities |= DeviceCapabilities.FanControl;
            this.Capabilities |= DeviceCapabilities.DynamicLighting;
            this.Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
            this.Capabilities |= DeviceCapabilities.BatteryChargeLimit;
            this.Capabilities |= DeviceCapabilities.BatteryChargeLimitPercent;

            this.DynamicLightingCapabilities = LEDLevel.SolidColor;

            this.ECDetails = new ECDetails
            {
                AddressStatusCommandPort = 0x4E,
                AddressDataPort = 0x4F,
                AddressFanControl = 0x44A,
                AddressFanDuty = 0x44B,
                FanValueMin = 0,
                FanValueMax = 100
            };

            this.GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
            this.GyrometerAxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            };

            this.AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
            this.AccelerometerAxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            };

            this.OEMChords.Add(new KeyboardChord("Custom Key Big",
                [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17],
                [KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey],
                false, ButtonFlags.OEM1
            ));
            this.OEMChords.Add(new KeyboardChord("Custom Key Small",
                [KeyCode.LWin, KeyCode.D],
                [KeyCode.LWin, KeyCode.D],
                false, ButtonFlags.OEM2
            ));
            this.OEMChords.Add(new KeyboardChord("Custom Key Top Left",
                [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15],
                [KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey],
                false, ButtonFlags.OEM3
            ));
            this.OEMChords.Add(new KeyboardChord("Custom Key Top Right",
                [KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16],
                [KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey],
                false, ButtonFlags.OEM4
            ));
        }

        public override bool Open()
        {
            bool success = base.Open();
            if (!success)
                return false;

            lock (this.updateLock)
            {
                this.CEcControl_RgbHoldControl();
            }

            return true;
        }

        public override void OpenEvents()
        {
            base.OpenEvents();

            // manage events
            PowerManager.RemainingChargePercentChanged += PowerManager_RemainingChargePercentChanged;
        }

        protected override void QuerySettings()
        {
            // raise events
            SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetString("BatteryChargeLimit"), false);

            base.QuerySettings();
        }

        public override void Close()
        {
            lock (this.updateLock)
            {
                this.CEcControl_RgbReleaseControl();
            }

            PowerManager.RemainingChargePercentChanged -= PowerManager_RemainingChargePercentChanged;

            base.Close();
        }

        protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "BatteryChargeLimit":
                    {
                        bool enabled = Convert.ToBoolean(value);
                        switch (enabled)
                        {
                            case true:
                                CEcControl_BypassChargeOpen();
                                break;
                            case false:
                                CEcControl_BypassChargeClose();
                                break;
                        }
                    }
                    break;
            }

            base.SettingsManager_SettingValueChanged(name, value, temporary);
        }

        private void PowerManager_RemainingChargePercentChanged(object? sender, object e)
        {
            // Check if BatteryChargeLimit is enabled in SettingsManager
            bool isBatteryChargeLimitEnabled = ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit");
            if (!isBatteryChargeLimitEnabled)
                return;

            int BatteryChargeLimit = ManagerFactory.settingsManager.GetInt("BatteryChargeLimitPercent");

            // Get the current battery percentage
            int batteryPercentage = PowerManager.RemainingChargePercent;
            if (batteryPercentage >= BatteryChargeLimit)
            {
                // Call the function to open the charge bypass
                CEcControl_BypassChargeOpen();
            }
            else if (batteryPercentage < BatteryChargeLimit)
            {
                // Call the function to close the charge bypass
                CEcControl_BypassChargeClose();
            }
        }

        public override bool SetLedStatus(bool status)
        {
            lock (this.updateLock)
            {
                if (this.ledStatus == status) return true;
                this.ledStatus = status;

                if ((bool)this.ledStatus)
                {
                    this.CEcRgb_GlobalOn(LEDGroup.StickBoth);
                    if (this.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor)) this.CEcRgb_GlobalOn(LEDGroup.AYA);
                }
                else
                {
                    this.CEcRgb_GlobalOff(LEDGroup.StickBoth);
                    if (this.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor)) this.CEcRgb_GlobalOff(LEDGroup.AYA);
                }

                return true;
            }
        }

        public override bool SetLedBrightness(int brightness)
        {
            lock (this.updateLock)
            {
                if (this.ledBrightness == brightness) return true;
                this.ledBrightness = brightness;

                if (this.ledColorSticks == null || this.ledColorAYA == null) return true;

                this.CEcRgb_SetColorAll(LEDGroup.StickLeft, (Color)this.ledColorSticks);
                this.CEcRgb_SetColorAll(LEDGroup.StickRight, (Color)this.ledColorSticks);
                if (this.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor)) this.CEcRgb_SetColorAya((Color)this.ledColorAYA);

                return true;
            }
        }

        public override bool SetLedColor(Color colorSticks, Color colorAYA, LEDLevel level, int speed)
        {
            lock (this.updateLock)
            {
                bool hasChangedSticks = this.ledColorSticks != colorSticks;
                bool hasChangedAYA = this.ledColorAYA != colorAYA;
                this.ledColorSticks = colorSticks;
                this.ledColorAYA = colorAYA;
                this.ledLevel = level;

                if (this.ledBrightness == null)
                {
                    return true;
                }

                switch ((LEDLevel)this.ledLevel)
                {
                    case LEDLevel.SolidColor:
                        if (hasChangedSticks)
                        {
                            this.CEcRgb_SetColorAll(LEDGroup.StickLeft, (Color)this.ledColorSticks);
                            this.CEcRgb_SetColorAll(LEDGroup.StickRight, (Color)this.ledColorSticks);
                        }
                        if (hasChangedAYA && this.Capabilities.HasFlag(DeviceCapabilities.DynamicLightingSecondLEDColor))
                        {
                            this.CEcRgb_SetColorAya((Color)this.ledColorAYA);
                        }
                        break;
                }

                return true;
            }
        }

        private void CEcRgb_GlobalOn(LEDGroup group, byte speed = 0x00)
        {
            this.CEcRgb_I2cWrite(group, 0x02, (byte)(0x80 + speed));
        }

        private void CEcRgb_GlobalOff(LEDGroup group)
        {
            this.CEcRgb_I2cWrite(group, 0x02, 0xc0);
        }

        private void CEcRgb_SetColorAll(LEDGroup group, Color color)
        {
            foreach (int zone in this.rgbZones)
            {
                byte[] colorBytes = this.MapColorValues(zone, color);
                this.CEcRgb_SetColorOne(group, zone, colorBytes[0], colorBytes[1], colorBytes[2]);
            }
        }

        private void CEcRgb_SetColorAya(Color color)
        {
            this.CEcRgb_SetColorOne(LEDGroup.AYA, 4, color.B, color.R, color.G);
        }

        private void CEcRgb_SetColorOne(LEDGroup group, int zone, int red, int green, int blue)
        {
            byte zoneByte = (byte)(zone * 0x03);
            byte redByte = (byte)this.CEcRgb_GetBrightness(red);
            byte greenByte = (byte)this.CEcRgb_GetBrightness(green);
            byte blueByte = (byte)this.CEcRgb_GetBrightness(blue);

            this.CEcRgb_I2cWrite(group, zoneByte, redByte);
            this.CEcRgb_I2cWrite(group, (byte)(zoneByte + 0x01), greenByte);
            this.CEcRgb_I2cWrite(group, (byte)(zoneByte + 0x02), blueByte);
        }

        private int CEcRgb_GetBrightness(int color)
        {
            return (int)(((((float)color / 255) * 192) / 100) * (float)this.ledBrightness);
        }

        protected virtual byte[] MapColorValues(int zone, Color color)
        {
            return [color.R, color.G, color.B];
        }

        protected virtual void CEcRgb_I2cWrite(LEDGroup group, byte command, byte argument)
        {
            this.CEcControl_RgbI2cWrite(group, command, argument);
            if (this.rgbConfirmation) this.CEcControl_RgbI2cWrite(LEDGroup.StickBoth, 0x00, 0x00);
        }

        protected virtual void CEcControl_RgbI2cWrite(LEDGroup group, byte command, byte argument)
        {
            this.ECRAMWrite(0x6d, (byte)group);
            this.ECRAMWrite(0xb1, command);
            this.ECRAMWrite(0xb2, argument);
            this.ECRAMWrite(0xbf, 0x10);
            Thread.Sleep(5); // AYASpace does this so copied it here
            this.ECRAMWrite(0xbf, 0xfe);
        }

        protected virtual void CEcControl_RgbHoldControl()
        {
            this.ECRAMWrite(0xbf, 0xfe);
        }

        protected virtual void CEcControl_RgbReleaseControl()
        {
            this.ECRAMWrite(0xbf, 0x00);
        }

        protected virtual void CEcControl_BypassChargeOpen()
        {
            if (this.ECRamReadByte(0x1e) != 0x55)
                this.ECRAMWrite(0x1e, 0x55);
        }

        protected virtual void CEcControl_BypassChargeClose()
        {
            if (this.ECRamReadByte(0x1e) != 0xaa)
                this.ECRAMWrite(0x1e, 0xaa);
        }
    }
}
