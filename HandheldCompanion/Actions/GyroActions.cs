using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public class GyroActions : IActions
    {
        public MotionInput MotionInput = MotionInput.JoystickCamera;
        public MotionMode MotionMode = MotionMode.Off;

        public ButtonState MotionTrigger = new();

        public GyroActions()
        {
        }
    }
}
