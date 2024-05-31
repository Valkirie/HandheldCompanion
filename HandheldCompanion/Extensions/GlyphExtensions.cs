using HandheldCompanion.Utils;

namespace HandheldCompanion.Extensions
{
    public static class GlyphExtensions
    {
        public static string ToGlyph(this MotionInput motionInput)
        {
            switch (motionInput)
            {
                default:
                case MotionInput.LocalSpace:
                    return "\uF272";
                case MotionInput.PlayerSpace:
                    return "\uF119";
                case MotionInput.WorldSpace:
                    return "\uE714";
                /*
            case MotionInput.AutoRollYawSwap:
                return "\uE7F8";
                */
                case MotionInput.JoystickSteering:
                    return "\uEC47";
            }
        }

        public static string ToGlyph(this MotionOutput motionOuput)
        {
            switch (motionOuput)
            {
                default:
                case MotionOutput.Disabled:
                    return "\uE8D8";
                case MotionOutput.RightStick:
                    return "\uF109";
                case MotionOutput.LeftStick:
                    return "\uF108";
                case MotionOutput.MoveCursor:
                    return "\uE962";
                case MotionOutput.ScrollWheel:
                    return "\uEC8F";
            }
        }
    }
}
