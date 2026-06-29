using HandheldCompanion.Devices;
using HandheldCompanion.Shared;
using System;
using System.Numerics;
using Windows.Devices.Sensors;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Sensors;

public class IMUAccelerometer : IMUSensor
{
    public IMUAccelerometer(SensorFamily sensorFamily, int updateInterval)
    {
        this.sensorFamily = sensorFamily;
        this.updateInterval = updateInterval;

        UpdateSensor();
    }

    public void UpdateSensor()
    {
        switch (sensorFamily)
        {
            case SensorFamily.Windows:
                sensor = Accelerometer.GetDefault();
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
                ((Accelerometer)sensor).ReportInterval = Math.Max(((Accelerometer)sensor).MinimumReportInterval, (uint)updateInterval);
                LogManager.LogInformation("{0} initialised as a {1}. Report interval set to {2}ms", ToString(),
                    sensorFamily.ToString(), updateInterval);
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
                    ((Accelerometer)sensor).ReadingChanged += ReadingChanged;
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
                    ((Accelerometer)sensor).ReadingChanged -= ReadingChanged;
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
        reading.reading.X = AccelerationG.X;
        reading.reading.Y = AccelerationG.Y;
        reading.reading.Z = AccelerationG.Z;
        reading.timestamp = timestamp;

        base.ReadingChanged();
    }

    private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
    {
        IDevice device = IDevice.GetCurrent();
        var accelerometerAxisSwap = device.AcceleroMatrix.AxisSwap;
        Vector3 accelerometerAxis = device.AcceleroMatrix.Axis;
        int[] remapIndices = device.AcceleroMatrix.AxisRemapIndices;

        // Get raw readings
        double rawX = args.Reading.AccelerationX;
        double rawY = args.Reading.AccelerationY;
        double rawZ = args.Reading.AccelerationZ;

        // Direct axis remapping using pre-computed indices
        // remapIndices[input] tells us which output axis each input goes to
        readingAxis[remapIndices[0]] = rawX;  // X input → output axis at remapIndices[0]
        readingAxis[remapIndices[1]] = rawY;  // Y input → output axis at remapIndices[1]
        readingAxis[remapIndices[2]] = rawZ;  // Z input → output axis at remapIndices[2]

        reading.reading.X = (float)readingAxis[0] * accelerometerAxis.X;
        reading.reading.Y = (float)readingAxis[1] * accelerometerAxis.Y;
        reading.reading.Z = (float)readingAxis[2] * accelerometerAxis.Z;
        reading.timestamp = args.Reading.Timestamp.DateTime.TimeOfDay.TotalMilliseconds;

        base.ReadingChanged();
    }

    private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
    {
        // throw new NotImplementedException();
    }

    public SensorReading GetCurrentReading(bool center = false, bool ratio = false)
    {
        return this.reading;
    }
}