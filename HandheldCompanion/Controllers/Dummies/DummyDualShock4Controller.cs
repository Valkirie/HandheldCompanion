using HandheldCompanion.Controllers.SDL;
using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyDualShock4Controller : DualShock4Controller
    {
        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;
        protected override int GetTouchpads() => 1;
        protected override int GetTouchpadFingers(int touchpad) => 2;

        public override void Tick(long ticks, float delta, bool commit = false)
        {
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);
        }
    }
}
