using HandheldCompanion.Extensions;
using HandheldCompanion.Utils;

namespace HandheldCompanion.ViewModels
{
    public class MotionOutputViewModel : BaseViewModel
    {
        public MotionOutput Value { get; set; }
        public string? Glyph { get; set; }
        public string? Description { get; set; }
        public bool HasGlyph => !string.IsNullOrWhiteSpace(Glyph);

        public MotionOutputViewModel() { }

        public MotionOutputViewModel(MotionOutput mode)
        {
            Value = mode;
            Glyph = mode.ToGlyph();
            Description = EnumUtils.GetDescriptionFromEnumValue(mode);
        }
    }
}
