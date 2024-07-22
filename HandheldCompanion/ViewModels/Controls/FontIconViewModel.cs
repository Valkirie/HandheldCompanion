using System.Windows.Media;

namespace HandheldCompanion.ViewModels.Controls
{
    public class FontIconViewModel : BaseViewModel
    {
        private Hotkey Hotkey { get; set; }
        private HotkeyViewModel HotkeyViewModel { get; set; }

        private string _Glyph = string.Empty;
        public string Glyph
        {
            get
            {
                return _Glyph;
            }
            set
            {
                _Glyph = value;
                OnPropertyChanged(nameof(Glyph));
            }
        }

        private Brush _Foreground;
        public Brush Foreground
        {
            get
            {
                return _Foreground;
            }
            set
            {
                _Foreground = value;
                OnPropertyChanged(nameof(Foreground));
            }
        }

        public FontIconViewModel(Hotkey hotkey, HotkeyViewModel hotkeyViewModel, string glyph, Brush glyphColor)
        {
            Hotkey = hotkey;
            HotkeyViewModel = hotkeyViewModel;

            Glyph = glyph;

            if (glyphColor != null)
                Foreground = glyphColor;
        }
    }
}
