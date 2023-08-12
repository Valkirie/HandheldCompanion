using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System.Numerics;
using Windows.Devices.Sensors;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Sensors;

public class IMUAccelerometer : IMUSensor
{
    public new static SensorSpec sensorSpec = new()
    {
        minIn = -2.0f,
        maxIn = 2.0f,
        minOut = short.MinValue,
        maxOut = short.MaxValue
    };

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
                ((Accelerometer)sensor).ReportInterval = (uint)updateInterval;
                filter.SetFilterAttrs(MainWindow.CurrentDevice.oneEuroSettings.minCutoff, MainWindow.CurrentDevice.oneEuroSettings.beta);

                LogManager.LogInformation("{0} initialised as a {1}. Report interval set to {2}ms", ToString(),
                    sensorFamily.ToString(), updateInterval);
                break;
            case SensorFamily.SerialUSBIMU:
                filter.SetFilterAttrs(((SerialUSBIMU)sensor).GetFilterCutoff(), ((SerialUSBIMU)sensor).GetFilterBeta());

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

    private void ReadingChanged(float GyroAccelX, float GyroAccelY, float GyroAccelZ)
    {
        switch (sensorFamily)
        {
            case SensorFamily.Controller:
            {
                reading.X = reading_fixed.X = GyroAccelX;
                reading.Y = reading_fixed.Y = GyroAccelY;
                reading.Z = reading_fixed.Z = GyroAccelZ;

                base.ReadingChanged();
            }
                break;
        }
    }

    private void ReadingChanged(Vector3 AccelerationG, Vector3 AngularVelocityDeg)
    {
        reading.X = reading_fixed.X = (float)filter.axis1Filter.Filter(AccelerationG.X, MotionManager.DeltaSeconds);
        reading.Y = reading_fixed.Y = (float)filter.axis2Filter.Filter(AccelerationG.Y, MotionManager.DeltaSeconds);
        reading.Z = reading_fixed.Z = (float)filter.axis3Filter.Filter(AccelerationG.Z, MotionManager.DeltaSeconds);

        base.ReadingChanged();
    }

    private void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
    {
        if (sensor is null)
            return;

        foreach (var axis in reading_axis.Keys)
            switch (MainWindow.CurrentDevice.AccelerationAxisSwap[axis])
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

        reading.X = reading_fixed.X = (float)reading_axis['X'] * MainWindow.CurrentDevice.AccelerationAxis.X;
        reading.Y = reading_fixed.Y = (float)reading_axis['Y'] * MainWindow.CurrentDevice.AccelerationAxis.Y;
        reading.Z = reading_fixed.Z = (float)reading_axis['Z'] * MainWindow.CurrentDevice.AccelerationAxis.Z;

        base.ReadingChanged();
    }

    private void Shaken(Accelerometer sender, AccelerometerShakenEventArgs args)
    {
        // throw new NotImplementedException();
    }

    public new Vector3 GetCurrentReading(bool center = false, bool ratio = false)
    {
        return this.reading;
    }
}