using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.ViewModels.Controls
{
    public class GlyphViewModel : BaseViewModel
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

        public GlyphViewModel(Hotkey hotkey, HotkeyViewModel hotkeyViewModel, string glyph)
        {
            Hotkey = hotkey;
            HotkeyViewModel = hotkeyViewModel;
            Glyph = glyph;
        }
    }
}
