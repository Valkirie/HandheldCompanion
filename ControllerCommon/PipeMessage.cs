using System;
using System.Collections.Generic;

namespace ControllerCommon
{
    [Serializable]
    public abstract class PipeMessage
    {
        public PipeCode code;
    }

    #region serverpipe
    [Serializable]
    public class PipeServerToast : PipeMessage
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
    public class PipeServerPing : PipeMessage
    {
        public PipeServerPing()
        {
            code = PipeCode.SERVER_PING;
        }
    }

    [Serializable]
    public class PipeServerController : PipeMessage
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
    public class PipeServerSettings : PipeMessage
    {
        public Dictionary<string, string> settings = new();

        public PipeServerSettings()
        {
            code = PipeCode.SERVER_SETTINGS;
        }
    }
    #endregion

    #region clientpipe
    [Serializable]
    public class PipeClientProfile : PipeMessage
    {
        public Profile profile;

        public PipeClientProfile()
        {
            code = PipeCode.CLIENT_PROFILE;
        }
    }

    [Serializable]
    public class PipeClientScreen : PipeMessage
    {
        public int width;
        public int height;

        public PipeClientScreen()
        {
            code = PipeCode.CLIENT_SCREEN;
        }
    }

    [Serializable]
    public class PipeClientSettings : PipeMessage
    {
        public Dictionary<string, object> settings = new();

        public PipeClientSettings()
        {
            code = PipeCode.CLIENT_SETTINGS;
        }
    }

    [Serializable]
    public class PipeClientCursor : PipeMessage
    {
        public int action; // 0 = up, 1 = down, 2 = move
        public float x;
        public float y;
        public int button;

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
    public class PipeClientHidder : PipeMessage
    {
        public HidderAction action;
        public string path;

        public PipeClientHidder()
        {
            code = PipeCode.CLIENT_HIDDER;
        }
    }

    [Serializable]
    public class PipeConsoleArgs : PipeMessage
    {
        public string[] args;

        public PipeConsoleArgs()
        {
            code = PipeCode.CLIENT_CONSOLE;
        }
    }

    [Serializable]
    public class PipeShutdown : PipeMessage
    {
        public PipeShutdown()
        {
            code = PipeCode.FORCE_SHUTDOWN;
        }
    }
    #endregion
}
