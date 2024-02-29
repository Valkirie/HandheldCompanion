namespace HandheldCompanion.ViewModels
{
    public class MotionInputViewModel : BaseViewModel
    {
        public string? Glyph { get; set; }
        public string? Description { get; set; }
        public bool HasGlyph => !string.IsNullOrWhiteSpace(Glyph);
    }
}
