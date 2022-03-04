using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsHook;

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
    public partial class PipeServerController : PipeMessage
    {
        public string ProductName;
        public Guid InstanceGuid;
        public Guid ProductGuid;
        public int ProductIndex;

        public PipeServerController()
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
    public partial class PipeClientScreen : PipeMessage
    {
        public int width;
        public int height;

        public PipeClientScreen()
        {
            code = PipeCode.CLIENT_SCREEN;
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
    public partial class PipeClientCursor : PipeMessage
    {
        public CursorAction action;
        public float x;
        public float y;
        public MouseButtons button;

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
        Inclinometer = 2
    }

    [Serializable]
    public partial class PipeSensor : PipeMessage
    {
        public float x, y, z;
        public SensorType type;

        public PipeSensor(Vector3 reading, SensorType type)
        {
            code = PipeCode.SERVER_SENSOR;

            this.x = reading.X;
            this.y = reading.Y;
            this.z = reading.Z;

            this.type = type;
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
    #endregion
}
