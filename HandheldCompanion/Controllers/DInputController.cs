using controller_hidapi.net;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using System;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace HandheldCompanion.Controllers;

public class DInputController : IController
{
    public Joystick? joystick;
    protected JoystickState State = new();
    protected GenericController? controller;

    public DInputController()
    { }

    public DInputController(PnPDetails details)
    {
        if (details is null)
            throw new Exception("DInputController PnPDetails is null");

        AttachDetails(details);
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRumble();
            Unplug();

            joystick?.Dispose();
            joystick = null;
            controller = null;
        }

        base.Dispose(disposing);
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
                        controller = new GenericController(details.VendorID, details.ProductID, 64, -1);
                        UserIndex = (byte)joystick.Properties.JoystickId;
                        break;
                    }
                }
                catch { }
            }
        }

        // unsupported controller
        if (joystick is null)
            LogManager.LogError($"Couldn't find matching DirectInput controller: VID:{details.GetVendorID()} and PID:{details.GetProductID()}");

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

    public override bool IsConnected() => joystick is not null && !joystick.IsDisposed;

    public override void Plug()
    {
        if (!IsConnected())
            return;

        // Acquire joystick
        try
        {
            joystick?.Acquire();
            controller?.Open(false);
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
            controller?.EndRead();
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
            controller?.Close();
        }
        catch { }

        base.Unplug();
    }
}