namespace HandheldCompanion.Controllers
{
    // Function to map the Xbox rumble value to Nintendo Switch HD Rumble
    public class RumbleConverter
    {
        // Define the minimum and maximum values for frequency and amplitude
        public const float MinFrequency = 80f;  // Hz
        public const float MaxFrequency = 240f; // Hz
        private const float MinAmplitude = 0.0f;
        private const float MaxAmplitude = 1.0f;

        // Convert an Xbox rumble value (0-255) to a Switch amplitude (0.0 - 1.0)
        public static float ConvertAmplitude(byte xboxValue)
        {
            return xboxValue / 255.0f;
        }

        // Convert an Xbox rumble value (0-255) to a Switch frequency (40 Hz - 320 Hz)
        public static float ConvertFrequency(byte xboxValue)
        {
            return MinFrequency + (xboxValue / 255.0f) * (MaxFrequency - MinFrequency);
        }
    }
}
