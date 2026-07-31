using craftersmine.SteamGridDBNet;
using Fastenshtein;
using HandheldCompanion.Libraries;
using HandheldCompanion.Notifications;
using HandheldCompanion.Shared;
using IGDB;
using IGDB.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;

namespace HandheldCompanion.Managers
{
    public static class LibraryResources
    {
        // GameArt
        public static BitmapImage MissingCover = new BitmapImage(new Uri("pack://application:,,,/Resources/MissingCover.png"));
        public static BitmapImage MissingArtwork = new BitmapImage(new Uri("pack://application:,,,/Resources/MissingArtwork.png"));

        // Controllers
        public static BitmapImage Xbox360Big = new BitmapImage(new Uri("pack://application:,,,/Resources/Controllers/controller_0_big.png"));
        public static BitmapImage DualShock4Big = new BitmapImage(new Uri("pack://application:,,,/Resources/Controllers/controller_1_big.png"));
        public static BitmapImage DualSenseBig = new BitmapImage(new Uri("pack://application:,,,/Resources/Controllers/controller_2_big.png"));
        public static BitmapImage SteamDeckBig = new BitmapImage(new Uri("pack://application:,,,/Resources/Controllers/controller_3_big.png"));
        public static BitmapImage SwitchProBig = new BitmapImage(new Uri("pack://application:,,,/Resources/Controllers/controller_4_big.png"));
        public static BitmapImage SteamControllerBig = new BitmapImage(new Uri("pack://application:,,,/Resources/Controllers/controller_6_big.png"));
    }

    public class LibraryManager : IManager
    {
        [Flags]
        public enum LibraryType
        {
            cover = 1,
            artwork = 2,
            thumbnails = 4,
            logo = 8,
        }

        [Flags]
        public enum ErrorType
        {
            None = 0,
            NoResults = 1,
            Exception = 2,
        }

        #region events
        public event NetworkAvailabilityChangedEventHandler? NetworkAvailabilityChanged;
        public delegate void NetworkAvailabilityChangedEventHandler(bool status);

        public delegate void ProfileStatusChangedEventHandler(Profile profile, ManagerStatus status);
        public event ProfileStatusChangedEventHandler? ProfileStatusChanged;
        #endregion

        // Network
        public bool IsConnected = false;

        // IGDB
        private IGDBClient? IGDBClient;
        private SteamGridDb? steamGridDb;

        private readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _imageCache = new();

        public bool HasIGDBClient => IGDBClient is not null;
        public bool HasSteamGridDb => steamGridDb is not null;

        public LibraryManager()
        {
            // initialize path
            ManagerPath = Path.Combine(App.SettingsPath, "cache", "library");

            // create path
            if (!Directory.Exists(ManagerPath))
                Directory.CreateDirectory(ManagerPath);

            try { IGDBClient = new IGDBClient(SecretKeys.IGDB_CLIENT_ID, SecretKeys.IGDB_CLIENT_SECRET); } catch { }
            try { steamGridDb = new SteamGridDb(SecretKeys.STEAMGRID_CLIENT_SECRET); } catch { }
        }

        public string GetGameArtPath(long gameId, LibraryType libraryType, string imageId, string extension)
        {
            // check if the game has art
            string filePath = string.Empty;
            if (libraryType.HasFlag(LibraryType.thumbnails))
                filePath = "thumbnails";

            return Path.Combine(ManagerPath, gameId.ToString(), filePath, $"{imageId}{extension}");
        }

        public string GetGameArtPath(long gameId, LibraryType libraryType, long imageId, string extension)
        {
            return GetGameArtPath(gameId, libraryType, imageId.ToString(), extension);
        }

        public BitmapImage? GetGameArt(long gameId, LibraryType libraryType, string imageId, string extension)
        {
            string fileName = GetGameArtPath(gameId, libraryType, imageId, extension);
            if (!File.Exists(fileName))
            {
                if (libraryType.HasFlag(LibraryType.cover))
                    return LibraryResources.MissingCover;
                else if (libraryType.HasFlag(LibraryType.artwork))
                    return LibraryResources.MissingArtwork;
                else if (libraryType.HasFlag(LibraryType.thumbnails))
                    return LibraryResources.MissingCover;
                else if (libraryType.HasFlag(LibraryType.logo))
                    return null;
            }

            if (_imageCache.TryGetValue(fileName, out WeakReference<BitmapImage>? cachedReference) &&
                cachedReference.TryGetTarget(out BitmapImage? cachedImage))
                return cachedImage;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.UriSource = new Uri(fileName);
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            _imageCache[fileName] = new WeakReference<BitmapImage>(bitmapImage);
            return bitmapImage;
        }

