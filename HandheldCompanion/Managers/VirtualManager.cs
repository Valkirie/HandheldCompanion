using HandheldCompanion.Controllers;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Nefarius.ViGEm.Client;
using System;
using System.Threading;
using System.Windows;

namespace HandheldCompanion.Managers
{
    public static class VirtualManager
    {
        // controllers vars
        public static ViGEmClient vClient;
        public static ViGEmTarget vTarget;

        private static DSUServer DSUServer;

        // settings vars
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

                MainWindow.SplashScreen.Close();
                MessageBox.Show("Please get it from: https://github.com/ViGEm/ViGEmBus/releases", "ViGEm is missing");
                throw new InvalidOperationException();
            }

            // initialize DSUClient
            DSUServer = new DSUServer();

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            ProfileManager.Applied += ProfileManager_Applied;
        }

        public static void Start()
        {
            // todo: improve me !!
            while (!ControllerManager.IsInitialized)
                Thread.Sleep(250);

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "VirtualManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            Suspend();

            // unsubscrive events
            ProfileManager.Applied -= ProfileManager_Applied;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "VirtualManager");
        }

        public static void Resume()
        {
            // create new ViGEm client
            if (vClient is null)
                vClient = new ViGEmClient();

            // set controller mode
            SetControllerMode(HIDmode);

            SetDSUStatus(SettingsManager.GetBoolean("DSUEnabled"));
        }

        public static void Suspend()
        {
            // dispose virtual controller
            if (vTarget is not null)
            {
                vTarget.Disconnect();
                vTarget.Dispose();
                vTarget = null;
            }

            // dispose ViGEm drivers
            if (vClient is not null)
            {
                vClient.Dispose();
                vClient = null;
            }

            DSUServer.Stop();
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "HIDmode":
                    defaultHIDmode = (HIDmode)Convert.ToInt32(value);
                    SetControllerMode(defaultHIDmode);
                    break;
                case "HIDstatus":
                    SetControllerStatus((HIDstatus)Convert.ToInt32(value));
                    break;
                case "DSUEnabled":
                    SetDSUStatus(Convert.ToBoolean(value));
                    break;
            }
        }

        private static void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            try
            {
                // SetControllerMode takes care of ignoring identical mode switching
                if (HIDmode == profile.HID || profile.HID == HIDmode.NotSelected)
                    return;

                // monitor ControllerManager and check if automatic controller management is running
                // todo: improve me
                if (ControllerManager.ControllerManagerIsBusy)
                    return;

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

        private static void SetDSUStatus(bool started)
        {
            if (started)
                DSUServer.Start();
            else
                DSUServer.Stop();
        }

        public static void SetControllerMode(HIDmode mode)
        {
            // do not disconnect if similar to previous mode and connected
            if (HIDmode == mode)
            {
                if (HIDstatus == HIDstatus.Disconnected)
                    return;
                else if (vTarget is not null && vTarget.IsConnected)
                    return;
            }

            // disconnect current virtual controller
            if (vTarget is not null)
            {
                vTarget.Disconnect();
                vTarget.Dispose();
                vTarget = null;
            }

            switch (mode)
            {
                default:
                case HIDmode.NoController:
                    if (vTarget is not null)
                    {
                        vTarget.Disconnect();
                        vTarget.Dispose();
                        vTarget = null;
                    }
                    break;

                case HIDmode.DualShock4Controller:
                    vTarget = new DualShock4Target();
                    break;

                case HIDmode.Xbox360Controller:
                    // Generate a new random ProductId to help the controller pick empty slot rather than getting its previous one
                    VendorId = (ushort)new Random().Next(ushort.MinValue, ushort.MaxValue);
                    ProductId = (ushort)new Random().Next(ushort.MinValue, ushort.MaxValue);
                    vTarget = new Xbox360Target(VendorId, ProductId);
                    break;
            }

            ControllerSelected?.Invoke(mode);

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

        public static void SetControllerStatus(HIDstatus status)
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
    }
}