using HandheldCompanion.Inputs;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public sealed class TouchpadActions : ButtonActions
    {
        public int X = 960;
        public int Y = 471;
        public int EndX = 1440;
        public int EndY = 471;
        public float SwipeDuration = 300.0f;

        [NonSerialized] private bool wasPressed;
        [NonSerialized] private bool swipeActive;
        [NonSerialized] private bool swipeEndsAfterReport;
        [NonSerialized] private float swipeElapsed;

        public TouchpadActions() { }

        public TouchpadActions(ButtonFlags button) : base(button) { }

        public static bool IsTouchpadTarget(ButtonFlags button) => button is
            ButtonFlags.TouchpadCoordinateClick or ButtonFlags.TouchpadCoordinateTouch or ButtonFlags.TouchpadSwipe;

        public override void Execute(ButtonFlags button, bool value, ShiftSlot shiftSlot, float delta)
        {
            base.Execute(button, value, shiftSlot, delta);

            bool pressed = GetValue();
            if (Button == ButtonFlags.TouchpadSwipe && pressed && !wasPressed)
            {
                swipeActive = true;
                swipeEndsAfterReport = false;
                swipeElapsed = 0.0f;
            }
            else if (swipeActive)
            {
                swipeElapsed += delta;
                if (swipeElapsed >= Math.Max(1.0f, SwipeDuration))
                {
                    swipeElapsed = Math.Max(1.0f, SwipeDuration);
                    swipeEndsAfterReport = true;
                }
            }

            wasPressed = pressed;
        }

        public bool ConsumeTouch(out int x, out int y)
        {
            if (Button == ButtonFlags.TouchpadSwipe)
            {
                float duration = Math.Max(1.0f, SwipeDuration);
                float progress = Math.Clamp(swipeElapsed / duration, 0.0f, 1.0f);
                x = (int)MathF.Round(X + (EndX - X) * progress);
                y = (int)MathF.Round(Y + (EndY - Y) * progress);
                bool active = swipeActive;
                if (swipeEndsAfterReport)
                {
                    swipeActive = false;
                    swipeEndsAfterReport = false;
                }
                return active;
            }

            x = X;
            y = Y;
            return GetValue();
        }
    }
}
