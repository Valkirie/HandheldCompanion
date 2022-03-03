using ControllerCommon;
using System.Numerics;

namespace ControllerService.Sensors
{
    public abstract class XInputSensor
    {
        protected Vector3 reading = new();
        protected XInputController controller;
        protected PipeServer pipeServer;

        public static float MaxValue = 128.0f;

        protected XInputSensor(XInputController controller, PipeServer pipeServer)
        {
            this.controller = controller;
            this.pipeServer = pipeServer;
        }
    }
}
