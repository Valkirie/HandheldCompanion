using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
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
                ((Gyrometer)sensor).ReadingChanged += ReadingChanged;
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
                ((Gyrometer)sensor).ReadingChanged -= ReadingChanged;
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
        reading.reading.X = AngularVelocityDeg.X;
        reading.reading.Y = AngularVelocityDeg.Y;
        reading.reading.Z = AngularVelocityDeg.Z;
        reading.timestamp = timestamp;

        base.ReadingChanged();
    }

    private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
    {
        foreach (char axis in reading_axis.Keys)
        {
            switch (IDevice.GetCurrent().GyrometerAxisSwap[axis])
            {
                default:
                case 'X':
                    if (!(Math.Abs(args.Reading.AngularVelocityX) >= threshold))
                        reading_axis[axis] = args.Reading.AngularVelocityX;
                    break;
                case 'Y':
                    if (!(Math.Abs(args.Reading.AngularVelocityY) >= threshold))
                        reading_axis[axis] = args.Reading.AngularVelocityY;
                    break;
                case 'Z':
                    if (!(Math.Abs(args.Reading.AngularVelocityZ) >= threshold))
                        reading_axis[axis] = args.Reading.AngularVelocityZ;
                    break;
            }
        }

        reading.reading.X = (float)reading_axis['X'] * IDevice.GetCurrent().GyrometerAxis.X;
        reading.reading.Y = (float)reading_axis['Y'] * IDevice.GetCurrent().GyrometerAxis.Y;
        reading.reading.Z = (float)reading_axis['Z'] * IDevice.GetCurrent().GyrometerAxis.Z;
        reading.timestamp = args.Reading.Timestamp.DateTime.TimeOfDay.TotalMilliseconds;

        base.ReadingChanged();
    }

    public new SensorReading GetCurrentReading()
    {
        return this.reading;
    }
}