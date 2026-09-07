using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Windows;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public enum LibraryNavigationItemKind
    {
        AllGames,
        Platform,
        CollectionsRoot,
        Collection,
        TriggerGlyph
    }

    public class LibraryNavigationItemViewModel : BaseViewModel
    {
        public string Key { get; }
        private string _title;
        public string Title => _title;
        public LibraryNavigationItemKind Kind { get; }
        public GamePlatform Platform { get; }
        public Guid? CollectionId { get; }
        public string? IconGlyph { get; private set; }

        private int _gameCount;
        public int GameCount
        {
            get => _gameCount;
            set
            {
                if (SetProperty(ref _gameCount, value))
                    InfoBadge?.Value = value;
            }
        }

        public void RefreshTitle(string title)
        {
            if (SetProperty(ref _title, title))
                OnPropertyChanged(nameof(Title));
        }

        public InfoBadge? InfoBadge { get; }

        private object? _icon;
        public object? Icon
        {
            get => _icon;
            private set
            {
                if (SetProperty(ref _icon, value))
                    OnPropertyChanged(nameof(HasIcon));
            }
        }

        public bool HasIcon => Icon is not null;

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public LibraryNavigationItemViewModel(string key, string title, LibraryNavigationItemKind kind)
        {
            Key = key;
            _title = title;
            Kind = kind;
            InfoBadge = kind is not LibraryNavigationItemKind.CollectionsRoot and not LibraryNavigationItemKind.TriggerGlyph
                ? new InfoBadge
                {
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(6, 0, 0, 0),
                    Foreground = (Brush)Application.Current.FindResource("SystemControlForegroundBaseMediumBrush")
                }
                : null;

            IconGlyph = kind switch
            {
                LibraryNavigationItemKind.AllGames => "\uE80F",
                LibraryNavigationItemKind.CollectionsRoot => "\uE8B7",
                LibraryNavigationItemKind.Collection => "\uE734",
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(IconGlyph))
                Icon = new FontIcon() { Glyph = IconGlyph };
        }

        public LibraryNavigationItemViewModel(string key, string title, GamePlatform platform) : this(key, title, LibraryNavigationItemKind.Platform)
        {
            Platform = platform;
        }

        public LibraryNavigationItemViewModel(string key, string title, Guid collectionId) : this(key, title, LibraryNavigationItemKind.Collection)
        {
            CollectionId = collectionId;
        }

        /// <summary>Constructor for L2/R2 trigger glyph bookend items.</summary>
        public LibraryNavigationItemViewModel(string key, string defaultGlyph)
            : this(key, string.Empty, LibraryNavigationItemKind.TriggerGlyph)
        {
            _isEnabled = false;
            Icon = new FontIcon()
            {
                Glyph = defaultGlyph,
                FontFamily = new FontFamily("PromptFont"),
                FontSize = 22,
                Margin = new Thickness(-6)
            };
        }

        public void UpdateTriggerGlyph(string glyph)
        {
            if (Icon is FontIcon fi)
                fi.Glyph = glyph;
        }

        public void RefreshPlatformGlyph()
        {
            if (Kind != LibraryNavigationItemKind.Platform)
                return;

            IconGlyph = PlatformManager.GetPlatformGlyph(Platform);
            Icon = new FontIcon()
            {
                Glyph = IconGlyph,
                FontFamily = new FontFamily(PlatformManager.GetPlatformFont(Platform)),
                FontSize = PlatformManager.GetPlatformFontSize(Platform),
                Margin = new Thickness(-6)
            };
        }

        /// <summary>Crops fully-transparent rows/columns from all four sides of a 32bppArgb bitmap.</summary>
        private static System.Drawing.Bitmap CropTransparentPadding(System.Drawing.Bitmap src)
        {
            int w = src.Width, h = src.Height;
            int minX = w, minY = h, maxX = -1, maxY = -1;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (src.GetPixel(x, y).A > 10)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // Nothing opaque found — return a copy unchanged.
            if (maxX < 0)
                return new System.Drawing.Bitmap(src);

            var rect = new System.Drawing.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return src.Clone(rect, src.PixelFormat);
        }

        public override string ToString() => Title;
    }
}
