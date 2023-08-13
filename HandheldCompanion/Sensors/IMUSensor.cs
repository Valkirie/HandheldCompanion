using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Sensors;

[Flags]
public enum XInputSensorFlags
{
    Default = 0,
    RawValue = 1,
    Centered = 2,
    WithRatio = 4,
    CenteredRaw = RawValue | Centered,
    CenteredRatio = RawValue | WithRatio
}

[Flags]
public enum XInputSensorStatus
{
    Missing = 0,
    Ready = 1,
    Busy = 2
}

public abstract class IMUSensor : IDisposable
{
    protected static SensorSpec sensorSpec;

    protected bool disposed;
    public OneEuroFilter3D filter = new();
    protected Vector3 reading = new();

    protected Dictionary<char, double> reading_axis = new()
    {
        { 'X', 0.0d },
        { 'Y', 0.0d },
        { 'Z', 0.0d }
    };

    public object sensor;
    protected SensorFamily sensorFamily;
    protected int updateInterval;

    public event ReadingUpdatedEventHandler ReadingUpdated;
    public delegate void ReadingUpdatedEventHandler();

    protected IMUSensor()
    {
    }

    public virtual void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);
        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    protected virtual void ReadingChanged()
    {
        ReadingUpdated?.Invoke();
    }

    public virtual void StartListening()
    {
    }

    public virtual void StopListening()
    {
    }

    public override string ToString()
    {
        return GetType().Name;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
            if (disposing)
                StopListening();
        //dispose unmanaged resources
        disposed = true;
    }
}