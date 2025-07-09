using HandheldCompanion.Controllers.SDL;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyDualShock4Controller : DualShock4Controller
    {
        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;
        protected override int GetTouchpads() => 1;
        protected override int GetTouchpadFingers(int touchpad) => 2;
    }
}
