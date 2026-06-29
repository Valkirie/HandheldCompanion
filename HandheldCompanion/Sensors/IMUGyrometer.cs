using HandheldCompanion.Devices;
using HandheldCompanion.Shared;
using System;
using System.Numerics;
using Windows.Devices.Sensors;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Sensors;

public class IMUGyrometer : IMUSensor
{
    public IMUGyrometer(SensorFamily sensorFamily, int updateInterval, float threshold)
    {
        this.sensorFamily = sensorFamily;
        this.updateInterval = updateInterval;
        this.threshold = threshold;

        UpdateSensor();
    }

    public void UpdateSensor()
    {
        switch (sensorFamily)
        {
            case SensorFamily.Windows:
                sensor = Gyrometer.GetDefault();
                break;
            case SensorFamily.SerialUSBIMU:
                sensor = SerialUSBIMU.GetCurrent();
                break;
            case SensorFamily.Controller:
                sensor = new object();
                break;
        }

        if (sensor is null)
        {
            LogManager.LogWarning("{0} not initialised as a {1}", ToString(), sensorFamily.ToString());
            return;
        }

        switch (sensorFamily)
        {
            case SensorFamily.Windows:
                {
                    ((Gyrometer)sensor).ReportInterval = Math.Max(((Gyrometer)sensor).MinimumReportInterval, (uint)updateInterval);
                    LogManager.LogInformation("{0} initialised as a {1}. Report interval set to {2}ms", ToString(),
                        sensorFamily.ToString(), updateInterval);
                }
                break;
            case SensorFamily.SerialUSBIMU:
                LogManager.LogInformation("{0} initialised as a {1}. Baud rate set to {2}", ToString(),
                    sensorFamily.ToString(), ((SerialUSBIMU)sensor).GetInterval());
                break;
            case SensorFamily.Controller:
                LogManager.LogInformation("{0} initialised as a {1}", ToString(), sensorFamily.ToString());
                break;
        }

        StartListening();
    }

    public override void StartListening()
    {
        switch (sensorFamily)
        {
            case SensorFamily.Windows:
                if (sensor is not null)
                    ((Gyrometer)sensor).ReadingChanged += ReadingChanged;
                break;
            case SensorFamily.SerialUSBIMU:
                if (sensor is not null)
                    ((SerialUSBIMU)sensor).ReadingChanged += ReadingChanged;
                break;
        }
    }

    public override void StopListening()
    {
        if (sensor is null)
            return;

        switch (sensorFamily)
        {
            case SensorFamily.Windows:
                if (sensor is not null)
                    ((Gyrometer)sensor).ReadingChanged -= ReadingChanged;
                break;
            case SensorFamily.SerialUSBIMU:
                if (sensor is not null)
                    ((SerialUSBIMU)sensor).ReadingChanged -= ReadingChanged;
                break;
        }

        sensor = null;

        base.StopListening();
    }

    private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg, double timestamp)
    {
        reading.reading.X = AngularVelocityDeg.X;
        reading.reading.Y = AngularVelocityDeg.Y;
        reading.reading.Z = AngularVelocityDeg.Z;
        reading.timestamp = timestamp;

        base.ReadingChanged();
    }

    private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
    {
        IDevice device = IDevice.GetCurrent();
        var gyrometerAxisSwap = device.GyroMatrix.AxisSwap;
        Vector3 gyrometerAxis = device.GyroMatrix.Axis;
        int[] remapIndices = device.GyroMatrix.AxisRemapIndices;

        // Get raw readings with threshold check
        double rawX = Math.Abs(args.Reading.AngularVelocityX) >= threshold ? 0 : args.Reading.AngularVelocityX;
        double rawY = Math.Abs(args.Reading.AngularVelocityY) >= threshold ? 0 : args.Reading.AngularVelocityY;
        double rawZ = Math.Abs(args.Reading.AngularVelocityZ) >= threshold ? 0 : args.Reading.AngularVelocityZ;

        // Direct axis remapping using pre-computed indices
        // remapIndices[input] tells us which output axis each input goes to
        readingAxis[remapIndices[0]] = rawX;  // X input → output axis at remapIndices[0]
        readingAxis[remapIndices[1]] = rawY;  // Y input → output axis at remapIndices[1]
        readingAxis[remapIndices[2]] = rawZ;  // Z input → output axis at remapIndices[2]

        reading.reading.X = (float)readingAxis[0] * gyrometerAxis.X;
        reading.reading.Y = (float)readingAxis[1] * gyrometerAxis.Y;
        reading.reading.Z = (float)readingAxis[2] * gyrometerAxis.Z;
        reading.timestamp = args.Reading.Timestamp.DateTime.TimeOfDay.TotalMilliseconds;

        base.ReadingChanged();
    }

    public SensorReading GetCurrentReading()
    {
        return this.reading;
    }
}