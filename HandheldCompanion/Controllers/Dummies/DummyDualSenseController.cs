using HandheldCompanion.Controllers.SDL;
using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyDualSenseController : DualSenseController
    {
        public DummyDualSenseController()
        {
            // The dummy describes mapping destinations on the emulated DualSense;
            // it is not an input device and must not advertise source touchpads.
            TargetButtons.Add(ButtonFlags.LeftPadClick);
            TargetButtons.Add(ButtonFlags.RightPadClick);
            TargetButtons.Add(ButtonFlags.CenterPadClick);
            TargetButtons.Add(ButtonFlags.MicrophoneMute);
            TargetAxis.Add(AxisLayoutFlags.LeftPad);
            TargetAxis.Add(AxisLayoutFlags.RightPad);
        }

        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;
        protected override int GetTouchpads() => 0;
        protected override int GetTouchpadFingers(int touchpad) => 0;

        public override void Tick(long ticks, float delta, bool commit = false)
        {
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);
        }
    }
}
