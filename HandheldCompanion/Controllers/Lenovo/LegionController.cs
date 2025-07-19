using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;

namespace HandheldCompanion.Controllers.Lenovo
{
    public class LegionController : XInputController
    {
        // Import the user32.dll library
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        [Flags]
        private enum FrontEnum
        {
            None = 0,
            LegionR = 64,
            LegionL = 128,
        }

        [Flags]
        private enum BackEnum
        {
            None = 0,
            M3 = 4,
            M2 = 8,
            Y3 = 32,
            Y2 = 64,
            Y1 = 128,
        }

        private enum ControllerState
        {
            Unk0 = 0,
            Unk1 = 1,
            Wired = 2,
            Wireless = 3,
        }

        private const byte FRONT_IDX = 17;
        private const byte BACK_IDX = 19;
        private const byte STATUS_IDX = 1;
        private const byte LCONTROLLER_STATE_IDX = 11;
        private const byte RCONTROLLER_STATE_IDX = 12;

        private HashSet<int> READY_STATES = [25, 60];

        private controller_hidapi.net.LegionController Controller;
        private byte[] data = new byte[64];

        #region TouchVariables
        private bool IsPassthrough = false;
        private bool touchpadTouched = false;
        private DateTime touchStartTime;
        private DateTime lastTapTime = DateTime.MinValue;
        private Vector2 lastTapPosition;
        private Vector2 touchStartPosition;
        private const int doubleTapMaxDistance = 100; // Example threshold distance
        private const int doubleTapMaxTime = 300; // Maximum time between taps in milliseconds
        private uint longTapDuration = 500; // Threshold for a long tap in milliseconds
        private const int longTapMaxMovement = 50; // Maximum movement allowed for a long tap
        private bool longTapTriggered = false;
        private bool validTapPosition = false;
        private bool doubleTapPending = false;
        private bool doubleTapped = false;
        private Vector2 lastKnownPosition;
        #endregion

        public override bool IsReady => Controller?.GetStatus(STATUS_IDX) is byte status && READY_STATES.Contains(status);

        public LegionController() : base()
        { }

        public LegionController(PnPDetails details) : base(details)
        {
            // Capabilities
            Capabilities |= ControllerCapabilities.MotionSensor;

            // get long press time from system settings
            SystemParametersInfo(0x006A, 0, ref longTapDuration, 0);

            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        public override string ToString() => "Legion Controller";

        protected override void InitializeInputOutput()
        {
            // Additional controller specific source buttons
            SourceButtons.Add(ButtonFlags.RightPadTouch);
            SourceButtons.Add(ButtonFlags.RightPadClick);
            SourceButtons.Add(ButtonFlags.RightPadClickDown);

            SourceButtons.Add(ButtonFlags.R4);
            SourceButtons.Add(ButtonFlags.R5);
            SourceButtons.Add(ButtonFlags.L4);
            SourceButtons.Add(ButtonFlags.L5);

            SourceButtons.Add(ButtonFlags.B5);
            SourceButtons.Add(ButtonFlags.B6);
            SourceButtons.Add(ButtonFlags.B7);
            SourceButtons.Add(ButtonFlags.B8);

            // Legion Controllers do not have the Special button
            SourceButtons.Remove(ButtonFlags.Special);

            SourceAxis.Add(AxisLayoutFlags.RightPad);
            SourceAxis.Add(AxisLayoutFlags.Gyroscope);
        }

        protected override void UpdateSettings()
        {
            SetPassthrough(ManagerFactory.settingsManager.GetBoolean("LegionControllerPassthrough"));
            SetGyroIndex(ManagerFactory.settingsManager.GetInt("LegionControllerGyroIndex"));
        }

        public override bool IsWireless() =>
            Controller?.GetStatus(LCONTROLLER_STATE_IDX) == (byte)ControllerState.Wireless ||
            Controller?.GetStatus(RCONTROLLER_STATE_IDX) == (byte)ControllerState.Wireless;

        public override bool IsExternal() => false;

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "LegionControllerPassthrough":
                    SetPassthrough(Convert.ToBoolean(value));
                    break;
                case "LegionControllerGyroIndex":
                    SetGyroIndex(Convert.ToInt32(value));
                    break;
            }
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            // (un)plug controller if needed
            bool WasPlugged = Controller?.Reading == true && Controller?.IsDeviceValid == true;
            if (WasPlugged) Close();

            // create controller
            Controller = new(details.VendorID, details.ProductID);

            // open controller as we need to check if it's ready by polling the hiddevice
            Open();

            // manage gamepad motion from right controller
            gamepadMotions[1] = new($"{details.baseContainerDeviceInstanceId}\\{LegionGoTablet.RightJoyconIndex}");
        }

