using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System.Numerics;
using Windows.Devices.Sensors;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Sensors;

public class IMUGyrometer : IMUSensor
{
    public new static SensorSpec sensorSpec = new()
    {
        minIn = -128.0f,
        maxIn = 128.0f,
        minOut = -2048.0f,
        maxOut = 2048.0f
    };

    public IMUGyrometer(SensorFamily sensorFamily, int updateInterval)
    {
        this.sensorFamily = sensorFamily;
        this.updateInterval = updateInterval;
        centerTimer.Interval = updateInterval * 6;

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
                sensor = SerialUSBIMU.GetDefault();
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
                    ((Gyrometer)sensor).ReportInterval = (uint)updateInterval;

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

    private void ReadingChanged(float GyroRoll, float GyroPitch, float GyroYaw)
    {
        switch (sensorFamily)
        {
            case SensorFamily.Controller:
                {
                    reading.X = reading_fixed.X = GyroRoll;
                    reading.Y = reading_fixed.Y = GyroPitch;
                    reading.Z = reading_fixed.Z = GyroYaw;

                    base.ReadingChanged();
                }
                break;
        }
    }

    private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
    {
        /*
        this.reading.X = this.reading_fixed.X = (float)filter.axis1Filter.Filter(AccelerationG.X, IMU.DeltaSeconds);
        this.reading.Y = this.reading_fixed.Y = (float)filter.axis2Filter.Filter(AccelerationG.Y, IMU.DeltaSeconds);
        this.reading.Z = this.reading_fixed.Z = (float)filter.axis3Filter.Filter(AccelerationG.Z, IMU.DeltaSeconds);
        */
        reading.X = reading_fixed.X = AngularVelocityDeg.X;
        reading.Y = reading_fixed.Y = AngularVelocityDeg.Y;
        reading.Z = reading_fixed.Z = AngularVelocityDeg.Z;

        base.ReadingChanged();
    }

    private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
    {
        if (sensor is null)
            return;

        foreach (var axis in reading_axis.Keys)
            switch (MainWindow.CurrentDevice.AngularVelocityAxisSwap[axis])
            {
                default:
                case 'X':
                    reading_axis[axis] = args.Reading.AngularVelocityX;
                    break;
                case 'Y':
                    reading_axis[axis] = args.Reading.AngularVelocityY;
                    break;
                case 'Z':
                    reading_axis[axis] = args.Reading.AngularVelocityZ;
                    break;
            }

        reading.X = reading_fixed.X = (float)reading_axis['X'] * MainWindow.CurrentDevice.AngularVelocityAxis.X;
        reading.Y = reading_fixed.Y = (float)reading_axis['Y'] * MainWindow.CurrentDevice.AngularVelocityAxis.Y;
        reading.Z = reading_fixed.Z = (float)reading_axis['Z'] * MainWindow.CurrentDevice.AngularVelocityAxis.Z;

        base.ReadingChanged();
    }

    public new Vector3 GetCurrentReading()
    {
        return this.reading;
    }
}