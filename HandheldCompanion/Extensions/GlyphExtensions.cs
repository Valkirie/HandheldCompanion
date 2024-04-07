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

        public static string ToGlyph(this MotionOuput motionOuput)
        {
            switch (motionOuput)
            {
                default:
                case MotionOuput.Disabled:
                    return "\uE8D8";
                case MotionOuput.RightStick:
                    return "\uF109";
                case MotionOuput.LeftStick:
                    return "\uF108";
                case MotionOuput.MoveCursor:
                    return "\uE962";
                case MotionOuput.ScrollWheel:
                    return "\uEC8F";
            }
        }
    }
}
