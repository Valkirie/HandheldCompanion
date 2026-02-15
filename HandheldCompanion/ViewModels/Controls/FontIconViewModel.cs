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
                if (_Glyph != value)
                {
                    _Glyph = value;
                    OnPropertyChanged(nameof(Glyph));
                }
            }
        }

        private string _FontFamily = string.Empty;
        public string FontFamily
        {
            get
            {
                return _FontFamily;
            }
            set
            {
                if (_FontFamily != value)
                {
                    _FontFamily = value;
                    OnPropertyChanged(nameof(FontFamily));
                }
            }
        }

        private Brush? _Foreground;
        public Brush? Foreground
        {
            get
            {
                return _Foreground;
            }
            set
            {
                if (_Foreground != value)
                {
                    _Foreground = value;
                    OnPropertyChanged(nameof(Foreground));
                }
            }
        }

        public FontIconViewModel(Hotkey hotkey, HotkeyViewModel hotkeyViewModel, string glyph, Brush? glyphColor, string fontFamily)
        {
            Hotkey = hotkey;
            HotkeyViewModel = hotkeyViewModel;

            Glyph = glyph;
            FontFamily = fontFamily;

            if (glyphColor != null)
                Foreground = glyphColor;
        }
    }
}
