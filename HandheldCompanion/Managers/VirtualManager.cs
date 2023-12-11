using HandheldCompanion.Controllers;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using System;
<<<<<<< HEAD
=======
using System.Threading;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

namespace HandheldCompanion.Managers
{
    public static class VirtualManager
    {
        // controllers vars
        public static ViGEmClient vClient;
        public static ViGEmTarget vTarget;

        private static DSUServer DSUServer;

        // settings vars
<<<<<<< HEAD
        private static HIDmode HIDmode = HIDmode.NoController;
        private static HIDstatus HIDstatus = HIDstatus.Disconnected;

        public static bool IsInitialized;

        public static event ControllerSelectedEventHandler ControllerSelected;
        public delegate void ControllerSelectedEventHandler(IController Controller);
=======
        public static HIDmode HIDmode = HIDmode.NoController;
        private static HIDmode defaultHIDmode = HIDmode.NoController;
        public static HIDstatus HIDstatus = HIDstatus.Disconnected;

        public static ushort ProductId = 0x28E; // Xbox 360
        public static ushort VendorId = 0x45E;  // Microsoft

        public static ushort FakeVendorId = 0x76B;  // HC

        public static bool IsInitialized;

        public static event HIDChangedEventHandler HIDchanged;
        public delegate void HIDChangedEventHandler(HIDmode HIDmode);


        public static event ControllerSelectedEventHandler ControllerSelected;
        public delegate void ControllerSelectedEventHandler(HIDmode mode);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public static event VibrateEventHandler Vibrated;
        public delegate void VibrateEventHandler(byte LargeMotor, byte SmallMotor);

        static VirtualManager()
        {
            // verifying ViGEm is installed
            try
            {
                vClient = new ViGEmClient();
            }
            catch (Exception)
            {
                LogManager.LogCritical("ViGEm is missing. Please get it from: {0}", "https://github.com/ViGEm/ViGEmBus/releases");
                throw new InvalidOperationException();
            }

            // initialize DSUClient
            DSUServer = new DSUServer();

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            SettingsManager.Initialized += SettingsManager_Initialized;
<<<<<<< HEAD
=======
            ProfileManager.Applied += ProfileManager_Applied;
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        }

        public static void Start()
        {
<<<<<<< HEAD
=======
            // todo: improve me !!
            while (!ControllerManager.IsInitialized)
                Thread.Sleep(250);

>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "VirtualManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            ResetViGEm();
            DSUServer.Stop();

<<<<<<< HEAD
=======
            // unsubscrive events
            ProfileManager.Applied -= ProfileManager_Applied;

>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "VirtualManager");
        }

        public static void Resume()
        {
<<<<<<< HEAD
            // reset vigem
            ResetViGEm();

            // create new ViGEm client
            vClient = new ViGEmClient();
=======
            // create new ViGEm client
            if (vClient is null)
                vClient = new ViGEmClient();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

            // set controller mode
            SetControllerMode(HIDmode);
        }

<<<<<<< HEAD
        public static void Pause()
        {
=======
        public static void Suspend()
        {
            // reset vigem
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
            ResetViGEm();
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "HIDmode":
<<<<<<< HEAD
                    SetControllerMode((HIDmode)Convert.ToInt32(value));
=======
                    defaultHIDmode = (HIDmode)Convert.ToInt32(value);
                    SetControllerMode(defaultHIDmode);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                    break;
                case "HIDstatus":
                    SetControllerStatus((HIDstatus)Convert.ToInt32(value));
                    break;
                case "DSUEnabled":
                    if (SettingsManager.IsInitialized)
                        SetDSUStatus(Convert.ToBoolean(value));
                    break;
                case "DSUport":
                    DSUServer.port = Convert.ToInt32(value);
                    if (SettingsManager.IsInitialized)
                        SetDSUStatus(SettingsManager.GetBoolean("DSUEnabled"));
                    break;
            }
        }

<<<<<<< HEAD
=======
        private static void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            try
            {
                // SetControllerMode takes care of ignoring identical mode switching
                if (HIDmode == profile.HID)
                    return;

                // todo: monitor ControllerManager and check if automatic controller management is running
                
                switch (profile.HID)
                {
                    case HIDmode.Xbox360Controller:
                    case HIDmode.DualShock4Controller:
                        {
                            SetControllerMode(profile.HID);
                            break;
                        }

                    default: // Default or not assigned
                        {
                            SetControllerMode(defaultHIDmode);
                            break;
                        }
                }
            }
            catch // TODO requires further testing
            {
                LogManager.LogError("Couldnt set per-profile HIDmode: {0}", profile.HID);
            }
        }


