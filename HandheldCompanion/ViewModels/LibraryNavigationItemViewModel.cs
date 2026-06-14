using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        public string Title { get; }
        public LibraryNavigationItemKind Kind { get; }
        public GamePlatform Platform { get; }
        public Guid? CollectionId { get; }
        public string? IconGlyph { get; }
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
            Title = title;
            Kind = kind;
            IconGlyph = kind switch
            {
                LibraryNavigationItemKind.AllGames => "\uE80F",
                LibraryNavigationItemKind.CollectionsRoot => "\uE8B7",
                LibraryNavigationItemKind.Collection => "\uE734",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(IconGlyph))
                Icon = new FontIcon() { Glyph = IconGlyph };
        }

        public LibraryNavigationItemViewModel(string key, string title, GamePlatform platform)
            : this(key, title, LibraryNavigationItemKind.Platform)
        {
            Platform = platform;
            Icon = new FontIcon() { Glyph = "\uF712" };
        }

        // Cached frozen ImageSource per platform type — computed once across all instances.
        private static readonly System.Collections.Generic.Dictionary<GamePlatform, ImageSource?> _logoCache = new();

        /// <summary>
        /// (Re-)loads the platform logo. Safe to call after PlatformManager has started.
        /// </summary>
        public async void RefreshIcon()
        {
            if (Kind != LibraryNavigationItemKind.Platform)
                return;

            // Check cache first on the UI thread to avoid any Task overhead when already resolved.
            if (_logoCache.TryGetValue(Platform, out ImageSource? cached))
            {
                if (cached is not null)
                    Icon = new System.Windows.Controls.Image { Source = cached, Width = 16, Height = 16, Stretch = Stretch.Uniform };
                return;
            }

            // Run all heavy work (GDI draw, pixel-loop crop, PNG encode, BitmapImage decode) off the UI thread.
            // The default await continuation resumes on the captured UI SynchronizationContext.
            ImageSource? source = await Task.Run(() => GetPlatformLogoSource(Platform));

            // Only cache successful conversions; avoid caching null so subsequent calls after
            // PlatformManager initializes can retry fetching the logo.
            if (source is not null)
            {
                _logoCache[Platform] = source;
                Icon = new System.Windows.Controls.Image { Source = source, Width = 16, Height = 16, Stretch = Stretch.Uniform };
            }
        }

        /// <summary>
        /// Clears the platform logo cache. Call this when platforms (re)initialize to allow fresh logo lookups.
        /// </summary>
        public static void ClearLogoCache()
        {
            _logoCache.Clear();
        }

        private static ImageSource? GetPlatformLogoSource(GamePlatform platform)
        {
            System.Drawing.Image? drawingImage = platform switch
            {
                GamePlatform.Steam => PlatformManager.Steam?.GetLogo(),
                GamePlatform.Origin => PlatformManager.Origin?.GetLogo(),
                GamePlatform.EADesktop => PlatformManager.EADesktop?.GetLogo(),
                GamePlatform.UbisoftConnect => PlatformManager.UbisoftConnect?.GetLogo(),
                GamePlatform.GOG => PlatformManager.GOGGalaxy?.GetLogo(),
                GamePlatform.BattleNet => PlatformManager.BattleNet?.GetLogo(),
                GamePlatform.Epic => PlatformManager.Epic?.GetLogo(),
                GamePlatform.RiotGames => PlatformManager.RiotGames?.GetLogo(),
                GamePlatform.Rockstar => PlatformManager.Rockstar?.GetLogo(),
                GamePlatform.MicrosoftStore => PlatformManager.MicrosoftStore?.GetLogo(),
                _ => null
            };

            if (drawingImage == null) return null;

            try
            {
                // Save as PNG via MemoryStream to preserve alpha transparency.
                // Wrap in a 32bppArgb Bitmap first so non-Bitmap Image types also work.
                using var bmp = new System.Drawing.Bitmap(drawingImage.Width, drawingImage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    g.DrawImage(drawingImage, 0, 0, drawingImage.Width, drawingImage.Height);

                // Crop transparent/white padding so the logo fills the nav icon slot.
                using var cropped = CropTransparentPadding(bmp);

                using var ms = new MemoryStream();
                cropped.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public LibraryNavigationItemViewModel(string key, string title, Guid collectionId)
            : this(key, title, LibraryNavigationItemKind.Collection)
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
