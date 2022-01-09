using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Sensors;
using static ControllerCommon.Utils;

namespace ControllerService
{
    public class XInputInclinometer
    {
        public Inclinometer sensor;
        private Vector3 reading = new();

        private long prev_microseconds;
        private readonly OneEuroFilter3D accelFilter;

        public event ReadingChangedEventHandler ReadingHasChanged;
        public delegate void ReadingChangedEventHandler(XInputInclinometer sender, Vector3 e);

        private readonly ILogger logger;
        private readonly XInputController xinput;

        public XInputInclinometer(XInputController controller, ILogger logger)
        {
            this.logger = logger;
            this.xinput = controller;

            accelFilter = new OneEuroFilter3D();

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