        public BitmapImage? GetGameArt(long gameId, LibraryType libraryType, long imageId, string extension)
        {
            return GetGameArt(gameId, libraryType, imageId.ToString(), extension);
        }

        public async Task<IEnumerable<LibraryEntry>> GetGames(LibraryFamily libraryFamily, string name)
        {
            // prepare list
            ConcurrentDictionary<long, LibraryEntry> entries = new();

            // check connection
            if (!IsConnected)
                return entries.Values;

            // update status
            AddStatus(ManagerStatus.Busy);

            try
            {
                // Clean the input name and convert to lowercase for case-insensitive comparison.
                string cleanedName = RemoveSpecialCharacters(name);

                // Split the game name on space characters into words
                string[] words = cleanedName.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Try using as many words as possible, then one less, then one less, etc.
                for (int i = words.Length; i > 0; i--)
                {
                    // Join the first i words to form the search query.
                    string searchQuery = string.Join(" ", words.Take(i));

                    // check IGDB
                    if (libraryFamily.HasFlag(LibraryFamily.IGDB))
                    {
                        IGDBClient? igdbClient = IGDBClient;
                        if (igdbClient is null)
                            return entries.Values;

                        // Query IGDB using the search query.
                        Game[] games = await igdbClient.QueryAsync<Game>(IGDBClient.Endpoints.Games,
                            query: $"fields id,name,summary,storyline,category,cover.image_id,cover.url,artworks.image_id,artworks.url,screenshots.image_id,screenshots.url,first_release_date; search \"{searchQuery}\";");

                        await Parallel.ForEachAsync(games, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (game, cancellationToken) =>
                        {
                            if (!game.Id.HasValue)
                                return;

                            long gameId = (long)game.Id.Value;

                            IGDBEntry entry = new IGDBEntry(gameId, game.Name, game.FirstReleaseDate.HasValue ? game.FirstReleaseDate.Value.DateTime : new())
                            {
                                Description = game.Summary,
                                Storyline = game.Storyline,
                                Cover = game.Cover?.Value ?? null,
                            };

                            if (game.Artworks is not null)
                                entry.Artworks.AddRange(game.Artworks.Values);

                            if (game.Screenshots is not null)
                            {
                                // cast screenshots
                                IEnumerable<Artwork> screenshots = game.Screenshots.Values.Select(screenshot => new Artwork
                                {
                                    Id = screenshot.Id,
                                    ImageId = screenshot.ImageId,
                                    Url = screenshot.Url,
                                    Width = screenshot.Width,
                                    Height = screenshot.Height,
                                    Checksum = screenshot.Checksum,
                                    AlphaChannel = screenshot.AlphaChannel,
                                    Animated = screenshot.Animated,
                                    Game = screenshot.Game
                                });

                                // add to array
                                entry.Artworks.AddRange(screenshots);

                                // set default artwork
                                entry.Artwork = entry.Artworks.FirstOrDefault();
                            }

                            entries[gameId] = entry;
                        });
                    }

                    if (libraryFamily.HasFlag(LibraryFamily.SteamGrid))
                    {
                        SteamGridDb? steamGridClient = steamGridDb;
                        if (steamGridClient is null)
                            return entries.Values;

                        // Query IGDB using the search query.
                        SteamGridDbGame[]? games = await steamGridClient.SearchForGamesAsync(cleanedName);

                        await Parallel.ForEachAsync(games, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (game, cancellationToken) =>
                        {
                            long gameId = game.Id;

                            SteamGridDbGrid[]? grids = await steamGridClient.GetGridsByGameIdAsync(
                                gameId: game.Id,
                                types: SteamGridDbTypes.Static,
                                styles: SteamGridDbStyles.AllGrids,
                                dimensions: SteamGridDbDimensions.AllGrids,
                                formats: SteamGridDbFormats.Png | SteamGridDbFormats.Jpeg);
                            grids = grids
                                .Where(g => !g.IsLocked)
                                .OrderByDescending(g => g.Style == SteamGridDbStyles.Official)
                                .ThenByDescending(g => g.Upvotes)
                                .ToArray();

                            SteamGridDbHero[]? heroes = await steamGridClient.GetHeroesByGameIdAsync(
                                gameId: game.Id,
                                types: SteamGridDbTypes.Static,
                                styles: SteamGridDbStyles.AllHeroes,
                                dimensions: SteamGridDbDimensions.AllHeroes,
                                formats: SteamGridDbFormats.Png | SteamGridDbFormats.Jpeg);
                            heroes = heroes
                                .Where(h => !h.IsLocked)
                                .OrderByDescending(h => h.Style == SteamGridDbStyles.Official)
                                .ThenByDescending(h => h.Upvotes)
                                .ToArray();

                            SteamGridDbLogo[]? logos = await steamGridClient.GetLogosByGameIdAsync(
                                gameId: game.Id,
                                types: SteamGridDbTypes.Static,
                                styles: SteamGridDbStyles.AllLogos,
                                formats: SteamGridDbFormats.Png);
                            logos = logos
                                .Where(l => !l.IsLocked)
                                .OrderByDescending(l => l.Style == SteamGridDbStyles.Official)
                                .ThenByDescending(l => l.Upvotes)
                                .ToArray();

                            // Skip if no visuals are available
                            if (grids.Length == 0 && heroes.Length == 0)
                                return;

                            SteamGridEntry entry = new SteamGridEntry(gameId, game.Name, game.ReleaseDate)
                            {
                                Heroes = heroes,
                                Grids = grids,
                                Logos = logos,
                                Hero = heroes.FirstOrDefault(),
                                Grid = grids.FirstOrDefault(),
                                Logo = logos.FirstOrDefault(),
                            };

                            entries[gameId] = entry;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // update status
                AddStatus(ManagerStatus.Failed, ErrorType.Exception, ex.Message);
                LogManager.LogError(ex.Message);
            }
            finally
            {
                // update status
                if (entries.Count == 0)
                    AddStatus(ManagerStatus.Failed, ErrorType.NoResults);

                RemoveStatus(ManagerStatus.Busy);
            }

            // If no results were found with any substring, return an empty array.
            return entries.Values;
        }

        /// <summary>
        /// Returns the best matching game based on fuzzy comparison
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<LibraryEntry?> GetGame(LibraryFamily libraryFamily, string name)
        {
            // Retrieve games based on the original search logic.
            IEnumerable<LibraryEntry> games = await GetGames(libraryFamily, name);
            return GetGame(games, name);
        }

        public LibraryEntry? GetGame(IEnumerable<LibraryEntry> entries, string name)
        {
            if (entries == null || entries.Count() == 0)
                return null;

            // Clean the input name and convert to lowercase for case-insensitive comparison.
            string cleanedName = RemoveSpecialCharacters(name).ToLowerInvariant();

            LibraryEntry? bestEntry = entries.FirstOrDefault();
            int bestScore = 999;

            foreach (LibraryEntry game in entries)
            {
                // clean and normalize the game name.
                string gameNameCleaned = RemoveSpecialCharacters(game.Name).ToLowerInvariant();

                // compute a fuzzy match score between the game name and the input name.
                int score = Levenshtein.Distance(gameNameCleaned, cleanedName);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestEntry = game;
                }
            }

            return bestEntry;
        }

        // Maximum thumbnail dimensions per art type (width × height)
        private static readonly (int W, int H) ThumbSizeCover = (267, 400);
        private static readonly (int W, int H) ThumbSizeArtwork = (850, 274);
        private static readonly (int W, int H) ThumbSizeLogo = (500, 486);

        private static (int W, int H) GetThumbSize(LibraryType libraryType)
        {
            if (libraryType.HasFlag(LibraryType.cover)) return ThumbSizeCover;
            if (libraryType.HasFlag(LibraryType.artwork)) return ThumbSizeArtwork;
            if (libraryType.HasFlag(LibraryType.logo)) return ThumbSizeLogo;
            return ThumbSizeCover;
        }

        /// <summary>
        /// Copies a manually-selected image file into the library cache for the given profile/entry.
        /// The full-res slot is a verbatim copy; the thumbnails sub-folder receives a PNG resized to
        /// fit within the art-type maximum dimensions (never upscaled).
        /// Returns the cache destination path of the full-res file on success, or null on failure.
        /// </summary>
        public string? CopyManualArt(long gameId, LibraryType libraryType, long imageId, string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return null;

            string extension = Path.GetExtension(sourcePath);

            // Invalidate ALL cached bitmaps for this entry so stale images are not served
            string entryRoot = Path.Combine(ManagerPath, gameId.ToString());
            foreach (string key in _imageCache.Keys.Where(k => k.StartsWith(entryRoot)).ToList())
                _imageCache.TryRemove(key, out _);

            // Full-res slot — verbatim copy (preserve original format)
            string destPath = WriteSingle(sourcePath, GetGameArtPath(gameId, libraryType, imageId, extension));

            // Thumbnail slot — resized PNG written under the thumbnails sub-folder
            (int maxW, int maxH) = GetThumbSize(libraryType);
            string thumbPath = GetGameArtPath(gameId, libraryType | LibraryType.thumbnails, imageId, ".png");
            WriteResizedThumbnail(sourcePath, thumbPath, maxW, maxH);

            return destPath;
        }

        /// <summary>Writes a verbatim file copy, removing any stale file with the same stem but different extension.</summary>
        private static string WriteSingle(string sourcePath, string destPath)
        {
            EnsureDir(destPath);
            RemoveStaleFiles(destPath);
            File.Copy(sourcePath, destPath, overwrite: true);
            return destPath;
        }

        /// <summary>
        /// Decodes <paramref name="sourcePath"/>, scales it down to fit within
        /// (<paramref name="maxW"/>, <paramref name="maxH"/>) without upscaling, then saves as PNG.
        /// </summary>
        private static void WriteResizedThumbnail(string sourcePath, string destPath, int maxW, int maxH)
        {
            EnsureDir(destPath);
            RemoveStaleFiles(destPath);

            BitmapImage src;
            try
            {
                src = new();
                src.BeginInit();
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.UriSource = new Uri(sourcePath);
                src.EndInit();
                src.Freeze();
            }
            catch
            {
                return;
            }

            int srcW = src.PixelWidth;
            int srcH = src.PixelHeight;
            if (srcW == 0 || srcH == 0)
                return;

            // Scale-to-fit, never upscale
            double scale = Math.Min(1.0, Math.Min((double)maxW / srcW, (double)maxH / srcH));
            int dstW = Math.Max(1, (int)Math.Round(srcW * scale));
            int dstH = Math.Max(1, (int)Math.Round(srcH * scale));

            DrawingVisual dv = new();
            using (DrawingContext dc = dv.RenderOpen())
                dc.DrawImage(src, new Rect(0, 0, dstW, dstH));

            RenderTargetBitmap rtb = new(dstW, dstH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            PngBitmapEncoder enc = new();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using FileStream fs = File.Create(destPath);
            enc.Save(fs);
        }

        private static void EnsureDir(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static void RemoveStaleFiles(string destPath)
        {
            string stem = Path.GetFileNameWithoutExtension(destPath);
            string folder = Path.GetDirectoryName(destPath) ?? string.Empty;
            if (!Directory.Exists(folder))
                return;
            foreach (string old in Directory.GetFiles(folder, $"{stem}.*"))
                try { File.Delete(old); } catch { }
        }

        public async Task<bool> DownloadGameArts(LibraryEntry entry, bool preview, bool includeThumbnails = false)
        {
            // manual arts are handled via CopyManualArt, nothing to download
            if (entry is ManualEntry)
                return true;

            // check connection
            if (!IsConnected)
                return false;

            if (entry is SteamGridEntry steamGridEntry)
                return await DownloadGameArts(steamGridEntry, preview, includeThumbnails);
            else if (entry is IGDBEntry igdbEntry)
                return await DownloadGameArts(igdbEntry, preview, includeThumbnails);

            return true;
        }

        public async Task<bool> DownloadGameArts(SteamGridEntry entry, bool preview, bool includeThumbnails = false)
        {
            // check connection
            if (!IsConnected)
                return false;

            if (preview)
            {
                // download grid
                foreach (SteamGridDbGrid grid in entry.Grids)
                    await DownloadGameArt(entry.Id, grid, LibraryType.cover | LibraryType.thumbnails);
                // download hero
                foreach (SteamGridDbHero hero in entry.Heroes)
                    await DownloadGameArt(entry.Id, hero, LibraryType.artwork | LibraryType.thumbnails);
                // download logo
                foreach (SteamGridDbLogo logo in entry.Logos)
                    await DownloadGameArt(entry.Id, logo, LibraryType.logo | LibraryType.thumbnails);
            }
            else
            {
                // download grid
                if (entry.Grid != null)
                    await DownloadGameArt(entry.Id, entry.Grid, LibraryType.cover);
                // download hero
                if (entry.Hero != null)
                    await DownloadGameArt(entry.Id, entry.Hero, LibraryType.artwork);
                // download logo
                if (entry.Logo != null)
                    await DownloadGameArt(entry.Id, entry.Logo, LibraryType.logo);

                if (includeThumbnails)
                {
                    // download thumbnail variants for selected arts
                    if (entry.Grid != null)
                        await DownloadGameArt(entry.Id, entry.Grid, LibraryType.cover | LibraryType.thumbnails);
                    if (entry.Hero != null)
                        await DownloadGameArt(entry.Id, entry.Hero, LibraryType.artwork | LibraryType.thumbnails);
                    if (entry.Logo != null)
                        await DownloadGameArt(entry.Id, entry.Logo, LibraryType.logo | LibraryType.thumbnails);
                }
            }

            return true;
        }

        public async Task<bool> DownloadGameArt(LibraryEntry entry, int index, LibraryType libraryType)
        {
            if ((libraryType.HasFlag(LibraryType.cover) && !string.IsNullOrEmpty(entry.ManualCoverPath)) ||
                (libraryType.HasFlag(LibraryType.artwork) && !string.IsNullOrEmpty(entry.ManualArtworkPath)) ||
                (libraryType.HasFlag(LibraryType.logo) && !string.IsNullOrEmpty(entry.ManualLogoPath)))
                return true;

            if (entry is SteamGridEntry steamEntry)
                return await DownloadGameArt(steamEntry, index, libraryType);
            else if (entry is IGDBEntry igdbEntry)
                return await DownloadGameArt(igdbEntry, index, libraryType);

            return false;
        }

        public async Task<bool> DownloadGameArt(SteamGridEntry entry, int index, LibraryType libraryType)
        {
            SteamGridDbObject[] objects = libraryType.HasFlag(LibraryType.cover) ? entry.Grids : libraryType.HasFlag(LibraryType.artwork) ? entry.Heroes : entry.Logos;
            if (index < 0 || index >= objects.Length)
                return false;

            return await DownloadGameArt(entry.Id, libraryType.HasFlag(LibraryType.cover) ? entry.Grids[index] : libraryType.HasFlag(LibraryType.artwork) ? entry.Heroes[index] : entry.Logos[index], libraryType);
        }

        public async Task<bool> DownloadGameArt(long gameId, SteamGridDbObject entry, LibraryType libraryType)
        {
            try
            {
                string imageUrl = libraryType.HasFlag(LibraryType.thumbnails) ? entry.ThumbnailImageUrl : entry.FullImageUrl;
                if (string.IsNullOrEmpty(imageUrl))
                    return false;

                string fileExtension = Path.GetExtension(imageUrl);
                string filePath = GetGameArtPath(gameId, libraryType, entry.Id, fileExtension);
                string? directoryPath = Directory.GetParent(filePath)?.FullName;

                // check if file exists firts
                if (File.Exists(filePath))
                    return true;

                // update status
                AddStatus(ManagerStatus.Busy);

                // If the image directory does not exist, create it
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(imageUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                        using (Stream file = File.Create(filePath))
                        {
                            file.Write(imageBytes, 0, imageBytes.Length);
                            return true;
                        }
                    }
                    else
                    {
                        // update status
                        string reasonPhrase = response.ReasonPhrase ?? string.Empty;
                        AddStatus(ManagerStatus.Failed, ErrorType.Exception, reasonPhrase);
                        LogManager.LogError("Failed to download image: {0}", reasonPhrase);
                    }
                }
            }
            catch { }
            finally
            {
                // update status
                RemoveStatus(ManagerStatus.Busy);
            }

            return false;
        }

        public async Task<bool> DownloadGameArts(IGDBEntry entry, bool preview, bool includeThumbnails = false)
        {
            // check connection
            if (!IsConnected)
                return false;

            if (preview)
            {
                // download cover
                if (entry.Cover != null)
                    await DownloadGameArt(entry.Id, entry.Cover, LibraryType.cover | LibraryType.thumbnails, preview);
                // download artworks
                foreach (Artwork artwork in entry.Artworks)
                    await DownloadGameArt(entry.Id, artwork, LibraryType.artwork | LibraryType.thumbnails, preview);
            }
            else
            {
                // download cover
                if (entry.Cover != null)
                    await DownloadGameArt(entry.Id, entry.Cover, LibraryType.cover, preview);
                // download artwork
                if (entry.Artwork != null)
                    await DownloadGameArt(entry.Id, entry.Artwork, LibraryType.artwork, preview);

                if (includeThumbnails)
                {
                    // download thumbnail variants for selected arts
                    if (entry.Cover != null)
                        await DownloadGameArt(entry.Id, entry.Cover, LibraryType.cover | LibraryType.thumbnails, preview);
                    if (entry.Artwork != null)
                        await DownloadGameArt(entry.Id, entry.Artwork, LibraryType.artwork | LibraryType.thumbnails, preview);
                }
            }

            return true;
        }

        public async Task<bool> DownloadGameArt(IGDBEntry entry, int index, LibraryType libraryType)
        {
            if (libraryType.HasFlag(LibraryType.artwork) && index < 0 || index >= entry.Artworks.Count)
                return false;

            /*
            return await DownloadGameArt(entry.Id,
                libraryType.HasFlag(LibraryType.cover) ? entry.Cover.ImageId : entry.Artworks[index].ImageId,
                libraryType.HasFlag(LibraryType.cover) ? entry.Cover.Id.Value : entry.Artworks[index].Id.Value,
                libraryType, false);
            */

            if (libraryType.HasFlag(LibraryType.cover))
            {
                if (entry.Cover is null)
                    return false;

                return await DownloadGameArt(entry.Id, entry.Cover, libraryType, false);
            }

            return await DownloadGameArt(entry.Id, entry.Artworks[index], libraryType, false);
        }

        /* long gameId, string imageName, long? imageId, LibraryType libraryType, bool preview */
        public async Task<bool> DownloadGameArt(long gameId, IIdentifier identifier, LibraryType libraryType, bool preview)
        {
            // check connection
            if (!IsConnected)
                return false;

            string imageName = string.Empty;
            long? imageId = null;

            if (identifier is Cover cover)
            {
                imageName = cover.ImageId;
                imageId = cover.Id;
            }
            else if (identifier is Artwork artwork)
            {
                imageName = artwork.ImageId;
                imageId = artwork.Id;
            }

            try
            {
                ImageSize imageSize = ImageSize.CoverBig;

                if (libraryType.HasFlag(LibraryType.cover))
                    imageSize = libraryType.HasFlag(LibraryType.thumbnails) ? ImageSize.CoverSmall : ImageSize.CoverBig;
                else if (libraryType.HasFlag(LibraryType.artwork))
                    imageSize = libraryType.HasFlag(LibraryType.thumbnails) ? ImageSize.ScreenshotMed : ImageSize.ScreenshotHuge;

                string imageUrl = ImageHelper.GetImageUrl(imageId: imageName, size: imageSize, retina: !preview).Replace("//", "https://");
                if (string.IsNullOrEmpty(imageUrl))
                    return false;

                string fileExtension = Path.GetExtension(imageUrl);
                string filePath = GetGameArtPath(gameId, libraryType, imageId.HasValue ? imageId.Value : 0, fileExtension);
                string directoryPath = Directory.GetParent(filePath)?.FullName ?? string.Empty;

                // check if file exists firts
                if (File.Exists(filePath))
                    return true;

                // If the image directory does not exist, create it
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                // update status
                AddStatus(ManagerStatus.Busy);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(imageUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                        using (Stream file = File.Create(filePath))
                        {
                            file.Write(imageBytes, 0, imageBytes.Length);
                            return true;
                        }
                    }
                    else
                    {
                        // update status
                        string reasonPhrase = response.ReasonPhrase ?? string.Empty;
                        AddStatus(ManagerStatus.Failed, ErrorType.Exception, reasonPhrase);
                        LogManager.LogError("Failed to download image: {0}", reasonPhrase);
                    }
                }
            }
            catch { }
            finally
            {
                // update status
                RemoveStatus(ManagerStatus.Busy);
            }

            return false;
        }

        public static string RemoveSpecialCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Define a set of allowed characters (letters, digits, '.', '_', and space)
            var allowedCharacters = new HashSet<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._ ");
            var sanitizedString = new StringBuilder(input.Length);

            // Iterate over each character in the input string
            foreach (char character in input)
                if (allowedCharacters.Contains(character))
                    sanitizedString.Append(character);

            // Return the sanitized string
            return sanitizedString.ToString();
        }

        public override void Start()
        {
            if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
                return;

            base.PrepareStart();

            // manage events
            NetworkChange.NetworkAddressChanged += (sender, e) => NetworkChange_NetworkAddressChanged(false);
            NetworkChange.NetworkAvailabilityChanged += (sender, e) => NetworkChange_NetworkAddressChanged(false);

            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfile();
                    break;
            }

            // raise events
            NetworkChange_NetworkAddressChanged(true);

            base.Start();
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void QueryProfile()
        {
            // do something
        }

        public async Task RefreshProfilesArts()
        {
            await Parallel.ForEachAsync(ManagerFactory.profileManager.GetProfiles(true), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (profile, cancellationToken) =>
            {
                await RefreshProfileArtsAsync(profile, UpdateSource.LibraryUpdate, includeFullResAssets: true);
            });
        }

        public async void RefreshProfileArts(Profile profile, UpdateSource source = UpdateSource.LibraryUpdate, bool includeFullResAssets = false)
        {
            await RefreshProfileArtsAsync(profile, source, includeFullResAssets);
        }

        private async Task RefreshProfileArtsAsync(Profile profile, UpdateSource source, bool includeFullResAssets)
        {
            // skip if profile is default
            if (profile.Default)
                return;

            // update status
            ProfileStatusChanged?.Invoke(profile, ManagerStatus.Busy);

            // update variables
            LibraryEntry? entry = profile.LibraryEntry ?? null;
            long entryId = entry?.Id ?? 0;
            long coverId = entry?.GetCoverId() ?? 0;
            long artworkId = entry?.GetArtworkId() ?? 0;
            long logoId = entry?.GetLogoId() ?? 0;

            // retrieve library entry
            IEnumerable<LibraryEntry> entries = await ManagerFactory.libraryManager.GetGames(LibraryFamily.SteamGrid, profile.Name);

            if (entryId == 0)
            {
                // pick most relevant entry
                entry = ManagerFactory.libraryManager.GetGame(entries, profile.Name);

                // update variables
                coverId = entry?.GetCoverId() ?? 0;
                artworkId = entry?.GetArtworkId() ?? 0;
                logoId = entry?.GetLogoId() ?? 0;
            }
            else
            {
                // update entry
                entry = entries.FirstOrDefault(e => e.Id == entryId);
            }

            // update status
            ProfileStatusChanged?.Invoke(profile, ManagerStatus.None);

            // failed to retrieve a library entry
            if (entry is null)
                return;

            // download arts
            await UpdateProfileArts(profile, entry, (int)coverId, (int)artworkId, (int)logoId, includeFullResAssets);

            // update profile (always use LibraryUpdate to avoid re-entering creation logic)
            ManagerFactory.profileManager.UpdateOrCreateProfile(profile, UpdateSource.LibraryUpdate);
        }

        public async Task UpdateProfileArts(Profile profile, LibraryEntry entry, int coverId = 0, int artworkId = 0, int logoId = 0, bool includeFullResAssets = true)
        {
            // update status
            ProfileStatusChanged?.Invoke(profile, ManagerStatus.Busy);

            if (entry is ManualEntry manualEntry)
            {
                // Files were already copied to cache when the user browsed them;
                // nothing more to do here except assign the entry to the profile.
                profile.LibraryEntry = manualEntry;
                ProfileStatusChanged?.Invoke(profile, ManagerStatus.None);
                return;
            }

            // update library entry
            if (entry is SteamGridEntry Steam)
            {
                if (coverId > 0)
                    Steam.ManualCoverPath = string.Empty;
                if (artworkId > 0)
                    Steam.ManualArtworkPath = string.Empty;
                if (logoId > 0)
                    Steam.ManualLogoPath = string.Empty;

                if (coverId > 0 || (Steam.Grid is null && string.IsNullOrEmpty(Steam.ManualCoverPath)))
                    Steam.Grid = Steam.Grids.FirstOrDefault(g => g.Id == coverId);

                if (artworkId > 0 || (Steam.Hero is null && string.IsNullOrEmpty(Steam.ManualArtworkPath)))
                    Steam.Hero = Steam.Heroes.FirstOrDefault(h => h.Id == artworkId);

                if (logoId > 0 || (Steam.Logo is null && string.IsNullOrEmpty(Steam.ManualLogoPath)))
                    Steam.Logo = Steam.Logos.FirstOrDefault(l => l.Id == logoId);
            }
            else if (entry is IGDBEntry IGDB)
            {
                if (artworkId > 0)
                    IGDB.ManualArtworkPath = string.Empty;

                if (artworkId > 0 || (IGDB.Artwork is null && string.IsNullOrEmpty(IGDB.ManualArtworkPath)))
                    IGDB.Artwork = IGDB.Artworks.FirstOrDefault(a => a.Id == artworkId);
            }

            // update target entry and name
            profile.LibraryEntry = entry;
            profile.Name = entry.Name;

            if (includeFullResAssets)
            {
                // download arts (including thumbnails for library card display)
                await ManagerFactory.libraryManager.DownloadGameArts(entry, false, includeThumbnails: true);
            }
            else if (entry is SteamGridEntry steamGridEntry)
            {
                if (steamGridEntry.Grid != null)
                    await DownloadGameArt(entry.Id, steamGridEntry.Grid, LibraryType.cover | LibraryType.thumbnails);

                if (steamGridEntry.Hero != null)
                    await DownloadGameArt(entry.Id, steamGridEntry.Hero, LibraryType.artwork | LibraryType.thumbnails);

                if (steamGridEntry.Logo != null)
                    await DownloadGameArt(entry.Id, steamGridEntry.Logo, LibraryType.logo | LibraryType.thumbnails);
            }
            else if (entry is IGDBEntry igdbEntry)
            {
                if (igdbEntry.Cover != null)
                    await DownloadGameArt(entry.Id, igdbEntry.Cover, LibraryType.cover | LibraryType.thumbnails, false);

                if (igdbEntry.Artwork != null)
                    await DownloadGameArt(entry.Id, igdbEntry.Artwork, LibraryType.artwork | LibraryType.thumbnails, false);
            }

            // update status
            ProfileStatusChanged?.Invoke(profile, ManagerStatus.None);
        }

        public override void Stop()
        {
            if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
                return;

            base.PrepareStop();

            // manage events
            NetworkChange.NetworkAddressChanged -= (sender, e) => NetworkChange_NetworkAddressChanged(false);
            NetworkChange.NetworkAvailabilityChanged -= (sender, e) => NetworkChange_NetworkAddressChanged(false);

            base.Stop();
        }

        private bool CheckInternetConnection()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = ping.Send("google.fr", 3000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Notification Notification_IsBusy = new("Library Manager", "Downloading artworks and metadatas.") { IsInternal = true, IsIndeterminate = false };
        private static Notification Notification_Failed = new("Library Manager", "Unknown error.") { IsInternal = true, IsIndeterminate = true };

        protected override void AddStatus(ManagerStatus status, params object[] args)
        {
            // send internal notification(s)
            switch (status)
            {
                case ManagerStatus.Busy:
                    ManagerFactory.notificationManager.Add(Notification_IsBusy);
                    break;
                case ManagerStatus.Failed:
                    {
                        ErrorType errorType = ErrorType.None;
                        if (args.Length != 0 && args[0] is ErrorType argsError)
                            errorType = argsError;

                        switch (errorType)
                        {
                            case ErrorType.None:
                                Notification_Failed.Message = "Unknown error.";
                                break;
                            case ErrorType.NoResults:
                                Notification_Failed.Message = "No artworks found.";
                                break;
                            case ErrorType.Exception:
                                {
                                    if (args.Length != 0 && args[0] is string messageError)
                                        Notification_Failed.Message = string.Format("Exception raised: {0}", messageError);
                                    else
                                        Notification_Failed.Message = "Unknown exception.";
                                }
                                break;
                        }
                        ManagerFactory.notificationManager.Add(Notification_Failed);
                    }
                    break;
            }

            base.AddStatus(status);
        }

        protected override void RemoveStatus(ManagerStatus status, params object[] args)
        {
            switch (status)
            {
                case ManagerStatus.Busy:
                    ManagerFactory.notificationManager.Discard(Notification_IsBusy);
                    break;
            }

            base.RemoveStatus(status, args);
        }

        private static Notification Notification_ConnectivityDown = new("Library Manager", "Oops, we're offline! We will let you know when we are back.") { IsInternal = true, IsIndeterminate = true };
        private static Notification Notification_ConnectivityUp = new("Library Manager", "We are back online. All features are available.") { IsInternal = true, IsIndeterminate = true };

        private void NetworkChange_NetworkAddressChanged(bool startup)
        {
            // wait a short time to allow network stabilization
            if (!startup)
                Thread.Sleep(4000);

            // check connection
            IsConnected = CheckInternetConnection();
            NetworkAvailabilityChanged?.Invoke(IsConnected);

            // send internal notification
            if (!startup)
                ManagerFactory.notificationManager.Add(IsConnected ? Notification_ConnectivityUp : Notification_ConnectivityDown);
        }
    }
}
