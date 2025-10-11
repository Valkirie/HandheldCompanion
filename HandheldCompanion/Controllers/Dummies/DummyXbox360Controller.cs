using HandheldCompanion.Controllers.SDL;
using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyXbox360Controller : Xbox360Controller
    {
        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;

        public override void Tick(long ticks, float delta, bool commit = false)
        {
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);
        }
    }
}
