namespace HandheldCompanion.ViewModels.Misc
{
    public class ScreenFrequencyViewModel
    {
        public int Frequency { get; }

        public ScreenFrequencyViewModel(int frequency)
        {
            Frequency = frequency;
        }

        public override string ToString()
        {
            return $"{Frequency} Hz";
        }
    }
}
