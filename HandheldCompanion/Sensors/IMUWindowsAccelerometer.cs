using HandheldCompanion.Devices;
using HandheldCompanion.Shared;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Sensors;

public sealed class IMUWindowsAccelerometer : IMUSensor
{
    private WindowsSensorHandle? windowsSensor;

    public IMUWindowsAccelerometer(int updateInterval)
    {
        this.updateInterval = updateInterval;
        UpdateSensor();
    }

    public override void UpdateSensor()
    {
        windowsSensor?.Dispose();
        windowsSensor = WindowsSensorManager.Find(WindowsSensorKind.Accelerometer);
        sensor = windowsSensor;

        if (windowsSensor is null)
            LogManager.LogWarning("{0} not initialised as a legacy Windows accelerometer", ToString());
        else
            LogManager.LogInformation("{0} initialised as a legacy Windows accelerometer", ToString());
    }

    public override void StopListening()
    {
        windowsSensor?.Dispose();
        windowsSensor = null;
        sensor = null;
        base.StopListening();
    }

    public override SensorReading GetCurrentReading(bool center = false, bool ratio = false)
    {
        if (windowsSensor is null)
            return reading;

        try
        {
            (double x, double y, double z) = windowsSensor.Read();
            IDevice device = IDevice.GetCurrent();
            Vector3 axis = device.AcceleroMatrix.Axis;
            int[] remapIndices = device.AcceleroMatrix.AxisRemapIndices;

            readingAxis[remapIndices[0]] = x;
            readingAxis[remapIndices[1]] = y;
            readingAxis[remapIndices[2]] = z;
            reading.reading.X = (float)readingAxis[0] * axis.X;
            reading.reading.Y = (float)readingAxis[1] * axis.Y;
            reading.reading.Z = (float)readingAxis[2] * axis.Z;
            reading.timestamp = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ObjectDisposedException)
        {
            LogManager.LogTrace("Unable to read legacy Windows accelerometer: {0}", ex.Message);
        }

        return reading;
    }
}
