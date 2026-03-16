using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using vJoyInterfaceWrap;

namespace HandheldCompanion.Targets
{
    public class VJoyTarget : ViGEmTarget
    {
        private readonly vJoy joystick;
        private vJoy.JoystickState state = new vJoy.JoystickState();

        private readonly uint deviceId;

        private long minAxisVal = ushort.MinValue;
        private long maxAxisVal = short.MaxValue;
        private const uint PovNeutral = 0xFFFFFFFF;

        // Force-feedback state — written by the native FFB callback, read by EFF_START
        private vJoy.WrapFfbCbFunc? _ffbCallback;
        private volatile byte _largeMotor;
        private volatile byte _smallMotor;

        // VID/PID are fixed by the vJoy driver (vjoy.sys) and cannot be changed at runtime.
        // They are exposed here so Steam/SDL can be taught to recognise the device via WriteSDLGameControllerMapping().
        public const ushort VendorId = 4660;
        public const ushort ProductId = 48813;
        private const ushort DriverRevision = 0x0000;

        public VJoyTarget(uint _deviceId = 1) : base()
        {
            deviceId = _deviceId;
            HID = HIDmode.DInputController;
            joystick = new vJoy();

            LogManager.LogInformation("{0} initialized (device {1})", ToString(), deviceId);
        }

        public override string ToString()
        {
            return EnumUtils.GetDescriptionFromEnumValue(HID);
        }

        public override bool Connect()
        {
            if (IsConnected)
                return true;

            try
            {
                if (!joystick.vJoyEnabled())
                {
                    LogManager.LogWarning("vJoy driver is not enabled");
                    return false;
                }

                VjdStat status = joystick.GetVJDStatus(deviceId);

                // Another feeder owns it — nothing we can do
                if (status == VjdStat.VJD_STAT_BUSY)
                {
                    LogManager.LogWarning("vJoy device {0} is owned by another feeder", deviceId);
                    return false;
                }

                // Relinquish before reconfiguring so the driver lets us modify the device
                if (status == VjdStat.VJD_STAT_OWN)
                    joystick.RelinquishVJD(deviceId);

                // Always reconfigure: force=true when device already exists so axes/buttons/POV are correct;
                // force=false when device is missing (just creates it fresh).
                bool force = status != VjdStat.VJD_STAT_MISS;
                if (!MountDevice(deviceId, force))
                {
                    LogManager.LogWarning("Failed to configure vJoy device {0}", deviceId);
                    return false;
                }

                // Re-check status after (re)configuration
                status = joystick.GetVJDStatus(deviceId);

                if (status != VjdStat.VJD_STAT_FREE)
                {
                    LogManager.LogWarning("vJoy device {0} not available after configuration (status: {1})", deviceId, status);
                    return false;
                }

                if (!joystick.AcquireVJD(deviceId))
                {
                    LogManager.LogWarning("Failed to acquire vJoy device {0}", deviceId);
                    return false;
                }

                joystick.GetVJDAxisMin(deviceId, HID_USAGES.HID_USAGE_X, ref minAxisVal);
                joystick.GetVJDAxisMax(deviceId, HID_USAGES.HID_USAGE_X, ref maxAxisVal);

                joystick.ResetVJD(deviceId);

                // Start force feedback
                _largeMotor = 0;
                _smallMotor = 0;
                _ffbCallback = OnFfbPacket;
                joystick.FfbRegisterGenCB(_ffbCallback, IntPtr.Zero);

                // Test if DLL matches the driver
                UInt32 DllVer = 0, DrvVer = 0;
                bool match = joystick.DriverMatch(ref DllVer, ref DrvVer);
                if (!match)
                    LogManager.LogWarning("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})", DrvVer, DllVer);

                WriteSDLGameControllerMapping();

                IsConnected = true;
                RaiseConnected();
                LogManager.LogInformation("{0} connected (device {1})", ToString(), deviceId);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to connect vJoy device {0}: {1}", deviceId, ex.Message);
                return false;
            }
        }

        public override bool Disconnect()
        {
            if (!IsConnected)
                return false;

            try
            {
                _ffbCallback = null;
                joystick.ResetVJD(deviceId);
                joystick.RelinquishVJD(deviceId);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to disconnect vJoy device {0}: {1}", deviceId, ex.Message);
                return false;
            }

            DismountDevice(deviceId);

            IsConnected = false;
            RaiseDisconnected();
            LogManager.LogInformation("{0} disconnected (device {1})", ToString(), deviceId);
            return true;
        }

