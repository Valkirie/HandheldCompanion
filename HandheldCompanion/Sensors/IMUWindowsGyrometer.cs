using HandheldCompanion.Devices;
using HandheldCompanion.Shared;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Sensors;

public sealed class IMUWindowsGyrometer : IMUSensor
{
    private WindowsSensorHandle? windowsSensor;

    public IMUWindowsGyrometer(int updateInterval, float threshold)
    {
        this.updateInterval = updateInterval;
        this.threshold = threshold;
        UpdateSensor();
    }

    public override void UpdateSensor()
    {
        windowsSensor?.Dispose();
        windowsSensor = WindowsSensorManager.Find(WindowsSensorKind.Gyrometer);
        sensor = windowsSensor;

        if (windowsSensor is null)
            LogManager.LogWarning("{0} not initialised as a legacy Windows gyrometer", ToString());
        else
            LogManager.LogInformation("{0} initialised as a legacy Windows gyrometer", ToString());
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
            Vector3 axis = device.GyroMatrix.Axis;
            int[] remapIndices = device.GyroMatrix.AxisRemapIndices;

            // Get raw readings with threshold check
            double rawX = Math.Abs(x) >= threshold ? 0 : x;
            double rawY = Math.Abs(y) >= threshold ? 0 : y;
            double rawZ = Math.Abs(z) >= threshold ? 0 : z;

            readingAxis[remapIndices[0]] = rawX;
            readingAxis[remapIndices[1]] = rawY;
            readingAxis[remapIndices[2]] = rawZ;
            reading.reading.X = (float)readingAxis[0] * axis.X;
            reading.reading.Y = (float)readingAxis[1] * axis.Y;
            reading.reading.Z = (float)readingAxis[2] * axis.Z;
            reading.timestamp = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ObjectDisposedException)
        {
            LogManager.LogTrace("Unable to read legacy Windows gyrometer: {0}", ex.Message);
        }

        return reading;
    }
}
