using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading.Tasks;
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
                sensor.ReportInterval = (uint)controller.updateInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingHasChanged;
            }
        }

        private void ReadingHasChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            // swapping Y and Z
            this.reading.X = (float)reading.AngularVelocityX;
            this.reading.Y = (float)reading.AngularVelocityZ;
            this.reading.Z = (float)reading.AngularVelocityY;

            logger?.LogDebug("XInputGirometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z);

            // update client(s)
            if (ControllerService.CurrentTag == "ProfileSettingsMode0")
                pipeServer?.SendMessage(new PipeSensor(this.reading, SensorType.Girometer));
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        public Vector3 GetCurrentReading()
        {
            float readingX = this.reading.X;
            float readingY = this.reading.Y;
            float readingZ = this.reading.Z;

            if (controller.virtualTarget != null)
            {
                this.reading *= controller.profile.gyrometer;

                this.reading.Y *= controller.WidhtHeightRatio;

                this.reading.Z = controller.profile.steering == 0 ? readingZ : readingY;
                this.reading.Y = controller.profile.steering == 0 ? readingY : readingZ;
                this.reading.X = controller.profile.steering == 0 ? readingX : readingX;

                if (controller.profile.inverthorizontal)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.Z *= -1.0f;
                }

                if (controller.profile.invertvertical)
                {
                    this.reading.Y *= -1.0f;
                    this.reading.X *= -1.0f;
                }
            }

            return this.reading;
        }
    }
}
