using HandheldCompanion.Controllers.SDL;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyXbox360Controller : Xbox360Controller
    {
        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;
    }
}
