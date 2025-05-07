using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public abstract class StackViewModel : BaseViewModel
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (value != Name)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private string _glyph;
        public string Glyph
        {
            get => _glyph;
            set
            {
                if (value != Glyph)
                {
                    _glyph = value;
                    OnPropertyChanged(nameof(Glyph));
                }
            }
        }

        private FontFamily? _glyphFontFamily;
        public FontFamily? GlyphFontFamily
        {
            get => _glyphFontFamily;
            set
            {
                if (value != GlyphFontFamily)
                {
                    _glyphFontFamily = value;
                    OnPropertyChanged(nameof(GlyphFontFamily));
                }
            }
        }

        private double _glyphFontSize = 14;
        public double GlyphFontSize
        {
            get => _glyphFontSize;
            set
            {
                if (value != GlyphFontSize)
                {
                    _glyphFontSize = value;
                    OnPropertyChanged(nameof(GlyphFontSize));
                }
            }
        }

        private Brush? _glyphForeground;
        public Brush? GlyphForeground
        {
            get => _glyphForeground;
            set
            {
                if (value != GlyphForeground)
                {
                    _glyphForeground = value;
                    OnPropertyChanged(nameof(GlyphForeground));
                }
            }
        }

        public int ActionNumber = 0;

        public abstract void AddMapping();
        public abstract void RemoveMapping(MappingViewModel mapping);
        protected abstract void UpdateController(IController controller);

        public ICommand ButtonCommand { get; protected set; }

        public StackViewModel(object value)
        {
            // manage events
            ControllerManager.ControllerSelected += UpdateController;

            // send events
            if (ControllerManager.HasTargetController)
                UpdateController(ControllerManager.GetTarget());
        }

        protected void UpdateIcon(GlyphIconInfo glyphIconInfo)
        {
            if (glyphIconInfo is null)
                return;

            Name = glyphIconInfo.Name!;
            Glyph = glyphIconInfo.Glyph!;
            GlyphFontFamily = glyphIconInfo.FontFamily;
            GlyphFontSize = glyphIconInfo.FontSize;

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                GlyphForeground = glyphIconInfo.Color.HasValue ? new SolidColorBrush(glyphIconInfo.Color.Value) : null;
            });
        }

        public override void Dispose()
        {
            ControllerManager.ControllerSelected -= UpdateController;

            base.Dispose();
        }
    }
}
