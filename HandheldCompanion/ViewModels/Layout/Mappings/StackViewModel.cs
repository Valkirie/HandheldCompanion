using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Views;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public abstract class StackViewModel : BaseViewModel
    {
        private string _name = string.Empty;
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

        private string _glyph = string.Empty;
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

        public ICommand? ButtonCommand { get; protected set; }

        public StackViewModel(object value)
        {
            // manage events
            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;
            ControllerManager.Initialized += ControllerManager_Initialized;

            // raise events
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();
        }

        private void ControllerManager_Initialized()
        {
            // raise events
            // which physical input is being processed
            ControllerManager.ControllerSelected += UpdateController;

            // raise events
            if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
                UpdateController(controller);
        }

        protected virtual void UpdateMapping(Layout layout)
        { }

        protected void UpdateIcon(GlyphIconInfo glyphIconInfo)
        {
            if (glyphIconInfo is null)
                return;

            Name = glyphIconInfo.Name!;
            Glyph = glyphIconInfo.Glyph!;
            GlyphFontFamily = glyphIconInfo.FontFamily;
            GlyphFontSize = glyphIconInfo.FontSize;

            if (glyphIconInfo.Color.HasValue)
            {
                var brush = new SolidColorBrush(glyphIconInfo.Color.Value);
                brush.Freeze();
                GlyphForeground = brush;
            }
            else
            {
                GlyphForeground = null;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
                ControllerManager.Initialized -= ControllerManager_Initialized;
                ControllerManager.ControllerSelected -= UpdateController;
            }

            base.Dispose(disposing);
        }
    }
}
