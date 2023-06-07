using ControllerCommon.Controllers;
using ControllerCommon.Platforms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ControllerCommon.Pipes
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
        private string jsonString;

        public Profile GetValue()
        {
            return JsonConvert.DeserializeObject<Profile>(jsonString, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }

        public PipeClientProfile()
        {
            code = PipeCode.CLIENT_PROFILE;
        }

        public PipeClientProfile(Profile profile) : this()
        {
            this.jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }
    }

    [Serializable]
    public partial class PipeClientProcess : PipeMessage
    {
        public string executable;
        public PlatformType platform;

        public PipeClientProcess()
        {
            code = PipeCode.CLIENT_PROCESS;
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
    public partial class PipeClientInputs : PipeMessage
    {
        private string jsonString;

        public ControllerState GetValue()
        {
            return JsonConvert.DeserializeObject<ControllerState>(jsonString, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }

        public PipeClientInputs()
        {
            code = PipeCode.CLIENT_INPUT;
        }

        public PipeClientInputs(ControllerState inputs) : this()
        {
            jsonString = JsonConvert.SerializeObject(inputs, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }
    }

    [Serializable]
    public partial class PipeClientMovements : PipeMessage
    {
        public ControllerMovements Movements;

        public PipeClientMovements()
        {
            code = PipeCode.CLIENT_MOVEMENTS;
        }

        public PipeClientMovements(ControllerMovements movements) : this()
        {
            Movements = movements;
        }
    }

    [Serializable]
    public partial class PipeClientVibration : PipeMessage
    {
        public byte LargeMotor;
        public byte SmallMotor;

        public PipeClientVibration()
        {
            code = PipeCode.SERVER_VIBRATION;
        }
    }

    [Serializable]
    public partial class PipeClientControllerConnect : PipeMessage
    {
        public string ControllerName;
        public ControllerCapacities Capacacities;

        public PipeClientControllerConnect()
        {
            code = PipeCode.CLIENT_CONTROLLER_CONNECT;
        }

        public PipeClientControllerConnect(string name, ControllerCapacities capacities) : this()
        {
            ControllerName = name;
            Capacacities = capacities;
        }
    }

    [Serializable]
    public partial class PipeClientControllerDisconnect : PipeMessage
    {
        public PipeClientControllerDisconnect()
        {
            code = PipeCode.CLIENT_CONTROLLER_DISCONNECT;
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
    #endregion
}
