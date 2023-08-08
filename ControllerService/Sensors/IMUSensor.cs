using System;
using System.Collections.Generic;
using System.Numerics;
using System.Timers;
using ControllerCommon;
using ControllerCommon.Utils;
using static ControllerCommon.Utils.CommonUtils;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService.Sensors;

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

    protected Timer centerTimer;

    protected bool disposed;
    public OneEuroFilter3D filter = new();
    protected Vector3 reading = new();

    protected Dictionary<char, double> reading_axis = new()
    {
        { 'X', 0.0d },
        { 'Y', 0.0d },
        { 'Z', 0.0d }
    };

    protected Vector3 reading_fixed;

    public object sensor;
    protected SensorFamily sensorFamily;
    protected int updateInterval;

    protected IMUSensor()
    {
        centerTimer = new Timer(100);
        centerTimer.AutoReset = false;
        centerTimer.Elapsed += CenterTimer_Elapsed;
    }

    public virtual void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);
        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    private void CenterTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        reading_fixed.X = reading_fixed.Y = reading_fixed.Z = 0;
    }

    protected virtual void ReadingChanged()
    {
        if (centerTimer is null)
            return;

        // reset reading after inactivity
        centerTimer.Stop();
        centerTimer.Start();
    }

    public virtual void StartListening()
    {
    }

    public virtual void StopListening()
    {
        if (centerTimer is null)
            return;

        centerTimer.Stop();
        centerTimer.Dispose();
        centerTimer = null;
    }

    public override string ToString()
    {
        return GetType().Name;
    }

    protected virtual Vector3 GetCurrentReading(bool center = false, bool ratio = false)
    {
        return center ? reading_fixed : reading;
    }

    public Vector3 GetCurrentReadingRaw(bool center = false, bool ratio = false)
    {
        return center ? reading_fixed : reading;
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