using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Numerics;
using ControllerCommon.Controllers;
using ControllerCommon.Platforms;
using MemoryPack;
using Newtonsoft.Json;

namespace ControllerCommon.Pipes;

[Serializable]
[MemoryPackable]
[MemoryPackUnion(0, typeof(PipeServerToast))]
[MemoryPackUnion(1, typeof(PipeServerPing))]
[MemoryPackUnion(2, typeof(PipeServerSettings))]
[MemoryPackUnion(3, typeof(PipeClientProfile))]
[MemoryPackUnion(4, typeof(PipeClientProcess))]
[MemoryPackUnion(5, typeof(PipeClientSettings))]
[MemoryPackUnion(6, typeof(PipeClientCursor))]
[MemoryPackUnion(7, typeof(PipeClientInputs))]
[MemoryPackUnion(8, typeof(PipeClientMovements))]
[MemoryPackUnion(9, typeof(PipeClientVibration))]
[MemoryPackUnion(10, typeof(PipeClientControllerConnect))]
[MemoryPackUnion(11, typeof(PipeClientControllerDisconnect))]
[MemoryPackUnion(12, typeof(PipeSensor))]
[MemoryPackUnion(13, typeof(PipeNavigation))]
[MemoryPackUnion(14, typeof(PipeOverlay))]
[MemoryPackUnion(15, typeof(PipeServerControllerConnect))]
public abstract partial class PipeMessage
{
    public PipeCode code;
}

#region serverpipe

[Serializable]
[MemoryPackable]
public partial class PipeServerToast : PipeMessage
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
[MemoryPackable]
public partial class PipeServerPing : PipeMessage
{
    public PipeServerPing()
    {
        code = PipeCode.SERVER_PING;
    }
}

[Serializable]
[MemoryPackable]
public partial class PipeServerSettings : PipeMessage
{
    public Dictionary<string, string> Settings { get; set; } = new();

    public PipeServerSettings()
    {
        code = PipeCode.SERVER_SETTINGS;
    }
}

[Serializable]
[MemoryPackable]
public partial class PipeServerControllerConnect : PipeMessage
{
    public PipeServerControllerConnect()
    {
        code = PipeCode.SERVER_CONTROLLER_CONNECT;
    }
}

#endregion

#region clientpipe

[Serializable]
[MemoryPackable]
public partial class PipeClientProfile : PipeMessage
{
    public Profile profile;

    public PipeClientProfile()
    {
        code = PipeCode.CLIENT_PROFILE;
    }

    [MemoryPackConstructor]
    public PipeClientProfile(Profile profile) : this()
    {
        this.profile = profile;
    }
}

[Serializable]
[MemoryPackable]
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
[MemoryPackable]
public partial class PipeClientSettings : PipeMessage
{
    public Dictionary<string, string> Settings { get; set; } = new();

    public PipeClientSettings()
    {
        code = PipeCode.CLIENT_SETTINGS;
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
[MemoryPackable]
public partial class PipeClientCursor : PipeMessage
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
[MemoryPackable]
public partial class PipeClientInputs : PipeMessage
{
    public ControllerState controllerState;

    public PipeClientInputs()
    {
        code = PipeCode.CLIENT_INPUT;
    }

    [MemoryPackConstructor]
    public PipeClientInputs(ControllerState controllerState) : this()
    {
        this.controllerState = controllerState;
    }
}

[Serializable]
[MemoryPackable]
public partial class PipeClientMovements : PipeMessage
{
    public ControllerMovements Movements;

    public PipeClientMovements()
    {
        code = PipeCode.CLIENT_MOVEMENTS;
    }

    [MemoryPackConstructor]
    public PipeClientMovements(ControllerMovements movements) : this()
    {
        Movements = movements;
    }
}

[Serializable]
[MemoryPackable]
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
[MemoryPackable]
public partial class PipeClientControllerConnect : PipeMessage
{
    public ControllerCapacities Capacities;
    public string ControllerName;

    public PipeClientControllerConnect()
    {
        code = PipeCode.CLIENT_CONTROLLER_CONNECT;
    }

    [MemoryPackConstructor]
    public PipeClientControllerConnect(string controllerName, ControllerCapacities capacities) : this()
    {
        ControllerName = controllerName;
        Capacities = capacities;
    }
}

[Serializable]
[MemoryPackable]
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
[MemoryPackable]
public partial class PipeSensor : PipeMessage
{
    public Vector3 reading;
    public Quaternion quaternion;
    public SensorType sensorType;

    public PipeSensor()
    {
        code = PipeCode.SERVER_SENSOR;
    }

    [MemoryPackConstructor]
    public PipeSensor(Vector3 reading, Quaternion quaternion, SensorType sensorType) : this()
    {
        this.reading = reading;
        this.quaternion = quaternion;
        this.sensorType = sensorType;
    }
}

[Serializable]
[MemoryPackable]
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
[MemoryPackable]
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