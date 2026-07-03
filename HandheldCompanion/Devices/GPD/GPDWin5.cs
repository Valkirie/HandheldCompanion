using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using WindowsInput.Events;
using Task = System.Threading.Tasks.Task;

namespace HandheldCompanion.Devices;

public class GPDWin5 : IDevice
{
    private const int GpdVendorId = 0x2F24;
    private const int GpdBackButtonsPid = 0x0137;
    private const int GpdLegacyPid = 0x0135;
    private const int BackButtonsHidId = 0x0137;

    // EC (GPD Duo/Win5 map via 0x4E/0x4F)
    private const ushort EC_RPM_HI = 0x0478; // read
    private const ushort EC_RPM_LO = 0x0479; // read
    private const ushort EC_FAN_DUTY_1 = 0x047A; // write (0=auto)
    private const ushort EC_FAN_DUTY_2 = 0x047B; // write (0=auto)

    private bool isReading;
    private Version FirmwareVersion = new(0, 0);
    private bool IsHidSupported => FirmwareVersion >= new Version(0x01, 0x11);

    public GPDWin5()
    {
        ProductIllustration = "device_gpd5";
        UseOpenLib = true;

        vendorId = GpdVendorId;
        productIds = [GpdBackButtonsPid, GpdLegacyPid];
        hidFilters = new()
        {
            { GpdBackButtonsPid, new HidFilter(unchecked((short)0xFF00), 0x0001) },
            { GpdLegacyPid, new HidFilter(unchecked((short)0xFF00), 0x0001) },
        };

        // https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-385.html
        nTDP = new double[] { 55, 55, 75 };
        cTDP = new double[] { 8, 85 };

        // Todo: get exact processor names and use switch/case
        string Processor = MotherboardInfo.ProcessorName;
        if (Processor.Contains("385"))
        {
            GfxClock = new double[] { 500, 2800 };
            CpuClock = 5000;
        }
        else if (Processor.Contains("395"))
        {
            GfxClock = new double[] { 500, 2900 };
            CpuClock = 5100;
        }

        Capabilities = DeviceCapabilities.FanControl;

        // Win 5 uses the 4E/4F SIO window; fan value range is 0..244
        ECDetails = new ECDetails
        {
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            AddressFanControl = 0x0000, // not used on this device
            AddressFanDuty = 0x0000,    // not used; we write both fans directly
            FanValueMin = 0,
            FanValueMax = 244
        };

        GyroMatrix = new()
        {
            Axis = new Vector3(1.0f, -1.0f, -1.0f),
            AxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'Y' },
                { 'Y', 'Z' },
                { 'Z', 'X' }
            }
        };

        AcceleroMatrix = new()
        {
            Axis = new Vector3(-1.0f, -1.0f, 1.0f),
            AxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            }
        };

        // GPD Win 5 specific chords
        // todo: figure out which value is which button
        /*
         * F13 = 0x7C
         * F14 = 0x7D
         * F15 = 0x7E
        */

        OEMChords.Add(new KeyboardChord("Keyboard", [KeyCode.LControl, KeyCode.LWin, KeyCode.O], [KeyCode.O, KeyCode.LWin, KeyCode.LControl], false, ButtonFlags.OEM5));
        OEMChords.Add(new KeyboardChord("Home", [KeyCode.LWin, KeyCode.D], [KeyCode.D, KeyCode.LWin], false, ButtonFlags.OEM6));

        // Legacy support for older firmware
        OEMChords.Add(new KeyboardChord("L4", [KeyCode.LControl, KeyCode.LShift, KeyCode.F14], [KeyCode.F14, KeyCode.LControl, KeyCode.LShift], false, ButtonFlags.OEM3));
        OEMChords.Add(new KeyboardChord("R4", [KeyCode.F3, KeyCode.F15], [KeyCode.F15, KeyCode.F3], false, ButtonFlags.OEM2));
        OEMChords.Add(new KeyboardChord("Gamepad", [KeyCode.F13], [KeyCode.F13], false, ButtonFlags.OEM4));

        // Disabled this one as Win 5 also sends an Xbox guide input when Menu key is pressed.
        OEMChords.Add(new KeyboardChord("Menu", [KeyCode.LButton | KeyCode.XButton2], [KeyCode.LButton | KeyCode.XButton2], true, ButtonFlags.OEM1));
    }

    public override bool IsReady()
    {
        // Early return if device is already bound and connected
        if (hidDevices.TryGetValue(BackButtonsHidId, out HidDevice? boundDevice))
        {
            if (boundDevice.IsConnected /* && boundDevice.IsOpen */)
                return true;
        }

        IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (!hidFilters.TryGetValue(device.Attributes.ProductId, out HidFilter hidFilter))
                continue;

            if (device.Capabilities.UsagePage != hidFilter.UsagePage ||
                device.Capabilities.Usage != hidFilter.Usage)
                continue;

            hidDevices[BackButtonsHidId] = device;
            return true;
        }

        return false;
    }

    public override bool Open()
    {
        bool success = base.Open();
        if (!success)
            return false;

        if (hidDevices.TryGetValue(BackButtonsHidId, out HidDevice? device))
        {
            device.OpenDevice();

            (int major, int minor)? firmware = ReadControllerFirmwareVersion(device);
            if (firmware.HasValue)
            {
                FirmwareVersion = new Version(firmware.Value.major, firmware.Value.minor);
                LogManager.LogInformation("{0}: Controller firmware version 0x{1:X2}.0x{2:X2}", GetType().Name, firmware.Value.major, firmware.Value.minor);
            }
            else
            {
                LogManager.LogWarning("{0}: Could not read controller firmware version", GetType().Name);
            }

            device.CloseDevice();
        }

        return true;
    }

    public override void Close()
    {
        // Release all back buttons so none remain logically pressed after disconnect.
        lock (updateLock)
        {
            KeyRelease(ButtonFlags.OEM2); // R4
            KeyRelease(ButtonFlags.OEM3); // L4
            KeyRelease(ButtonFlags.OEM4); // Switch
        }

        lock (updateLock)
        {
            foreach (HidDevice hidDevice in hidDevices.Values)
                hidDevice.Dispose();
            hidDevices.Clear();
        }

        base.Close();
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        // On Win 5, "auto" is simply duty 0 on both fan registers.
        if (!enable)
        {
            if (!UseOpenLib || !IsOpen) return;
            ECRamDirectWriteByte(EC_FAN_DUTY_1, ECDetails, 0x00);
            ECRamDirectWriteByte(EC_FAN_DUTY_2, ECDetails, 0x00);
            return;
        }
    }

    public override void SetFanDuty(double percent)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        // Clamp and scale to 0..244 (Win 5 max)
        if (percent < 0) percent = 0;
        if (percent > 100) percent = 100;

        double dutyD = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100.0 + ECDetails.FanValueMin;
        byte duty = Convert.ToByte(dutyD);

        // Manual duty for both fans
        ECRamDirectWriteByte(EC_FAN_DUTY_1, ECDetails, duty);
        ECRamDirectWriteByte(EC_FAN_DUTY_2, ECDetails, duty);
    }

    public override float ReadFanDuty()
    {
        if (!UseOpenLib || !IsOpen)
            return 0;

        byte hi = ECRamDirectReadByte(EC_RPM_HI, ECDetails);
        byte lo = ECRamDirectReadByte(EC_RPM_LO, ECDetails);
        return (hi << 8) | lo;
    }

    public bool GetOnlyTypeC()
    {
        if (!UseOpenLib || !IsOpen)
            return false;

        byte val = ECRamDirectReadByte(0x0577, ECDetails);
        return val == 0x02 || val == 0x10; // Device is running on Type-C only
    }

    private (int major, int minor)? ReadControllerFirmwareVersion(HidDevice device)
    {
        try
        {
            HidReport request = device.CreateReport();
            request.ReportId = 0x01;
            request.Data[0] = 0x41;

            if (!device.WriteReportSync(request))
                return null;

            Thread.Sleep(50); // controller needs a moment.

            // Read input report ID 0x01
            HidReport response = device.ReadReportSync(0x01);
            if (response?.Data == null || response.Data.Length < 13)
                return null;

            // Optional sanity check for the ControllerV2 version response.
            // Raw response is: [0]=reportId, [1]=0x41, [2]=0x10, [6..7]=checksum,
            // [12]=major, [13]=minor. HidLibrary strips [0], so Data[0]=0x41.
            if (response.ReportId != 0x01 || response.Data[0] != 0x41 || response.Data[1] != 0x10)
                return null;

            // HidLibrary strips the report ID:
            // OWC raw respBuf[12]/[13] => HidReport.Data[11]/[12]
            // Bytes are raw hex: 0x01.0x11 = firmware v1.11
            int major = response.Data[11];
            int minor = response.Data[12];

            return (major, minor);
        }
        catch
        {
            return null;
        }
    }

    protected override void Device_Removed()
    {
        isReading = false;

        // Release all back buttons so none remain logically pressed after disconnect.
        lock (updateLock)
        {
            KeyRelease(ButtonFlags.OEM2); // R4
            KeyRelease(ButtonFlags.OEM3); // L4
            KeyRelease(ButtonFlags.OEM4); // Switch
        }

        if (hidDevices.TryGetValue(BackButtonsHidId, out HidDevice? device))
        {
            try { device.Dispose(); } catch { }
        }
    }

    protected override async void Device_Inserted(bool reScan = false)
    {
        if (reScan)
            await WaitUntilReady();

        if (hidDevices.TryGetValue(BackButtonsHidId, out HidDevice? device))
        {
            device.OpenDevice();

            // Silence L4/R4/Gamepad chords only for firmware >= 1.11.
            // Older firmware does not send HID button reports, so chords must remain active.
            if (IsHidSupported)
            {
                // mute L4/R4 chords so they don't clash with HID button reports
                foreach (KeyboardChord chord in OEMChords)
                {
                    if (chord.name is "L4" or "R4" or "Gamepad")
                        chord.silenced = true;
                }
            }

            isReading = true;
            _ = ReadLoopAsync(device);
        }
    }

    private async Task ReadLoopAsync(HidDevice device)
    {
        try
        {
            while (isReading)
            {
                HidReport report = await device.ReadReportAsync().ConfigureAwait(false);
                HandleReport(report);
            }
        }
        catch
        {
            Device_Removed();
        }
    }

    private void HandleReport(HidReport report)
    {
        if (report?.Data is null || report.Data.Length <= 10)
            return;

        lock (updateLock)
        {
            UpdateBackButtonState(ButtonFlags.OEM2, (report.Data[9] & 0x69) != 0);  // R4
            UpdateBackButtonState(ButtonFlags.OEM3, (report.Data[8] & 0x69) != 0);  // L4
            UpdateBackButtonState(ButtonFlags.OEM4, (report.Data[7] & 0x69) != 0);  // Switch
        }
    }

    private void UpdateBackButtonState(ButtonFlags button, bool pressed)
    {
        if (pressed)
            KeyPress(button);
        else
            KeyRelease(button);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        return button switch
        {
            ButtonFlags.OEM1 => "\u221A", // GPD Menu — PromptFont U221A
            ButtonFlags.OEM2 => "\u2277", // R4 — PromptFont U2277
            ButtonFlags.OEM3 => "\u2276", // L4 — PromptFont U2276
            ButtonFlags.OEM4 => "\u243C", // Gamepad / mode switch — PromptFont U243C
            ButtonFlags.OEM5 => "\u243D", // Keyboard — PromptFont U243D
            ButtonFlags.OEM6 => "\u21F9", // Home/Menu — PromptFont U21F9
            _ => base.GetGlyph(button)
        };
    }
}