        public override void UpdateInputs(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            if (!IsConnected)
                return;

            state.bDevice = (byte)deviceId;

            // Analog sticks: short (-32768..32767) → int (0..maxAxisVal)
            // DInput Y convention is inverted vs XInput: positive = down
            state.AxisX = MapStickAxis(inputs.AxisState[AxisFlags.LeftStickX]);
            state.AxisY = MapStickAxis((short)-inputs.AxisState[AxisFlags.LeftStickY]);
            state.AxisXRot = MapStickAxis(inputs.AxisState[AxisFlags.RightStickX]);
            state.AxisYRot = MapStickAxis((short)-inputs.AxisState[AxisFlags.RightStickY]);

            // Triggers: short (0..255) → int (1..maxAxisVal)
            state.AxisZ = MapTrigger(inputs.AxisState[AxisFlags.L2]);
            state.AxisZRot = MapTrigger(inputs.AxisState[AxisFlags.R2]);

            // Buttons: vJoy buttons are 1-based; Buttons covers 1-32, ButtonsEx1 covers 33-37
            uint buttons = 0;
            if (inputs.ButtonState[ButtonFlags.B1]) buttons |= 1u << 0;   // btn 1
            if (inputs.ButtonState[ButtonFlags.B2]) buttons |= 1u << 1;   // btn 2
            if (inputs.ButtonState[ButtonFlags.B3]) buttons |= 1u << 2;   // btn 3
            if (inputs.ButtonState[ButtonFlags.B4]) buttons |= 1u << 3;   // btn 4
            if (inputs.ButtonState[ButtonFlags.L1]) buttons |= 1u << 4;   // btn 5
            if (inputs.ButtonState[ButtonFlags.R1]) buttons |= 1u << 5;   // btn 6
            if (inputs.ButtonState[ButtonFlags.Back]) buttons |= 1u << 6;   // btn 7
            if (inputs.ButtonState[ButtonFlags.Start]) buttons |= 1u << 7;   // btn 8
            if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons |= 1u << 8;   // btn 9
            if (inputs.ButtonState[ButtonFlags.RightStickClick]) buttons |= 1u << 9;   // btn 10
            if (inputs.ButtonState[ButtonFlags.Special]) buttons |= 1u << 10;  // btn 11
            if (inputs.ButtonState[ButtonFlags.B5]) buttons |= 1u << 11;  // btn 12
            if (inputs.ButtonState[ButtonFlags.B6]) buttons |= 1u << 12;  // btn 13
            if (inputs.ButtonState[ButtonFlags.B7]) buttons |= 1u << 13;  // btn 14
            if (inputs.ButtonState[ButtonFlags.B8]) buttons |= 1u << 14;  // btn 15
            if (inputs.ButtonState[ButtonFlags.B9]) buttons |= 1u << 15;  // btn 16
            if (inputs.ButtonState[ButtonFlags.B10]) buttons |= 1u << 16;  // btn 17
            if (inputs.ButtonState[ButtonFlags.B11]) buttons |= 1u << 17;  // btn 18
            if (inputs.ButtonState[ButtonFlags.B12]) buttons |= 1u << 18;  // btn 19
            if (inputs.ButtonState[ButtonFlags.B13]) buttons |= 1u << 19;  // btn 20
            if (inputs.ButtonState[ButtonFlags.B14]) buttons |= 1u << 20;  // btn 21
            if (inputs.ButtonState[ButtonFlags.B15]) buttons |= 1u << 21;  // btn 22
            if (inputs.ButtonState[ButtonFlags.L4]) buttons |= 1u << 22;  // btn 23
            if (inputs.ButtonState[ButtonFlags.R4]) buttons |= 1u << 23;  // btn 24
            if (inputs.ButtonState[ButtonFlags.L5]) buttons |= 1u << 24;  // btn 25
            if (inputs.ButtonState[ButtonFlags.R5]) buttons |= 1u << 25;  // btn 26
            if (inputs.ButtonState[ButtonFlags.Special2]) buttons |= 1u << 26;  // btn 27
            state.Buttons = buttons;

            // Buttons 33-37 via ButtonsEx1
            uint buttonsEx1 = 0;
            if (inputs.ButtonState[ButtonFlags.OEM1]) buttonsEx1 |= 1u << 0;  // btn 28
            if (inputs.ButtonState[ButtonFlags.OEM2]) buttonsEx1 |= 1u << 1;  // btn 29
            if (inputs.ButtonState[ButtonFlags.OEM3]) buttonsEx1 |= 1u << 2;  // btn 30
            if (inputs.ButtonState[ButtonFlags.OEM4]) buttonsEx1 |= 1u << 3;  // btn 31
            if (inputs.ButtonState[ButtonFlags.OEM5]) buttonsEx1 |= 1u << 4;  // btn 32
            if (inputs.ButtonState[ButtonFlags.OEM6]) buttonsEx1 |= 1u << 5; // btn 33
            if (inputs.ButtonState[ButtonFlags.OEM7]) buttonsEx1 |= 1u << 6; // btn 34
            if (inputs.ButtonState[ButtonFlags.OEM8]) buttonsEx1 |= 1u << 7; // btn 35
            if (inputs.ButtonState[ButtonFlags.OEM9]) buttonsEx1 |= 1u << 8; // btn 36
            if (inputs.ButtonState[ButtonFlags.OEM10]) buttonsEx1 |= 1u << 9; // btn 37
            state.ButtonsEx1 = buttonsEx1;

            // D-Pad as continuous POV hat (hundredths of degrees: 0=N, 9000=E, 18000=S, 27000=W)
            bool dUp = inputs.ButtonState[ButtonFlags.DPadUp];
            bool dDown = inputs.ButtonState[ButtonFlags.DPadDown];
            bool dLeft = inputs.ButtonState[ButtonFlags.DPadLeft];
            bool dRight = inputs.ButtonState[ButtonFlags.DPadRight];

            uint pov;
            if (dUp && dRight) pov = 4500;
            else if (dRight && dDown) pov = 13500;
            else if (dDown && dLeft) pov = 22500;
            else if (dLeft && dUp) pov = 31500;
            else if (dUp) pov = 0;
            else if (dRight) pov = 9000;
            else if (dDown) pov = 18000;
            else if (dLeft) pov = 27000;
            else pov = PovNeutral;

            state.bHats = pov;

            try
            {
                if (!joystick.UpdateVJD(deviceId, ref state))
                {
                    // LogManager.LogWarning("vJoy UpdateVJD lost device {0}, re-acquiring", deviceId);
                    // joystick.AcquireVJD(deviceId);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("vJoy UpdateVJD failed: {0}", ex.Message);
            }
        }

        private int MapStickAxis(short value)
        {
            return (int)MathF.Round(InputUtils.rangeMap(value, short.MinValue, short.MaxValue, 0f, maxAxisVal));
        }

        private int MapTrigger(int value)
        {
            return (int)MathF.Round(InputUtils.rangeMap(value, byte.MinValue, byte.MaxValue, 0f, maxAxisVal));
        }

        // FFB callback — invoked on a native vJoy thread for every incoming HID force-feedback packet.
        // Constant force (PT_CONSTREP) maps to the large (low-frequency) motor.
        // Periodic effects (PT_PRIDREP) map to the small (high-frequency) motor.
        // EFF_START/EFF_SOLO fires SendVibrate; EFF_STOP/device-reset silences both motors.
        private void OnFfbPacket(IntPtr data, IntPtr userData)
        {
            var packetType = default(FFBPType);
            if (joystick.Ffb_h_Type(data, ref packetType) != 0)
                return;

            switch (packetType)
            {
                case FFBPType.PT_CONSTREP:
                    {
                        var fx = default(vJoy.FFB_EFF_CONSTANT);
                        if (joystick.Ffb_h_Eff_Constant(data, ref fx) == 0)
                            _largeMotor = (byte)(Math.Abs((int)fx.Magnitude) * byte.MaxValue / 10000);
                        break;
                    }
                case FFBPType.PT_PRIDREP:
                    {
                        var fx = default(vJoy.FFB_EFF_PERIOD);
                        if (joystick.Ffb_h_Eff_Period(data, ref fx) == 0)
                            _smallMotor = (byte)(fx.Magnitude * byte.MaxValue / 10000);
                        break;
                    }
                case FFBPType.PT_EFOPREP:
                    {
                        var fx = default(vJoy.FFB_EFF_OP);
                        if (joystick.Ffb_h_EffOp(data, ref fx) == 0)
                        {
                            switch (fx.EffectOp)
                            {
                                case FFBOP.EFF_START:
                                case FFBOP.EFF_SOLO:
                                    SendVibrate(_largeMotor, _smallMotor);
                                    break;
                                case FFBOP.EFF_STOP:
                                    SendVibrate(0, 0);
                                    break;
                            }
                        }
                        break;
                    }
                case FFBPType.PT_CTRLREP:
                    {
                        var ctrl = default(FFB_CTRL);
                        if (joystick.Ffb_h_DevCtrl(data, ref ctrl) == 0 &&
                            (ctrl == FFB_CTRL.CTRL_STOPALL || ctrl == FFB_CTRL.CTRL_DEVRST))
                        {
                            _largeMotor = 0;
                            _smallMotor = 0;
                            SendVibrate(0, 0);
                        }
                        break;
                    }
            }
        }

        public static uint FindAvailableDeviceId()
        {
            var joystick = new vJoy();
            for (uint id = 1; id <= 16; id++)
            {
                VjdStat status = joystick.GetVJDStatus(id);
                if (status != VjdStat.VJD_STAT_BUSY && status != VjdStat.VJD_STAT_UNKN)
                    return id;
            }

            LogManager.LogWarning("No free vJoy device slot found (all 16 busy); defaulting to device 1");
            return 1;
        }

        // Returns the Steam install directory
        private static string FindSteamPath()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                return key?.GetValue("SteamPath") as string ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        // Builds the 32-char hex SDL2 GUID for a Windows USB/HID device.
        // Layout (8 × LE uint16): bus=0x0003 | 0 | vendor | 0 | product | 0 | version | 0
        private static string BuildSDLGuid(ushort vendorId, ushort productId, ushort version)
        {
            var b = new byte[16];
            b[0] = 0x03;                          // bus low  (USB HID = 0x0003)
            b[4] = (byte)(vendorId & 0xFF);
            b[5] = (byte)(vendorId >> 8);
            b[8] = (byte)(productId & 0xFF);
            b[9] = (byte)(productId >> 8);
            b[12] = (byte)(version & 0xFF);
            b[13] = (byte)(version >> 8);
            return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
        }

        // Writes an SDL2 gamecontrollerdb entry for the vJoy device into Steam's config directory
        // so that Steam and SDL2-based games recognise and correctly map the controller.
        public static void WriteSDLGameControllerMapping()
        {
            string steamPath = FindSteamPath();
            if (string.IsNullOrEmpty(steamPath))
            {
                LogManager.LogWarning("Steam not found; skipping SDL game controller mapping for vJoy");
                return;
            }

            // 0300000034120000adbe000000000000,vJoy Device,a:b0,b:b1,back:b15,dpdown:b6,dpleft:b7,dpright:b8,dpup:b5,guide:b16,leftshoulder:b9,leftstick:b13,lefttrigger:b11,leftx:a0,lefty:a1,rightshoulder:b10,rightstick:b14,righttrigger:b12,rightx:a3,righty:a4,start:b4,x:b2,y:b3,platform:Windows,
            string dbPath = Path.Combine(steamPath, "config", "gamecontrollerdb.txt");
            string guid = BuildSDLGuid(VendorId, ProductId, DriverRevision);

            // Button/axis layout mirrors UpdateInputs (SDL indices are 0-based):
            //   Standard face/shoulder/sys (b0-b10):
            //     a=B1, b=B2, x=B3, y=B4, leftshoulder=L1, rightshoulder=R1,
            //     back=Back, start=Start, leftstick=LS, rightstick=RS, guide=Special
            //   Extended — SDL3 named slots (b11-b26, non-contiguous):
            //     misc1=Special2(b26), misc2-6=B5-B9(b11-b15), touchpad=B10(b16),
            //     paddle1=L4(b22), paddle2=R4(b23), paddle3=L5(b24), paddle4=R5(b25)
            //   Axes: leftx=a0, lefty=a1, lefttrigger=a2, rightx=a3, righty=a4, righttrigger=a5
            //   D-pad: continuous POV hat h0
            //   Unmapped (no remaining SDL named slots): B11-B15(b17-21), OEM1-OEM10(b27-36)
            string mapping =
                $"{guid},HC Virtual Joystick," +
                "a:b0,b:b1,x:b2,y:b3," +
                "leftshoulder:b4,rightshoulder:b5," +
                "back:b6,start:b7,leftstick:b8,rightstick:b9,guide:b10," +
                "misc1:b26,misc2:b11,misc3:b12,misc4:b13,misc5:b14,misc6:b15," +
                "touchpad:b16," +
                "paddle1:b22,paddle2:b23,paddle3:b24,paddle4:b25," +
                "leftx:a0,lefty:a1,lefttrigger:a2,rightx:a3,righty:a4,righttrigger:a5," +
                "dpup:h0.1,dpright:h0.2,dpdown:h0.4,dpleft:h0.8,platform:Windows,";

            try
            {
                var lines = new List<string>(File.Exists(dbPath) ? File.ReadAllLines(dbPath) : []);
                lines.RemoveAll(l => l.StartsWith(guid, StringComparison.OrdinalIgnoreCase));
                lines.Add(mapping);
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                File.WriteAllLines(dbPath, lines);
                LogManager.LogInformation("SDL game controller mapping written for vJoy ({0})", guid);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to write SDL game controller mapping for vJoy: {0}", ex.Message);
            }
        }

        // Returns true when the vJoy driver is installed on this system.
        public static bool IsInstalled() => !string.IsNullOrEmpty(FindVJoyConfig());

        // Searches standard registry uninstall entries for vJoyConfig.exe
        private static string FindVJoyConfig()
        {
            const string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey? uninstall = hklm.OpenSubKey(uninstallPath))
            {
                if (uninstall is null)
                    return string.Empty;

                foreach (string name in uninstall.GetSubKeyNames())
                {
                    using (RegistryKey? sub = uninstall.OpenSubKey(name))
                    {
                        if (sub is null)
                            return string.Empty;

                        string dll64 = sub?.GetValue("DllX64Location") as string ?? string.Empty;
                        if (string.IsNullOrEmpty(dll64))
                            continue;

                        string path = Path.Combine(dll64, "vJoyConfig.exe");
                        if (File.Exists(path))
                            return path;
                    }
                }
            }
            return string.Empty;
        }

