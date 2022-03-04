using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;
using SensorType = ControllerCommon.SensorType;

namespace ControllerService.Sensors
{
    public class XInputGirometer : XInputSensor
    {
        public Gyrometer sensor;

        public event ReadingChangedEventHandler ReadingChanged;
        public delegate void ReadingChangedEventHandler(XInputGirometer sender, Vector3 e);

        private readonly ILogger logger;

        public XInputGirometer(XInputController controller, ILogger logger, PipeServer pipeServer) : base(controller, pipeServer)
        {
            this.logger = logger;

            sensor = Gyrometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingHasChanged;
            }
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        private void ReadingHasChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            GyrometerReading reading = args.Reading;

            float readingX = this.reading.X = (float)reading.AngularVelocityX;
            float readingY = this.reading.Y = (float)reading.AngularVelocityZ;
            float readingZ = this.reading.Z = (float)reading.AngularVelocityY;

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

            logger?.LogDebug("XInputGirometer.ReadingChanged({0:00.####}, {1:00.####}, {2:00.####})", this.reading.X, this.reading.Y, this.reading.Z);

            // update client(s)
            if (ControllerService.CurrentTag == "ProfileSettingsMode0")
                pipeServer?.SendMessage(new PipeSensor(this.reading, SensorType.Girometer));

            // raise event
            ReadingChanged?.Invoke(this, this.reading);
        }
    }
}
