using Microsoft.Extensions.Logging;
using System.Numerics;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService
{
    public class XInputInclinometer
    {
        public Inclinometer sensor;
        private Vector3 reading = new();

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputInclinometer sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController xinput;

        public XInputInclinometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.xinput = controller;

            sensor = Inclinometer.GetDefault();
            if (sensor != null)
            {
                sensor.ReportInterval = sensor.MinimumReportInterval;
                logger.LogInformation("{0} initialised. Report interval set to {1}ms", this.ToString(), sensor.ReportInterval);

                sensor.ReadingChanged += ReadingChanged;
            }
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        private void ReadingChanged(Inclinometer sender, InclinometerReadingChangedEventArgs args)
        {
            InclinometerReading reading = args.Reading;

            // raise event
            ReadingHasChanged?.Invoke(this, this.reading);
        }
    }
}
