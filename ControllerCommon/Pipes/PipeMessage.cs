using System;
using System.Collections.Generic;
using System.Numerics;
using ControllerCommon.Controllers;
using ControllerCommon.Platforms;
using Newtonsoft.Json;

namespace ControllerCommon.Pipes;

[Serializable]
public abstract class PipeMessage
{
    public PipeCode code;
}

#region serverpipe

[Serializable]
public class PipeServerToast : PipeMessage
{
    public string content;
    public string image = "Toast";
    public string title;

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
public class PipeServerSettings : PipeMessage
{
    public Dictionary<string, string> settings = new();

    public PipeServerSettings()
    {
        code = PipeCode.SERVER_SETTINGS;
    }

    public PipeServerSettings(string key, string value) : this()
    {
        settings.Add(key, value);
    }
}

#endregion

#region clientpipe

[Serializable]
public class PipeClientProfile : PipeMessage
{
    private string jsonString;

    public PipeClientProfile()
    {
        code = PipeCode.CLIENT_PROFILE;
    }

    public PipeClientProfile(Profile profile) : this()
    {
        jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
    }

    public Profile GetValue()
    {
        return JsonConvert.DeserializeObject<Profile>(jsonString, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });
    }
}

[Serializable]
public class PipeClientProcess : PipeMessage
{
    public string executable;
    public PlatformType platform;

    public PipeClientProcess()
    {
        code = PipeCode.CLIENT_PROCESS;
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

    public PipeClientSettings(string key, object value) : this()
    {
        settings.Add(key, value);
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
public class PipeClientCursor : PipeMessage
{
    public CursorAction action;
    public CursorButton button;
    public int flags;
    public double x;
    public double y;

    public PipeClientCursor()
    {
        code = PipeCode.CLIENT_CURSOR;
    }
}

[Serializable]
public class PipeClientInputs : PipeMessage
{
    private string jsonString;

    public PipeClientInputs()
    {
        code = PipeCode.CLIENT_INPUT;
    }

    public PipeClientInputs(ControllerState inputs) : this()
    {
        jsonString = JsonConvert.SerializeObject(inputs);
    }

    public ControllerState GetValue()
    {
        return JsonConvert.DeserializeObject<ControllerState>(jsonString);
    }
}

[Serializable]
public class PipeClientMovements : PipeMessage
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
public class PipeClientVibration : PipeMessage
{
    public byte LargeMotor;
    public byte SmallMotor;

    public PipeClientVibration()
    {
        code = PipeCode.SERVER_VIBRATION;
    }
}

[Serializable]
public class PipeClientControllerConnect : PipeMessage
{
    public ControllerCapacities Capacacities;
    public string ControllerName;

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
public class PipeClientControllerDisconnect : PipeMessage
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
public class PipeSensor : PipeMessage
{
    public float q_x, q_y, q_z, q_w;
    public SensorType type;
    public float x, y, z;

    public PipeSensor(SensorType type)
    {
        code = PipeCode.SERVER_SENSOR;
        this.type = type;
    }

    public PipeSensor(Vector3 reading, SensorType type) : this(type)
    {
        x = reading.X;
        y = reading.Y;
        z = reading.Z;
    }

    public PipeSensor(Quaternion qt, SensorType type) : this(type)
    {
        q_x = qt.X;
        q_y = qt.Y;
        q_z = qt.Z;
        q_w = qt.W;
    }

    public PipeSensor(Vector3 reading, Quaternion qt, SensorType type) : this(type)
    {
        x = reading.X;
        y = reading.Y;
        z = reading.Z;

        q_x = qt.X;
        q_y = qt.Y;
        q_z = qt.Z;
        q_w = qt.W;
    }
}

[Serializable]
public class PipeNavigation : PipeMessage
{
    public string Tag;

    public PipeNavigation(string Tag)
    {
        code = PipeCode.CLIENT_NAVIGATED;

        this.Tag = Tag;
    }
}

[Serializable]
public class PipeOverlay : PipeMessage
{
    public int Visibility;

    public PipeOverlay(int Visibility)
    {
        code = PipeCode.CLIENT_OVERLAY;

        this.Visibility = Visibility;
    }
}

#endregion