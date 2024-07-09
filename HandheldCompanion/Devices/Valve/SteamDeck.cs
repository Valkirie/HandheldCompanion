using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class SteamDeck : IDevice
{
    private const ushort IO6C = 0x6C;

    public const ushort MAX_FAN_RPM = 0x1C84;

    // Those addresses are taken from DSDT for VLV0100
    // and might change at any time with a BIOS update
    // Purpose: https://lore.kernel.org/lkml/20220206022023.376142-1-andrew.smirnov@gmail.com/
    // Addresses: DSDT.txt
    // Author: Kamil Trzciński, 2022 (https://github.com/ayufan/steam-deck-tools/)
    private static readonly IntPtr FSLO_FSHI = new(0xFE700B00 + 0x92);
    private static readonly IntPtr GNLO_GNHI = new(0xFE700B00 + 0x95);
    private static readonly IntPtr FRPR = new(0xFE700B00 + 0x97);
    private static IntPtr FNRL_FNRH = new(0xFE700300 + 0xB0);
    private static IntPtr FNCK = new(0xFE700300 + 0x9F);
    private static IntPtr BATH_BATL = new(0xFE700400 + 0x6E);
    private static readonly IntPtr PDFV = new(0xFE700C00 + 0x4C);
    private static readonly IntPtr XBID = new(0xFE700300 + 0xBD);
    private static readonly IntPtr PDCT = new(0xFE700C00 + 0x01);
    private static readonly IntPtr MCBL = new(0xFE700B00 + 0x9F);

    public struct DeviceVersion
    {
        public ushort Firmware { get; set; }
        public byte BoardID { get; set; }
        public byte PDCS { get; set; }

        public bool BatteryTempLE { get; set; }
        public bool MaxBatteryCharge { get; set; }

        public bool IsSupported(ushort deviceFirmware, byte deviceBoardID, byte devicePDCS)
        {
            if (Firmware != 0 && Firmware != deviceFirmware)
                return false;
            if (BoardID != 0 && BoardID != deviceBoardID)
                return false;
            if (PDCS != 0 && PDCS != devicePDCS)
                return false;
            return true;
        }
    };

    private static readonly DeviceVersion[] deviceVersions =
    {
        // Steam Deck - LCD version
        new DeviceVersion() { Firmware = 0xB030, BoardID = 0x6, PDCS = 0 /* 0x2B */, BatteryTempLE = false },
        new DeviceVersion() { Firmware = 0xB030, BoardID = 0xA, PDCS = 0 /* 0x2B */, BatteryTempLE = false, MaxBatteryCharge = true },
        // Steam Deck - OLED version
        new DeviceVersion() { Firmware = 0x1030, BoardID = 0x5, PDCS = 0 /* 0x2F */, BatteryTempLE = true },
        new DeviceVersion() { Firmware = 0x1050, BoardID = 0x5, PDCS = 0 /* 0x2F */, BatteryTempLE = true, MaxBatteryCharge = true },
        new DeviceVersion() { Firmware = 0x1010, BoardID = 0x5, PDCS = 0 /* 0x2F */, BatteryTempLE = true, MaxBatteryCharge = true },
    };

    public bool BatteryTempLE { get; set; }
    public bool MaxBatteryCharge { get; set; }

    private InpOut inpOut;

    public SteamDeck()
    {
        // device specific settings
        ProductIllustration = "device_valve_jupiter";
        ProductModel = "SteamDeck";

        // Steam Controller Neptune
        Capabilities = DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;

        // https://www.steamdeck.com/en/tech
        nTDP = new double[] { 10, 10, 15 };
        cTDP = new double[] { 4, 15 };

        // https://www.techpowerup.com/gpu-specs/steam-deck-gpu.c3897
        GfxClock = new double[] { 200, 2500 };
        CpuClock = 3500;

        OEMChords.Add(new KeyboardChord("...",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u21E5";
        }

        return defaultGlyph;
    }

    public static ushort FirmwareVersion { get; private set; }
    public static byte BoardID { get; private set; }
    public static byte PDCS { get; private set; }

    public override bool IsOpen => inpOut is not null;
    public DeviceVersion? SupportedDevice => deviceVersions.FirstOrDefault((v) => v.IsSupported(FirmwareVersion, BoardID, PDCS));
    public override bool IsSupported => SupportedDevice is not null;

    public override bool Open()
    {
        if (inpOut != null)
            return true;

        try
        {
            inpOut = new InpOut();

            var data = inpOut?.ReadMemory(PDFV, 2);
            if (data is not null)
                FirmwareVersion = BitConverter.ToUInt16(data);
            else
                FirmwareVersion = 0xFFFF;

            data = inpOut?.ReadMemory(XBID, 1);
            if (data is not null)
                BoardID = data[0];
            else
                BoardID = 0xFF;

            data = inpOut?.ReadMemory(PDCT, 1);
            if (data is not null)
                PDCS = data[0];
            else
                PDCS = 0xFF;

            LogManager.LogInformation("FirmwareVersion: {0}, BoardID: {1}", FirmwareVersion, BoardID);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't initialise VLV0100. ErrorCode: {0}", ex.Message);
            Close();
            return false;
        }
    }

    public override void Close()
    {
        inpOut.Dispose();
        inpOut = null;

        base.Close();
    }

    private void SetGain(ushort gain)
    {
        if (!IsOpen || !IsSupported)
            return;

        var data = BitConverter.GetBytes(gain);
        inpOut.WriteMemory(GNLO_GNHI, data);
    }

    private void SetRampRate(byte rampRate)
    {
        if (!IsOpen || !IsSupported)
            return;

        var data = BitConverter.GetBytes((short)rampRate);
        inpOut.WriteMemory(FRPR, data);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        if (!IsOpen || !IsSupported)
            return;

        SetGain(10);
        SetRampRate(enable ? (byte)10 : (byte)20);

        inpOut.DlPortWritePortUchar(IO6C, enable ? (byte)0xCC : (byte)0xCD);
    }

    public override void SetFanDuty(double percent)
    {
        if (!IsOpen || !IsSupported)
            return;

        var rpm = (ushort)(MAX_FAN_RPM * percent / 100.0d);
        if (rpm > MAX_FAN_RPM)
            rpm = MAX_FAN_RPM;

        var data = BitConverter.GetBytes(rpm);
        inpOut.WriteMemory(FSLO_FSHI, data);
    }

    public override float ReadFanDuty()
    {
        var data = inpOut?.ReadMemory(FNRL_FNRH, 2);
        if (data is null)
            return 0.0f;
        return BitConverter.ToUInt16(data);
    }

    public float GetBattTemperature()
    {
        var data = inpOut?.ReadMemory(BATH_BATL, 2);
        if (data is null)
            return 0;
        int value = SupportedDevice?.BatteryTempLE == true ?
            ((data[1] << 8) + data[0]) :
            ((data[0] << 8) + data[1]);
        return (float)(value - 0x0AAC) / 10.0f;
    }

    public int? GetMaxBatteryCharge()
    {
        if (SupportedDevice?.MaxBatteryCharge != true)
            return null;
        var data = inpOut?.ReadMemory(MCBL, 1);
        if (data is null)
            return null;
        if (data[0] > 100)
            return null;
        return data[0];
    }

    public void SetMaxBatteryCharge(int chargeLimit)
    {
        if (SupportedDevice?.MaxBatteryCharge != true)
            return;
        if (chargeLimit < 0 || chargeLimit > 100)
            return;
        byte[] data = BitConverter.GetBytes(chargeLimit);
        inpOut?.WriteMemory(MCBL, data);
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                {
                    bool enabled = Convert.ToBoolean(value);
                    switch (enabled)
                    {
                        case true:
                            SetMaxBatteryCharge(80);
                            break;
                        case false:
                            SetMaxBatteryCharge(100);
                            break;
                    }
                }
                break;
        }
    }
}