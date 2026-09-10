using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using System;
using System.Threading;

namespace HandheldCompanion.Controllers.Lenovo
{
    public class LegionControllerDInput : LegionController
    {
        private Joystick? joystick;
        private controller_hidapi.net.GenericController? dinputController;

        public LegionControllerDInput() : base()
        { }

        public LegionControllerDInput(PnPDetails details) : base(details)
        { }

        public override bool IsConnected() => joystick is not null && !joystick.IsDisposed;

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            using (DirectInput directInput = new())
            {
                foreach (DeviceInstance deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    try
                    {
                        Joystick candidate = new(directInput, deviceInstance.InstanceGuid);
                        string devicePath = candidate.Properties.InterfacePath;
                        string symLink = DeviceManager.SymLinkToInstanceId(devicePath, DeviceInterfaceIds.HidDevice.ToString());

                        if (!symLink.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                        {
                            candidate.Dispose();
                            continue;
                        }

                        joystick = candidate;
                        dinputController = new(details.VendorID, details.ProductID, 64, -1);
                        UserIndex = (byte)joystick.Properties.JoystickId;

                        if (IDevice.GetCurrent() is LegionGoTablet device && device.ControllerMode != LegionGoTablet.GamepadMode.DInput)
                        {
                            // During the transition, ControllerManager keeps the original
                            // Legion controller registered and marks its container as cycling.
                            // Restore XInput only after this DInput endpoint is initialized.
                            foreach (LegionController controller in ControllerManager.GetControllers<LegionController>())
                            {
                                if (ControllerManager.PowerCyclers.TryGetValue(controller.GetContainerInstanceId(), out bool isPowerCycling) && isPowerCycling)
                                {
                                    while (!device.IsReady())
                                        Thread.Sleep(500);

                                    bool success = device.ApplyGamepadMode(LegionGoTablet.GamepadMode.XInput);
                                    break;
                                }
                            }
                        }

                        return;
                    }
                    catch { }
                }
            }

            // unsupported controller
            if (joystick is null)
                LogManager.LogError($"Couldn't find matching DirectInput controller: VID:{details.GetVendorID()} and PID:{details.GetProductID()}");
        }

        public override void Plug()
        {
            try
            {
                joystick?.Acquire();
                dinputController?.Open(false);
            }
            catch { }

            base.Plug();
        }

        public override void Unplug()
        {
            try
            {
                joystick?.Unacquire();
                dinputController?.Close();
            }
            catch { }

            base.Unplug();
        }

        public override void Gone()
        {
            try { dinputController?.EndRead(); } catch { }
            base.Gone();
        }

        protected override bool UpdateState()
        {
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

            if (!IsConnected() || joystick is null)
                return false;

            try
            {
                JoystickState state = joystick.GetCurrentState();

                if (state.RotationX == 32767 && state.RotationY == 32767 && state.RotationZ == 32767)
                    return false;

                Inputs.ButtonState[ButtonFlags.B1] |= state.Buttons[0];
                Inputs.ButtonState[ButtonFlags.B2] |= state.Buttons[1];
                Inputs.ButtonState[ButtonFlags.B3] |= state.Buttons[3];
                Inputs.ButtonState[ButtonFlags.B4] |= state.Buttons[4];
                Inputs.ButtonState[ButtonFlags.L1] |= state.Buttons[6];
                Inputs.ButtonState[ButtonFlags.R1] |= state.Buttons[7];
                Inputs.ButtonState[ButtonFlags.Back] |= state.Buttons[10];
                Inputs.ButtonState[ButtonFlags.Start] |= state.Buttons[11];
                Inputs.ButtonState[ButtonFlags.LeftStickClick] |= state.Buttons[13];
                Inputs.ButtonState[ButtonFlags.RightStickClick] |= state.Buttons[14];

                int pov = state.PointOfViewControllers[0];
                Inputs.ButtonState[ButtonFlags.DPadUp] |= pov is 0 or 4500 or 31500;
                Inputs.ButtonState[ButtonFlags.DPadRight] |= pov is 4500 or 9000 or 13500;
                Inputs.ButtonState[ButtonFlags.DPadDown] |= pov is 13500 or 18000 or 22500;
                Inputs.ButtonState[ButtonFlags.DPadLeft] |= pov is 22500 or 27000 or 31500;

                Inputs.AxisState[AxisFlags.LeftStickX] = (short)InputUtils.MapRange(state.X, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(state.Y, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightStickX] = (short)InputUtils.MapRange(state.Z, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(state.RotationZ, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);

                byte L2 = (byte)InputUtils.MapRange(data[22], byte.MinValue, byte.MaxValue, ushort.MinValue, ushort.MaxValue);
                byte R2 = (byte)InputUtils.MapRange(data[23], byte.MinValue, byte.MaxValue, ushort.MinValue, ushort.MaxValue);
                Inputs.AxisState[AxisFlags.L2] = L2;
                Inputs.AxisState[AxisFlags.R2] = R2;

                Inputs.ButtonState[ButtonFlags.L2Soft] |= L2 > TriggerThreshold;
                Inputs.ButtonState[ButtonFlags.R2Soft] |= R2 > TriggerThreshold;
                Inputs.ButtonState[ButtonFlags.L2Full] |= L2 > TriggerThreshold * 8;
                Inputs.ButtonState[ButtonFlags.R2Full] |= R2 > TriggerThreshold * 8;

                return true;
            }
            catch (SharpDX.SharpDXException ex)
            {
                if (ex.ResultCode == ResultCode.NotAcquired && IsPlugged)
                    Plug();
                else if (ex.ResultCode == ResultCode.InputLost && Details is not null)
                    AttachDetails(Details);

                return false;
            }
        }
    }
}