>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        private static void SettingsManager_Initialized()
        {
            SetDSUStatus(SettingsManager.GetBoolean("DSUEnabled"));
        }

        private static void SetDSUStatus(bool started)
        {
            if (started)
                DSUServer.Start();
            else
                DSUServer.Stop();
        }

<<<<<<< HEAD
        private static void SetControllerMode(HIDmode mode)
=======
        public static void SetControllerMode(HIDmode mode)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        {
            // do not disconnect if similar to previous mode
            if (HIDmode == mode && vTarget is not null)
                return;

            // disconnect current virtual controller
            if (vTarget is not null)
<<<<<<< HEAD
                vTarget.Disconnect();
=======
            {
                vTarget.Disconnect();
                vTarget.Dispose();
                vTarget = null;
            }
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

            switch (mode)
            {
                default:
                case HIDmode.NoController:
                    if (vTarget is not null)
                    {
<<<<<<< HEAD
=======
                        vTarget.Disconnect();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                        vTarget.Dispose();
                        vTarget = null;
                    }
                    break;
                case HIDmode.DualShock4Controller:
                    vTarget = new DualShock4Target();
                    break;
                case HIDmode.Xbox360Controller:
<<<<<<< HEAD
                    vTarget = new Xbox360Target();
                    break;
            }

            ControllerSelected?.Invoke(ControllerManager.GetEmulatedController());
=======
                    // Generate a new random ProductId to help the controller pick empty slot rather than getting its previous one
                    VendorId = (ushort)new Random().Next(ushort.MinValue, ushort.MaxValue);
                    ProductId = (ushort)new Random().Next(ushort.MinValue, ushort.MaxValue);
                    vTarget = new Xbox360Target(VendorId, ProductId);
                    break;
            }

            ControllerSelected?.Invoke(mode);
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc

            // failed to initialize controller
            if (vTarget is null)
            {
                if (mode != HIDmode.NoController)
                    LogManager.LogError("Failed to initialise virtual controller with HIDmode: {0}", mode);
                return;
            }

            vTarget.Connected += OnTargetConnected;
            vTarget.Disconnected += OnTargetDisconnected;
            vTarget.Vibrated += OnTargetVibrated;

            // update status
            SetControllerStatus(HIDstatus);

            // update current HIDmode
            HIDmode = mode;
        }

<<<<<<< HEAD
        private static void SetControllerStatus(HIDstatus status)
=======
        public static void SetControllerStatus(HIDstatus status)
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
        {
            if (vTarget is null)
                return;

            switch (status)
            {
                default:
                case HIDstatus.Connected:
                    vTarget.Connect();
                    break;
                case HIDstatus.Disconnected:
                    vTarget.Disconnect();
                    break;
            }

            // update current HIDstatus
            HIDstatus = status;
        }

        private static void OnTargetConnected(ViGEmTarget target)
        {
            ToastManager.SendToast($"{target}", "is now connected", $"HIDmode{(uint)target.HID}");
        }

        private static void OnTargetDisconnected(ViGEmTarget target)
        {
            ToastManager.SendToast($"{target}", "is now disconnected", $"HIDmode{(uint)target.HID}");
        }

        private static void OnTargetVibrated(byte LargeMotor, byte SmallMotor)
        {
            Vibrated?.Invoke(LargeMotor, SmallMotor);
        }

        public static void UpdateInputs(ControllerState controllerState)
        {
            // DS4Touch is used by both targets below, update first
            DS4Touch.UpdateInputs(controllerState);

            vTarget?.UpdateInputs(controllerState);
            DSUServer?.UpdateInputs(controllerState);
        }

        private static void ResetViGEm()
        {
            // dispose virtual controller
            if (vTarget is not null)
            {
<<<<<<< HEAD
=======
                vTarget.Disconnect();
>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
                vTarget.Dispose();
                vTarget = null;
            }

            // dispose ViGEm drivers
            if (vClient is not null)
            {
                vClient.Dispose();
                vClient = null;
            }
        }
    }
}