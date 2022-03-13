using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;
using SensorType = ControllerCommon.SensorType;

namespace ControllerService.Sensors
{
    public class XInputGirometer : XInputSensor
    {
        public Gyrometer sensor;
        public static SensorSpec sensorSpec = new SensorSpec()
        {
            minIn = -128.0f,
            maxIn = 128.0f,
            minOut = -2048.0f,
            maxOut = 2048.0f,
        };

        private readonly ILogger logger;

        public XInputGirometer(XInputController controller, ILogger logger, PipeServer pipeServer) : base(controller, pipeServer)
        {
            this.logger = logger;

            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = (uint)updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                // (re)center
                updateTimer.Interval = updateInterval * 6;

                sensor.ReadingChanged += ReadingHasChanged;
            }
            else
            {
                logger.LogWarning("{0} not initialised.", this.ToString());
            }
        }

        private void ReadingHasChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            // swapping Y and Z
            this.reading.X = this.reading_fixed.X = (float)reading.AngularVelocityX;
            this.reading.Y = this.reading_fixed.Y = (float)reading.AngularVelocityZ;
            this.reading.Z = this.reading_fixed.Z = (float)reading.AngularVelocityY;

            // reset reading after inactivity
            updateTimer.Stop();
            updateTimer.Start();

            logger?.LogDebug("XInputGirometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z);

            // update client(s)
            if (ControllerService.CurrentTag == "ProfileSettingsMode0")
                pipeServer?.SendMessage(new PipeSensor(this.reading, SensorType.Girometer));
        }

        public new Vector3 GetCurrentReading(bool center = false)
        {
            Vector3 reading = new Vector3()
            {
                X = center ? this.reading_fixed.X : this.reading.X,
                Y = center ? this.reading_fixed.Y : this.reading.Y,
                Z = center ? this.reading_fixed.Z : this.reading.Z
            };

            if (controller.virtualTarget != null)
            {
                reading *= controller.profile.gyrometer;

                reading.Y *= controller.WidhtHeightRatio;

                reading.Z = controller.profile.steering == 0 ? this.reading.Z : this.reading.Y;
                reading.Y = controller.profile.steering == 0 ? this.reading.Y : this.reading.Z;
                reading.X = controller.profile.steering == 0 ? this.reading.X : this.reading.X;

                if (controller.profile.inverthorizontal)
                {
                    reading.Y *= -1.0f;
                    reading.Z *= -1.0f;
                }

                if (controller.profile.invertvertical)
                {
                    reading.Y *= -1.0f;
                    reading.X *= -1.0f;
                }
            }

            return reading;
        }
    }
}
