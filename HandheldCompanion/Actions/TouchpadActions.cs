using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using System;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public sealed class TouchpadActions : ButtonActions
    {
        public const float MinimumSwipeDuration = 16.0f;
        public const float MaximumSwipeDuration = 5000.0f;

        internal static readonly ButtonFlags[] Targets =
        [
            ButtonFlags.TouchpadCoordinateClick,
            ButtonFlags.TouchpadCoordinateTouch,
            ButtonFlags.TouchpadSwipe,
        ];

        private int x = DS4Touch.TOUCHPAD_WIDTH / 2;
        private int y = DS4Touch.TOUCHPAD_HEIGHT / 2;
        private int endX = DS4Touch.TOUCHPAD_WIDTH * 3 / 4;
        private int endY = DS4Touch.TOUCHPAD_HEIGHT / 2;
        private float swipeDuration = 300.0f;

        public int X { get => x; set => x = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_WIDTH - 1); }
        public int Y { get => y; set => y = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_HEIGHT - 1); }
        public int EndX { get => endX; set => endX = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_WIDTH - 1); }
        public int EndY { get => endY; set => endY = Math.Clamp(value, 0, DS4Touch.TOUCHPAD_HEIGHT - 1); }
        public float SwipeDuration
        {
            get => swipeDuration;
            set => swipeDuration = Math.Clamp(value, MinimumSwipeDuration, MaximumSwipeDuration);
        }

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
                if (swipeElapsed >= SwipeDuration)
                {
                    swipeElapsed = SwipeDuration;
                    swipeEndsAfterReport = true;
                }
            }

            wasPressed = pressed;
        }

        internal bool TryGetTouch(out TouchpadSample sample)
        {
            int x;
            int y;
            bool active;

            if (Button == ButtonFlags.TouchpadSwipe)
            {
                float progress = Math.Clamp(swipeElapsed / SwipeDuration, 0.0f, 1.0f);
                x = (int)MathF.Round(X + (EndX - X) * progress);
                y = (int)MathF.Round(Y + (EndY - Y) * progress);
                active = swipeActive;
                if (swipeEndsAfterReport)
                {
                    swipeActive = false;
                    swipeEndsAfterReport = false;
                }
            }
            else
            {
                x = X;
                y = Y;
                active = GetValue();
            }

            sample = active
                ? new TouchpadSample(Button, x, y)
                : default;
            return active;
        }
    }

    internal readonly record struct TouchpadSample(ButtonFlags Target, int X, int Y)
    {
        public int Priority => Target switch
        {
            ButtonFlags.TouchpadCoordinateClick => 3,
            ButtonFlags.TouchpadSwipe => 2,
            ButtonFlags.TouchpadCoordinateTouch => 1,
            _ => 0,
        };
    }
}
