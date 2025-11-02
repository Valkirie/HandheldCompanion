using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Runtime.InteropServices;

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

        private const byte FRONT_IDX = 18;
        private const byte BACK_IDX = 20;
        private const byte TOUCH_IDX = 26;

        private const byte LCONTROLLER_STATE_IDX = 12;
        private const byte LCONTROLLER_ACCE_IDX = 35;
        private const byte LCONTROLLER_GYRO_IDX = 41;

        private const byte RCONTROLLER_STATE_IDX = 13;
        private const byte RCONTROLLER_ACCE_IDX = 48;
        private const byte RCONTROLLER_GYRO_IDX = 54;

        private controller_hidapi.net.LegionController Controller;
        private byte[] data = new byte[64];

        #region TouchVariables
        private bool ControllerPassthrough = false;
        private bool touchpadTouched = false;
        private DateTime touchStartTime;
        private DateTime lastTapTime = DateTime.MinValue;
        private const int doubleTapMaxDistance = 240;   // Example threshold distance
        private uint doubleTapMaxTime = 500;            // Maximum time between taps in milliseconds
        private uint longTapDuration = 500;             // Threshold for a long tap in milliseconds
        private const int longTapMaxMovement = 50;      // Maximum movement allowed for a long tap
        private bool longTapTriggered = false;
        private bool doubleTapPending = false;
        private bool doubleTapped = false;
        private int touchStartX, touchStartY;
        private int lastTapX, lastTapY;
        private int lastKnownX, lastKnownY;
        #endregion

        public override bool IsReady => IsWireless() || IsWired();

        public LegionController() : base()
        { }

        public LegionController(PnPDetails details) : base(details)
        {
            // Capabilities
            Capabilities |= ControllerCapabilities.MotionSensor;

            // get long press time from system settings
            SystemParametersInfo(0x006A, 0, ref longTapDuration, 0);
            SystemParametersInfo(0x006A, 0, ref doubleTapMaxTime, 0);
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

        public bool IsWired() =>
            Controller?.GetStatus(LCONTROLLER_STATE_IDX) == (byte)ControllerState.Wired ||
            Controller?.GetStatus(RCONTROLLER_STATE_IDX) == (byte)ControllerState.Wired;

        public override bool IsWireless() =>
            Controller?.GetStatus(LCONTROLLER_STATE_IDX) == (byte)ControllerState.Wireless ||
            Controller?.GetStatus(RCONTROLLER_STATE_IDX) == (byte)ControllerState.Wireless;

        public override bool IsExternal() => false;

        protected override void QuerySettings()
        {
            SettingsManager_SettingValueChanged("LegionControllerGyroIndex", ManagerFactory.settingsManager.GetInt("LegionControllerGyroIndex"), false);
            SettingsManager_SettingValueChanged("LegionControllerPassthrough", ManagerFactory.settingsManager.GetBoolean("LegionControllerPassthrough"), false);
            base.QuerySettings();
        }

        protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "LegionControllerGyroIndex":
                    SetGyroIndex(Convert.ToInt32(value));
                    break;
                case "LegionControllerPassthrough":
                    ControllerPassthrough = Convert.ToBoolean(value);
                    break;
            }

            base.SettingsManager_SettingValueChanged(name, value, temporary);
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
            Close();

            base.Unplug();
        }

        private void Controller_OnControllerInputReceived(byte[] Data)
        {
            Buffer.BlockCopy(Data, 0, this.data, 0, Data.Length);
        }

        public override void Tick(long ticks, float delta, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected() || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            base.Tick(ticks, delta, false);

            FrontEnum frontButton = (FrontEnum)data[FRONT_IDX];
            Inputs.ButtonState[ButtonFlags.OEM1] = frontButton.HasFlag(FrontEnum.LegionR);
            Inputs.ButtonState[ButtonFlags.OEM2] = frontButton.HasFlag(FrontEnum.LegionL);

            BackEnum backButton = (BackEnum)data[BACK_IDX];
            Inputs.ButtonState[ButtonFlags.R4] = backButton.HasFlag(BackEnum.M3);
            Inputs.ButtonState[ButtonFlags.R5] = backButton.HasFlag(BackEnum.Y3);
            Inputs.ButtonState[ButtonFlags.L4] = backButton.HasFlag(BackEnum.Y1);
            Inputs.ButtonState[ButtonFlags.L5] = backButton.HasFlag(BackEnum.Y2);
            Inputs.ButtonState[ButtonFlags.B5] = backButton.HasFlag(BackEnum.M2);
            Inputs.ButtonState[ButtonFlags.B6] = data[BACK_IDX] == 128;   // Scroll click
            Inputs.ButtonState[ButtonFlags.B7] = data[BACK_IDX + 4] == 129;   // Scroll up
            Inputs.ButtonState[ButtonFlags.B8] = data[BACK_IDX + 4] == 255;   // Scroll down

            // handle touchpad if passthrough is off
            if (!ControllerPassthrough)
            {
                // Right Pad
                ushort TouchpadX = (ushort)(data[TOUCH_IDX] << 8 | data[TOUCH_IDX + 1]);
                ushort TouchpadY = (ushort)(data[TOUCH_IDX + 2] << 8 | data[TOUCH_IDX + 3]);
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
                            aX = (short)(data[LCONTROLLER_ACCE_IDX] << 8 | data[LCONTROLLER_ACCE_IDX + 1]) * -(4.0f / short.MaxValue);
                            aZ = (short)(data[LCONTROLLER_ACCE_IDX + 2] << 8 | data[LCONTROLLER_ACCE_IDX + 3]) * -(4.0f / short.MaxValue);
                            aY = (short)(data[LCONTROLLER_ACCE_IDX + 4] << 8 | data[LCONTROLLER_ACCE_IDX + 5]) * -(4.0f / short.MaxValue);

                            gX = (short)(data[LCONTROLLER_GYRO_IDX] << 8 | data[LCONTROLLER_GYRO_IDX + 1]) * -(2000.0f / short.MaxValue);
                            // todo: invert gZ on LGO2
                            gZ = (short)(data[LCONTROLLER_GYRO_IDX + 2] << 8 | data[LCONTROLLER_GYRO_IDX + 3]) * -(2000.0f / short.MaxValue);
                            gY = (short)(data[LCONTROLLER_GYRO_IDX + 4] << 8 | data[LCONTROLLER_GYRO_IDX + 5]) * -(2000.0f / short.MaxValue);
                        }
                        break;

                    case 1: // RightJoycon
                        {
                            aX = (short)(data[RCONTROLLER_ACCE_IDX + 2] << 8 | data[RCONTROLLER_ACCE_IDX + 3]) * -(4.0f / short.MaxValue);
                            aZ = (short)(data[RCONTROLLER_ACCE_IDX] << 8 | data[RCONTROLLER_ACCE_IDX + 1]) * (4.0f / short.MaxValue);
                            aY = (short)(data[RCONTROLLER_ACCE_IDX + 4] << 8 | data[RCONTROLLER_ACCE_IDX + 5]) * -(4.0f / short.MaxValue);

                            gX = (short)(data[RCONTROLLER_GYRO_IDX + 2] << 8 | data[RCONTROLLER_GYRO_IDX + 3]) * -(2000.0f / short.MaxValue);
                            gZ = (short)(data[RCONTROLLER_GYRO_IDX] << 8 | data[RCONTROLLER_GYRO_IDX + 1]) * (2000.0f / short.MaxValue);
                            gY = (short)(data[RCONTROLLER_GYRO_IDX + 4] << 8 | data[RCONTROLLER_GYRO_IDX + 5]) * -(2000.0f / short.MaxValue);
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

            base.Tick(ticks, delta, true);
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
            if (ControllerPassthrough)
                return;

            DateTime now = DateTime.UtcNow;
            int x = TouchpadX;
            int y = TouchpadY;

            if (touched)
            {
                lastKnownX = x; lastKnownY = y;
                Inputs.AxisState[AxisFlags.RightPadX] =
                    (short)InputUtils.MapRange((short)x, 0, 1000, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightPadY] =
                    (short)InputUtils.MapRange((short)-y, 0, 1000, short.MinValue, short.MaxValue);
            }
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;
            }

            // One-frame left-click pulse (single tap only)
            bool pulseLeftClick = false;

            // Held left-click via double-tap (latched while finger stays down)
            bool holdLeftClick = doubleTapped && touched;

            // ---------- DOWN edge ----------
            if (touched && !touchpadTouched)
            {
                touchpadTouched = true;
                touchStartTime = now;
                touchStartX = x; touchStartY = y;
                longTapTriggered = false;

                // Second tap?
                bool inTime = (now - lastTapTime).TotalMilliseconds <= (doubleTapMaxTime > 0 ? doubleTapMaxTime : 300);
                int dx = x - lastTapX, dy = y - lastTapY;
                int dist2 = dx * dx + dy * dy;
                int dt = (doubleTapMaxDistance > 0 ? doubleTapMaxDistance : 240);
                int dt2 = dt * dt;

                if (doubleTapPending && inTime && dist2 <= dt2)
                {
                    // Confirm double-tap: start held LEFT (no pulse)
                    doubleTapPending = false;
                    doubleTapped = true;
                    holdLeftClick = true;
                }
                else
                {
                    // Fresh first tap candidate
                    doubleTapPending = false;
                    doubleTapped = false;
                }
            }

            // ---------- HELD (finger down) ----------
            if (touched && touchpadTouched)
            {
                // Long press allowed only if NOT arming/executing double-tap
                bool longPressAllowed = !doubleTapped && !doubleTapPending;

                if (longPressAllowed && !longTapTriggered)
                {
                    double heldMs = (now - touchStartTime).TotalMilliseconds;
                    int mdx = x - touchStartX, mdy = y - touchStartY;
                    int move2 = mdx * mdx + mdy * mdy;
                    int lt = (longTapMaxMovement > 0 ? longTapMaxMovement : 50);
                    int lt2 = lt * lt;

                    if (heldMs >= (longTapDuration > 0 ? longTapDuration : 500) && move2 <= lt2)
                    {
                        longTapTriggered = true;
                        Inputs.ButtonState[ButtonFlags.RightPadClickDown] = true; // right-click hold
                        doubleTapPending = false; // no single tap anymore
                    }
                }

                // If not long-pressing, ensure right-click hold isn't stuck
                if (!longTapTriggered)
                    Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false;
            }

            // ---------- UP edge ----------
            if (!touched && touchpadTouched)
            {
                Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false; // end any hold

                if (doubleTapped)
                {
                    // Finish double-tap contact: stop held left
                    doubleTapped = false;
                    doubleTapPending = false;
                    holdLeftClick = false;
                }
                else if (!longTapTriggered)
                {
                    // Candidate single tap: start double-tap window
                    lastTapTime = now;
                    lastTapX = lastKnownX; lastTapY = lastKnownY;
                    doubleTapPending = true;
                }
                else
                {
                    // Long press ended
                    longTapTriggered = false;
                    doubleTapPending = false;
                }

                touchpadTouched = false;
            }

            // ---------- Deferred single tap (no 2nd tap arrived) ----------
            if (!touched && doubleTapPending)
            {
                if ((now - lastTapTime).TotalMilliseconds > (doubleTapMaxTime > 0 ? doubleTapMaxTime : 300))
                {
                    pulseLeftClick = true;     // single-tap left click
                    doubleTapPending = false;
                }
            }

            // --- Outputs this frame ---
            // Left click: either a held click (double-tap) or a one-frame pulse (single tap)
            Inputs.ButtonState[ButtonFlags.RightPadClick] = holdLeftClick || pulseLeftClick;

            // Right-click (tap-and-hold)
            // (already set/cleared above via RightPadClickDown)

            // Touch flag:
            // - True whenever the pad is touched and NO click this frame,
            // - OR during a double-tap hold (you asked for Touch + Click both true then).
            bool anyClickThisFrame = Inputs.ButtonState[ButtonFlags.RightPadClickDown]
                                   || holdLeftClick
                                   || pulseLeftClick;

            Inputs.ButtonState[ButtonFlags.RightPadTouch] =
                touched && (doubleTapped || !anyClickThisFrame);

            // Safety: avoid stuck holds when idle
            if (!touched && !longTapTriggered)
                Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false;
        }

        public void SetGyroIndex(int idx)
        {
            gamepadIndex = (byte)idx;
        }
    }
}