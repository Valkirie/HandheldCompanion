using HandheldCompanion.Managers;
using hidapi;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using System;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace HandheldCompanion.Controllers;

public class DInputController : IController
{
    public Joystick joystick;
    protected JoystickState State = new();
    protected HidDevice joystickHid;

    public DInputController()
    { }

    public DInputController(PnPDetails details)
    {
        if (details is null)
            throw new Exception("DInputController PnPDetails is null");

        AttachDetails(details);
    }

    ~DInputController()
    {
        Dispose();
    }

    public override void Dispose()
    {
        Unplug();

        joystick.Dispose();
        joystick = null;

        base.Dispose();
    }

    public override void AttachDetails(PnPDetails details)
    {
        // search for the plugged controller
        // todo: check if joystick isn't null and is acquired
        using (DirectInput directInput = new DirectInput())
        {
            foreach (DeviceInstance? deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
            {
                try
                {
                    // Instantiate the joystick
                    Joystick lookup_joystick = new Joystick(directInput, deviceInstance.InstanceGuid);
                    string devicePath = lookup_joystick.Properties.InterfacePath;

                    // Check if lookup joystick has proper interface path
                    string SymLink = DeviceManager.SymLinkToInstanceId(devicePath, DeviceInterfaceIds.HidDevice.ToString());
                    if (SymLink.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                    {
                        joystick = lookup_joystick;
                        joystickHid = new HidDevice(details.VendorID, details.ProductID);
                        break;
                    }
                }
                catch { }
            }
        }

        // unsupported controller
        if (joystick is null)
            throw new Exception($"Couldn't find matching DirectInput controller: VID:{details.GetVendorID()} and PID:{details.GetProductID()}");

        // update UserIndex
        UserIndex = (byte)joystick.Properties.JoystickId;

        base.AttachDetails(details);
    }

    public override string ToString()
    {
        string baseName = base.ToString();
        if (!string.IsNullOrEmpty(baseName))
            return baseName;
        if (!string.IsNullOrEmpty(joystick?.Information.ProductName))
            return joystick.Information.ProductName;
        return $"DInput Controller {UserIndex}";
    }

    public override bool IsConnected()
    {
        if (joystick is null)
            return false;

        if (joystick.IsDisposed)
            return false;

        return true;
    }

    public override void Plug()
    {
        if (!IsConnected())
            return;

        // Acquire joystick
        try
        {
            joystick?.Acquire();
            joystickHid?.OpenDevice();
        }
        catch { }

        base.Plug();
    }

    public override void Gone()
    {
        if (!IsConnected())
            return;

        // Unacquire the joystick
        try
        {
            joystickHid?.EndRead();
        }
        catch { }
    }

    public override void Unplug()
    {
        if (!IsConnected())
            return;

        // Unacquire the joystick
        try
        {
            joystick?.Unacquire();
            joystickHid?.Close();
        }
        catch { }

        base.Unplug();
    }
}