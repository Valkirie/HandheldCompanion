using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
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
                ((Accelerometer)sensor).ReadingChanged += ReadingChanged;
                break;
            case SensorFamily.SerialUSBIMU:
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
                ((Accelerometer)sensor).ReadingChanged -= ReadingChanged;
                break;
            case SensorFamily.SerialUSBIMU:
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
        foreach (char axis in reading_axis.Keys)
        {
            switch (IDevice.GetCurrent().AccelerometerAxisSwap[axis])
            {
                default:
                case 'X':
                    reading_axis[axis] = args.Reading.AccelerationX;
                    break;
                case 'Y':
                    reading_axis[axis] = args.Reading.AccelerationY;
                    break;
                case 'Z':
                    reading_axis[axis] = args.Reading.AccelerationZ;
                    break;
            }
        }

        reading.reading.X = (float)reading_axis['X'] * IDevice.GetCurrent().AccelerometerAxis.X;
        reading.reading.Y = (float)reading_axis['Y'] * IDevice.GetCurrent().AccelerometerAxis.Y;
        reading.reading.Z = (float)reading_axis['Z'] * IDevice.GetCurrent().AccelerometerAxis.Z;
        reading.timestamp = args.Reading.Timestamp.DateTime.TimeOfDay.TotalMilliseconds;

        base.ReadingChanged();
    }

    private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
    {
        // throw new NotImplementedException();
    }

    public new SensorReading GetCurrentReading(bool center = false, bool ratio = false)
    {
        return this.reading;
    }
}