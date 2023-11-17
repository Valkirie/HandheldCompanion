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
                    reading.X = GyroRoll;
                    reading.Y = GyroPitch;
                    reading.Z = GyroYaw;

                    base.ReadingChanged();
                }
                break;
        }
    }

    private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
    {
        reading.X = AngularVelocityDeg.X;
        reading.Y = AngularVelocityDeg.Y;
        reading.Z = AngularVelocityDeg.Z;

        base.ReadingChanged();
    }

    private void ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
    {
        if (sensor is null)
            return;

        foreach (var axis in reading_axis.Keys)
            switch (MainWindow.CurrentDevice.GyrometerAxisSwap[axis])
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

        reading.X = (float)reading_axis['X'] * MainWindow.CurrentDevice.GyrometerAxis.X;
        reading.Y = (float)reading_axis['Y'] * MainWindow.CurrentDevice.GyrometerAxis.Y;
        reading.Z = (float)reading_axis['Z'] * MainWindow.CurrentDevice.GyrometerAxis.Z;

        base.ReadingChanged();
    }

    public new Vector3 GetCurrentReading()
    {
        return this.reading;
    }
}