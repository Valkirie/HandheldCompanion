using ControllerCommon;
using System.Numerics;
using static ControllerCommon.Utils;

namespace ControllerService.Sensors
{
    public abstract class XInputSensor
    {
        protected Vector3 reading = new();
        protected XInputController controller;
        protected PipeServer pipeServer;

        protected static SensorSpec sensorSpec;
        protected int updateInterval;

        protected XInputSensor(XInputController controller, PipeServer pipeServer)
        {
            this.controller = controller;
            this.pipeServer = pipeServer;

            this.updateInterval = controller.updateInterval;
        }
    }
}
