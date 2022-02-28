using System.Numerics;

namespace ControllerService.Sensors
{
    public abstract class XInputSensor
    {
        protected Vector3 reading = new();
        protected XInputController controller;

        protected static float MaxValue = 0.0f;
        protected static float MinValue = 0.0f;

        protected XInputSensor(XInputController controller)
        {
            this.controller = controller;
        }
    }
}