        // Invokes vJoyConfig.exe to create or reconfigure the device.
        // Requires the process to run elevated (inherits HC's elevation, or UAC-prompts).
        private static bool MountDevice(uint deviceId, bool force)
        {
            string configExe = FindVJoyConfig();
            if (configExe == null)
            {
                LogManager.LogWarning("vJoyConfig.exe not found; install vJoy then configure device {0} manually", deviceId);
                return false;
            }

            // 6 axes, 32 buttons, 1 continuous POV hat, all FFB effects
            string forceFlag = force ? "-f " : string.Empty;
            string args = $"{deviceId} {forceFlag}-a X Y Z Rx Ry Rz -b 32 -p 1 -e all";

            try
            {
                if (File.Exists(configExe))
                {
                    Process? process = Process.Start(new ProcessStartInfo(configExe, args)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    });

                    if (process is null)
                        return false;

                    if (process.WaitForExit(5000))
                    {
                        LogManager.LogWarning("vJoyConfig exited with code {0} for device {1}", process?.ExitCode ?? 0, deviceId);
                        return true;
                    }

                    // hacky: even if it doesn't exit in time, assume it worked since the device will be present on next connect attempt
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("ConfigureDevice failed for device {0}: {1}", deviceId, ex.Message);
                return false;
            }

            return false;
        }

        // Invokes vJoyConfig.exe -d to completely remove the device slot from the system.
        private static bool DismountDevice(uint deviceId)
        {
            string configExe = FindVJoyConfig();
            if (string.IsNullOrEmpty(configExe))
            {
                LogManager.LogWarning("vJoyConfig.exe not found; cannot dismount device {0}", deviceId);
                return false;
            }

            string args = $"-d {deviceId}";

            try
            {
                if (File.Exists(configExe))
                {
                    Process? process = Process.Start(new ProcessStartInfo(configExe, args)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    });

                    if (process is null)
                        return false;

                    if (process.WaitForExit(5000))
                    {
                        LogManager.LogWarning("vJoy device {0} dismounted", deviceId);
                        return true;
                    }

                    // hacky: even if it doesn't exit in time, assume it worked since the device will be gone on next connect attempt
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("DismountDevice failed for device {0}: {1}", deviceId, ex.Message);
                return false;
            }

            return false;
        }

        public override void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
