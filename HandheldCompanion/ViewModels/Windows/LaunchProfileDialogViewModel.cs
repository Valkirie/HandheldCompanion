using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public class LaunchProfileDialogViewModel : BaseViewModel
    {
        private static readonly Brush FallbackBackground = CreateFallbackBackground();

        private string _profileName = string.Empty;
        private string _profileDescription = string.Empty;
        private ImageSource? _logo;
        private Brush _backgroundBrush = FallbackBackground;

        public string ProfileName
        {
            get => _profileName;
            private set => SetProperty(ref _profileName, value, null, nameof(ProfileName));
        }

        public ImageSource? Logo
        {
            get => _logo;
            private set => SetProperty(ref _logo, value, () => OnPropertyChanged(nameof(HasLogo)), nameof(Logo));
        }

        public string ProfileDescription
        {
            get => _profileDescription;
            private set => SetProperty(ref _profileDescription, value, null, nameof(ProfileDescription));
        }

        public Brush BackgroundBrush
        {
            get => _backgroundBrush;
            private set => SetProperty(ref _backgroundBrush, value, null, nameof(BackgroundBrush));
        }

        public bool HasLogo => Logo is not null;

        public void Update(string profileName, string profileDescription, ImageSource? artwork, ImageSource? logo)
        {
            ProfileName = profileName;
            ProfileDescription = profileDescription;
            Logo = logo;
            BackgroundBrush = CreateBackgroundBrush(artwork);
        }

        private static Brush CreateBackgroundBrush(ImageSource? artwork)
        {
            if (artwork is null)
                return FallbackBackground;

            ImageBrush brush = new()
            {
                ImageSource = artwork,
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };

            if (brush.CanFreeze)
                brush.Freeze();

            return brush;
        }

        private static Brush CreateFallbackBackground()
        {
            SolidColorBrush brush = new(Color.FromArgb(0xCC, 0x14, 0x14, 0x14));

            if (brush.CanFreeze)
                brush.Freeze();

            return brush;
        }
    }
}
