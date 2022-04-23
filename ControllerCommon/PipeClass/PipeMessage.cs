using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ControllerCommon
{
    [Serializable]
    public abstract class PipeMessage
    {
        public PipeCode code;
    }

    #region serverpipe
    [Serializable]
    public partial class PipeServerToast : PipeMessage
    {
        public string title;
        public string content;
        public string image = "Toast";

        public PipeServerToast()
        {
            code = PipeCode.SERVER_TOAST;
        }
    }

    [Serializable]
    public partial class PipeServerPing : PipeMessage
    {
        public PipeServerPing()
        {
            code = PipeCode.SERVER_PING;
        }
    }

    [Serializable]
    public partial class PipeServerHandheld : PipeMessage
    {
        public string ManufacturerName;
        public string ProductName;
        public string ProductIllustration;
        public bool ProductSupported;

        public string SensorName;

        public bool hasGyrometer;
        public bool hasAccelerometer;
        public bool hasInclinometer;

        public string ControllerName;
        public ushort ControllerVID;
        public ushort ControllerPID;
        public int ControllerIdx;

        public PipeServerHandheld()
        {
            code = PipeCode.SERVER_CONTROLLER;
        }
    }

    [Serializable]
    public partial class PipeServerSettings : PipeMessage
    {
        public Dictionary<string, string> settings = new();

        public PipeServerSettings()
        {
            code = PipeCode.SERVER_SETTINGS;
        }

        public PipeServerSettings(string key, string value) : this()
        {
            this.settings.Add(key, value);
        }
    }

    [Serializable]
    public partial class PipeGamepad : PipeMessage
    {
        public int Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;

        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;

        public PipeGamepad(Gamepad gamepad)
        {
            code = PipeCode.SERVER_GAMEPAD;
            Buttons = (int)gamepad.Buttons;
            LeftTrigger = gamepad.LeftTrigger;
            RightTrigger = gamepad.RightTrigger;

            LeftThumbX = gamepad.LeftThumbX;
            LeftThumbY = gamepad.LeftThumbY;
            RightThumbX = gamepad.RightThumbX;
            RightThumbY = gamepad.RightThumbY;
        }

        public Gamepad ToGamepad()
        {
            return new Gamepad()
            {
                Buttons = (GamepadButtonFlags)this.Buttons,
                LeftTrigger = this.LeftTrigger,
                RightTrigger = this.RightTrigger,
                LeftThumbX = this.LeftThumbX,
                LeftThumbY = this.LeftThumbY,
                RightThumbX = this.RightThumbX,
                RightThumbY = this.RightThumbY
            };
        }
    }
    #endregion

    #region clientpipe
    [Serializable]
    public partial class PipeClientProfile : PipeMessage
    {
        public Profile profile;

        public PipeClientProfile()
        {
            code = PipeCode.CLIENT_PROFILE;
        }
    }

    [Serializable]
    public partial class PipeClientSettings : PipeMessage
    {
        public Dictionary<string, object> settings = new();

        public PipeClientSettings()
        {
            code = PipeCode.CLIENT_SETTINGS;
        }

        public PipeClientSettings(string key, object value) : this()
        {
            this.settings.Add(key, value);
        }
    }

    [Serializable]
    public enum CursorAction
    {
        CursorUp = 0,
        CursorDown = 1,
        CursorMove = 2
    }

    [Serializable]
    public enum CursorButton
    {
        None = 0,
        TouchLeft = 1,
        TouchRight = 2
    }

    [Serializable]
    public partial class PipeClientCursor : PipeMessage
    {
        public CursorAction action;
        public double x;
        public double y;
        public CursorButton button;
        public int flags;

        public PipeClientCursor()
        {
            code = PipeCode.CLIENT_CURSOR;
        }
    }

    [Serializable]
    public enum HidderAction
    {
        Register = 0,
        Unregister = 1
    }

    [Serializable]
    public partial class PipeClientHidder : PipeMessage
    {
        public HidderAction action;
        public string path;

        public PipeClientHidder()
        {
            code = PipeCode.CLIENT_HIDDER;
        }
    }

    [Serializable]
    public partial class PipeConsoleArgs : PipeMessage
    {
        public string[] args;

        public PipeConsoleArgs()
        {
            code = PipeCode.CLIENT_CONSOLE;
        }
    }

    [Serializable]
    public partial class PipeShutdown : PipeMessage
    {
        public PipeShutdown()
        {
            code = PipeCode.FORCE_SHUTDOWN;
        }
    }

    [Serializable]
    public enum SensorType
    {
        Girometer = 0,
        Accelerometer = 1,
        Inclinometer = 2,
        Quaternion = 3
    }

    [Serializable]
    public partial class PipeSensor : PipeMessage
    {
        public float x, y, z;
        public float q_x, q_y, q_z, q_w;
        public SensorType type;

        public PipeSensor(SensorType type)
        {
            code = PipeCode.SERVER_SENSOR;
            this.type = type;
        }

        public PipeSensor(Vector3 reading, SensorType type) : this(type)
        {
            this.x = reading.X;
            this.y = reading.Y;
            this.z = reading.Z;
        }

        public PipeSensor(Quaternion qt, SensorType type) : this(type)
        {
            this.q_x = qt.X;
            this.q_y = qt.Y;
            this.q_z = qt.Z;
            this.q_w = qt.W;
        }

        public PipeSensor(Vector3 reading, Quaternion qt, SensorType type) : this(type)
        {
            this.x = reading.X;
            this.y = reading.Y;
            this.z = reading.Z;

            this.q_x = qt.X;
            this.q_y = qt.Y;
            this.q_z = qt.Z;
            this.q_w = qt.W;
        }
    }

    [Serializable]
    public partial class PipeNavigation : PipeMessage
    {
        public string Tag;

        public PipeNavigation(string Tag)
        {
            code = PipeCode.CLIENT_NAVIGATED;

            this.Tag = Tag;
        }
    }

    [Serializable]
    public partial class PipeOverlay : PipeMessage
    {
        public int Visibility;

        public PipeOverlay(int Visibility)
        {
            code = PipeCode.CLIENT_OVERLAY;

            this.Visibility = Visibility;
        }
    }

    [Serializable]
    public partial class PipeControllerIndex : PipeMessage
    {
        public int UserIndex;
        public string deviceInstancePath;
        public string baseContainerDeviceInstancePath;

        public PipeControllerIndex(int UserIndex, string deviceInstancePath, string baseContainerDeviceInstancePath)
        {
            code = PipeCode.CLIENT_CONTROLLERINDEX;

            this.UserIndex = UserIndex;
            this.deviceInstancePath = deviceInstancePath;
            this.baseContainerDeviceInstancePath = baseContainerDeviceInstancePath;
        }
    }
    #endregion
}
