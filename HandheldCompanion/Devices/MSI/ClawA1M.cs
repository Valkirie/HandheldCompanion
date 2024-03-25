using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Management;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class ClawA1M : IDevice
{
    private enum WMIEventCode
    {
        LaunchMcxMainUI = 41, // 0x00000029
        LaunchMcxOSD = 88, // 0x00000058
    }

    private readonly Dictionary<WMIEventCode, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { WMIEventCode.LaunchMcxMainUI, ButtonFlags.OEM1 },
        { WMIEventCode.LaunchMcxOSD, ButtonFlags.OEM2 },
    };

    private ManagementEventWatcher specialKeyWatcher;
    private ManagementScope scope;

    public ClawA1M()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw";

        // https://www.intel.com/content/www/us/en/products/sku/236847/intel-core-ultra-7-processor-155h-24m-cache-up-to-4-80-ghz/specifications.html
        nTDP = new double[] { 28, 28, 65 };
        cTDP = new double[] { 20, 65 };
        GfxClock = new double[] { 100, 2250 };
        CpuClock = 4800;

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.None;

        OEMChords.Add(new DeviceChord("CLAW",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("QS",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("M1",             // unimplemented
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("M2",             // unimplemented
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM4
        ));

        // start thread to monitor WMI events
        new Thread(() =>
        {
            try
            {
                scope = new ManagementScope("\\\\.\\root\\WMI");
                scope.Connect();
                if (!scope.IsConnected)
                    return;
                specialKeyWatcher = new ManagementEventWatcher(scope, (EventQuery)(new WqlEventQuery("SELECT * FROM MSI_Event")));
                specialKeyWatcher.EventArrived += onWMIEvent;
            }
            catch (Exception ex)
            {
                LogManager.LogInformation("Exception configuring MSI_Event monitor: {0}", ex.Message);
            }
        }).Start();
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // start WMI event monitor
        specialKeyWatcher.Start();

        return true;
    }

    public override void Close()
    {
        // stop WMI event monitor
        specialKeyWatcher.Stop();

        base.Close();
    }

    public override void SetKeyPressDelay(HIDmode controllerMode)
    {
        switch (controllerMode)
        {
            case HIDmode.DualShock4Controller:
                KeyPressDelay = 180;
                break;
            default:
                KeyPressDelay = 20;
                break;
        }
    }

    private void onWMIEvent(object sender, EventArrivedEventArgs e)
    {
        int WMIEvent = Convert.ToInt32(e.NewEvent.Properties["MSIEvt"].Value);
        WMIEventCode key = (WMIEventCode)(WMIEvent & (int)byte.MaxValue);

        // LogManager.LogInformation("Received MSI WMI Event Code {0}", (int)key);

        if (!keyMapping.ContainsKey(key))
            return;

        // get button
        ButtonFlags button = keyMapping[key];
        switch (key)
        {
            default:
            case WMIEventCode.LaunchMcxMainUI:  // MSI Claw: Click
            case WMIEventCode.LaunchMcxOSD:     // Quick Settings: Click
                {
                    Task.Factory.StartNew(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(KeyPressDelay);
                        KeyRelease(button);
                    });
                }
                break;
        }
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE010";
            case ButtonFlags.OEM2:
                return "\uE011";
            case ButtonFlags.OEM3:
                return "\u2212";
            case ButtonFlags.OEM4:
                return "\u2213";
        }

        return defaultGlyph;
    }
}