        /*
        public override void Hide(bool powerCycle = true)
        {
            lock (hidLock)
            {
                Close();
                base.Hide(powerCycle);
                Open();
            }
        }

        public override void Unhide(bool powerCycle = true)
        {
            lock (hidLock)
            {
                Close();
                base.Unhide(powerCycle);
                Open();
            }
        }
        */

        private void Open()
        {
            lock (hidLock)
            {
                try
                {
                    if (Controller is not null)
                    {
                        // open controller
                        Controller.OnControllerInputReceived += Controller_OnControllerInputReceived;
                        Controller.Open();
                    }

                    UpdateSettings();
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Couldn't initialize {0}. Exception: {1}", typeof(LegionController), ex.Message);
                    return;
                }
            }
        }

        private void Close()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    // close controller
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.Close();
                }
            }
        }

        public override void Gone()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.EndRead();
                    Controller = null;
                }
            }
        }

        public override void Plug()
        {
            Open();

            base.Plug();
        }

        public override void Unplug()
        {
            SetPassthrough(true);
            Close();

            base.Unplug();
        }

        private void Controller_OnControllerInputReceived(byte[] Data)
        {
            Buffer.BlockCopy(Data, 1, this.data, 0, Data.Length - 1);
        }

        public override void UpdateInputs(long ticks, float delta, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected() || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            base.UpdateInputs(ticks, delta, false);

            FrontEnum frontButton = (FrontEnum)data[FRONT_IDX];
            Inputs.ButtonState[ButtonFlags.OEM1] = frontButton.HasFlag(FrontEnum.LegionR);
            Inputs.ButtonState[ButtonFlags.OEM2] = frontButton.HasFlag(FrontEnum.LegionL);

            BackEnum backButton = (BackEnum)data[BACK_IDX];
            Inputs.ButtonState[ButtonFlags.R4] = backButton.HasFlag(BackEnum.M3);
            Inputs.ButtonState[ButtonFlags.R5] = backButton.HasFlag(BackEnum.Y3);
            Inputs.ButtonState[ButtonFlags.L4] = backButton.HasFlag(BackEnum.Y1);
            Inputs.ButtonState[ButtonFlags.L5] = backButton.HasFlag(BackEnum.Y2);
            Inputs.ButtonState[ButtonFlags.B5] = backButton.HasFlag(BackEnum.M2);
            Inputs.ButtonState[ButtonFlags.B6] = data[20] == 128;   // Scroll click
            Inputs.ButtonState[ButtonFlags.B7] = data[24] == 129;   // Scroll up
            Inputs.ButtonState[ButtonFlags.B8] = data[24] == 255;   // Scroll down

            // handle touchpad if passthrough is off
            if (!IsPassthrough)
            {
                // Right Pad
                ushort TouchpadX = (ushort)(data[25] << 8 | data[26]);
                ushort TouchpadY = (ushort)(data[27] << 8 | data[28]);
                bool touched = TouchpadX != 0 || TouchpadY != 0;

                HandleTouchpadInput(touched, TouchpadX, TouchpadY);
            }

            for (byte idx = 0; idx <= 1; ++idx)
            {
                switch (idx)
                {
                    default:
                    case 0: // LeftJoycon
                        {
                            aX = (short)(data[34] << 8 | data[35]) * -(4.0f / short.MaxValue);
                            aZ = (short)(data[36] << 8 | data[37]) * -(4.0f / short.MaxValue);
                            aY = (short)(data[38] << 8 | data[39]) * -(4.0f / short.MaxValue);

                            gX = (short)(data[40] << 8 | data[41]) * -(2000.0f / short.MaxValue);
                            gZ = (short)(data[42] << 8 | data[43]) * -(2000.0f / short.MaxValue);
                            gY = (short)(data[44] << 8 | data[45]) * -(2000.0f / short.MaxValue);
                        }
                        break;

                    case 1: // RightJoycon
                        {
                            aX = (short)(data[49] << 8 | data[50]) * -(4.0f / short.MaxValue);
                            aZ = (short)(data[47] << 8 | data[48]) * (4.0f / short.MaxValue);
                            aY = (short)(data[51] << 8 | data[52]) * -(4.0f / short.MaxValue);

                            gX = (short)(data[55] << 8 | data[56]) * -(2000.0f / short.MaxValue);
                            gZ = (short)(data[53] << 8 | data[54]) * (2000.0f / short.MaxValue);
                            gY = (short)(data[57] << 8 | data[58]) * -(2000.0f / short.MaxValue);
                        }
                        break;
                }

                // store motion from user selected gyro (left, right)
                if (idx == gamepadIndex)
                {
                    Inputs.GyroState.SetGyroscope(gX, gY, gZ);
                    Inputs.GyroState.SetAccelerometer(aX, aY, aZ);
                }

                // compute motion from controller
                if (gamepadMotions.TryGetValue(idx, out GamepadMotion gamepadMotion))
                    gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, delta);
            }

            base.UpdateInputs(ticks, delta);
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.B5:
                    return "\u2213"; // M2
                case ButtonFlags.B6:
                    return "\u2206"; // Scroll click
                case ButtonFlags.B7:
                    return "\u27F0"; // Scroll up
                case ButtonFlags.B8:
                    return "\u27F1"; // Scroll down
            }

            return base.GetGlyph(button);
        }

        public void HandleTouchpadInput(bool touched, ushort TouchpadX, ushort TouchpadY)
        {
            // Convert the ushort values to Vector2
            Vector2 position = new Vector2(TouchpadX, TouchpadY);

            if (touched)
            {
                lastKnownPosition = position;
            }

            Inputs.ButtonState[ButtonFlags.RightPadTouch] = touched;

            // If the touchpad is touched
            if (touched)
            {
                Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange((short)TouchpadX, 0, 1000, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange((short)-TouchpadY, 0, 1000, short.MinValue, short.MaxValue);

                // If the touchpad was not touched before
                if (!touchpadTouched)
                {
                    touchpadTouched = true;
                    touchStartTime = DateTime.Now;
                    touchStartPosition = position;
                    longTapTriggered = false;
                    validTapPosition = true;

                    // Trigger double tap if pending
                    if (doubleTapPending && (DateTime.Now - lastTapTime).TotalMilliseconds <= doubleTapMaxTime &&
                        Vector2.Distance(lastTapPosition, lastKnownPosition) <= doubleTapMaxDistance)
                    {
                        HandleDoubleTap(lastKnownPosition);
                        doubleTapPending = false;
                        doubleTapped = true;
                        lastTapTime = DateTime.MinValue; // Reset lastTapTime after double tap
                    }
                }
                else if (!longTapTriggered && !doubleTapped && (DateTime.Now - touchStartTime).TotalMilliseconds >= longTapDuration)
                {
                    // Check if the touch moved too much for a long tap
                    if (Vector2.Distance(touchStartPosition, position) <= longTapMaxMovement)
                    {
                        // Trigger long tap while the touch is held
                        HandleLongTap(position);
                        longTapTriggered = true;
                    }
                }
                else if (Vector2.Distance(touchStartPosition, position) > longTapMaxMovement)
                {
                    validTapPosition = false;
                }
            }
            // If the touchpad is not touched
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;
                Inputs.ButtonState[ButtonFlags.RightPadClick] = false;
                Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false;

                // If the touchpad was touched before
                if (touchpadTouched)
                {
                    touchpadTouched = false;
                    doubleTapped = false;
                    DateTime touchEndTime = DateTime.Now;
                    double touchDuration = (touchEndTime - touchStartTime).TotalMilliseconds;

                    // Handle short tap
                    if (touchDuration < longTapDuration && !longTapTriggered && validTapPosition &&
                        Vector2.Distance(touchStartPosition, lastKnownPosition) <= doubleTapMaxDistance)
                    {
                        // Single short tap detected
                        HandleShortTap(lastKnownPosition);
                        lastTapTime = touchEndTime;
                        lastTapPosition = lastKnownPosition;
                        doubleTapPending = true;
                    }
                }
            }
        }

        private void HandleShortTap(Vector2 position)
        {
            // Handle short tap action here
            Inputs.ButtonState[ButtonFlags.RightPadTouch] = true;
            Inputs.ButtonState[ButtonFlags.RightPadClick] = true;
        }

        private void HandleDoubleTap(Vector2 position)
        {
            // Handle double tap action here
            Inputs.ButtonState[ButtonFlags.RightPadTouch] = true;
            Inputs.ButtonState[ButtonFlags.RightPadClick] = true;
        }

        private void HandleLongTap(Vector2 position)
        {
            // Handle long tap action here
            Inputs.ButtonState[ButtonFlags.RightPadTouch] = true;
            Inputs.ButtonState[ButtonFlags.RightPadClick] = true;
            Inputs.ButtonState[ButtonFlags.RightPadClickDown] = true;
        }

        public void SetPassthrough(bool enabled)
        {
            SetTouchPadStatus(enabled ? 1 : 0);
            IsPassthrough = enabled;
        }

        public void SetGyroIndex(int idx)
        {
            gamepadIndex = (byte)idx;
        }
    }
}