using craftersmine.SteamGridDBNet;
using Fastenshtein;
using HandheldCompanion.Libraries;
using IGDB;
using IGDB.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;

namespace HandheldCompanion.Managers
{
    public static class LibraryResources
    {
        // GameArt
        public static BitmapImage MissingCover = new BitmapImage(new Uri("pack://application:,,,/Resources/MissingCover.png"));
        public static BitmapImage Xbox360Big = new BitmapImage(new Uri("pack://application:,,,/Resources/controller_0_big.png"));
        public static BitmapImage DualShock4Big = new BitmapImage(new Uri("pack://application:,,,/Resources/controller_1_big.png"));
    }

    public class LibraryManager : IManager
    {
        [Flags]
        public enum LibraryType
        {
            cover = 1,
            artwork = 2,
            thumbnails = 4,
        }

        #region events
        public event EventHandler NetworkAvailabilityChanged;
        #endregion

        // Network
        public bool IsConnected => CheckInternetConnection();

        // IGDB
        private IGDBClient IGDBClient = new IGDBClient(SecretKeys.IGDB_CLIENT_ID, SecretKeys.IGDB_CLIENT_SECRET);
        private SteamGridDb steamGridDb = new SteamGridDb(SecretKeys.STEAMGRID_CLIENT_SECRET);

        public LibraryManager()
        {
            // initialize path
            ManagerPath = Path.Combine(App.SettingsPath, "cache", "library");

            // create path
            if (!Directory.Exists(ManagerPath))
                Directory.CreateDirectory(ManagerPath);
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

        public BitmapImage GetGameArt(long gameId, LibraryType libraryType, string imageId, string extension)
        {
            string fileName = GetGameArtPath(gameId, libraryType, imageId, extension);
            if (!File.Exists(fileName))
                return libraryType.HasFlag(LibraryType.cover) ? LibraryResources.MissingCover : LibraryResources.MissingCover;

            return new BitmapImage(new Uri(fileName));
        }

        public BitmapImage GetGameArt(long gameId, LibraryType libraryType, long imageId, string extension)
        {
            return GetGameArt(gameId, libraryType, imageId.ToString(), extension);
        }

        public async Task<IEnumerable<LibraryEntry>> GetGames(LibraryFamily libraryFamily, string name)
        {
            // prepare list
            Dictionary<long, LibraryEntry> entries = new();

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

                    switch (libraryFamily)
                    {
                        case LibraryFamily.IGDB:
                            {
                                // Query IGDB using the search query.
                                Game[] games = await IGDBClient.QueryAsync<Game>(IGDBClient.Endpoints.Games,
                                    query: $"fields id,name,summary,storyline,category,cover.image_id,artworks.image_id,screenshots.image_id,first_release_date; search \"{searchQuery}\";");

                                await Parallel.ForEachAsync(games, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (game, cancellationToken) =>
                                {
                                    long gameId = (long)game.Id;

                                    IGDBEntry entry = new IGDBEntry(gameId, game.Name, game.FirstReleaseDate.Value.DateTime)
                                    {
                                        Description = game.Summary,
                                        Storyline = game.Storyline,
                                        Category = game.Category.Value,
                                        Cover = game.Cover.Value,
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

                                    lock (entries)
                                        entries[gameId] = entry;
                                });
                            }
                            break;

                        case LibraryFamily.SteamGrid:
                            {
                                // Query IGDB using the search query.
                                SteamGridDbGame[]? games = await steamGridDb.SearchForGamesAsync(name);

                                await Parallel.ForEachAsync(games, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (game, cancellationToken) =>
                                {
                                    long gameId = (long)game.Id;

                                    SteamGridDbGrid[]? grids = await steamGridDb.GetGridsByGameIdAsync(
                                        gameId: game.Id,
                                        types: SteamGridDbTypes.Static,
                                        styles: SteamGridDbStyles.Alternate | SteamGridDbStyles.None | SteamGridDbStyles.Material,
                                        dimensions: SteamGridDbDimensions.W600H900,
                                        formats: SteamGridDbFormats.Png | SteamGridDbFormats.Jpeg);

                                    grids = grids.Where(game => !game.IsLocked).ToArray();

                                    SteamGridDbHero[]? heroes = await steamGridDb.GetHeroesByGameIdAsync(
                                        gameId: game.Id,
                                        types: SteamGridDbTypes.Static,
                                        styles: SteamGridDbStyles.Alternate | SteamGridDbStyles.None | SteamGridDbStyles.Material,
                                        dimensions: SteamGridDbDimensions.W1920H620,
                                        formats: SteamGridDbFormats.Png | SteamGridDbFormats.Jpeg);

                                    heroes = heroes.Where(game => !game.IsLocked).ToArray();

                                    // Skip if no visuals are available
                                    if (grids.Length == 0 && heroes.Length == 0)
                                        return;

                                    SteamGridEntry entry = new SteamGridEntry(gameId, game.Name, game.ReleaseDate)
                                    {
                                        Heroes = heroes,
                                        Grids = grids,
                                        Hero = heroes.FirstOrDefault(),
                                        Grid = grids.FirstOrDefault(),
                                    };

                                    lock (entries)
                                        entries[gameId] = entry;
                                });
                            }
                            break;
                    }
                }
            }
            catch { }
            finally
            {
                // update status
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
        public async Task<LibraryEntry> GetGame(LibraryFamily libraryFamily, string name)
        {
            // Retrieve games based on the original search logic.
            IEnumerable<LibraryEntry> games = await GetGames(libraryFamily, name);
            return GetGame(games, name);
        }

        public LibraryEntry GetGame(IEnumerable<LibraryEntry> entries, string name)
        {
            if (entries == null || entries.Count() == 0)
                return null;

            // Clean the input name and convert to lowercase for case-insensitive comparison.
            string cleanedName = RemoveSpecialCharacters(name).ToLowerInvariant();

            LibraryEntry bestEntry = entries.FirstOrDefault();
            int bestScore = 999;

            foreach (LibraryEntry game in entries)
            {
                // Clean and normalize the game name.
                string gameNameCleaned = RemoveSpecialCharacters(game.Name).ToLowerInvariant();

                // Compute a fuzzy match score between the game name and the input name.
                int score = Levenshtein.Distance(gameNameCleaned, cleanedName);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestEntry = game;
                }
            }

            return bestEntry;
        }

        public async Task<bool> DownloadGameArts(LibraryEntry entry, bool preview)
        {
            // check connection
            if (!IsConnected)
                return false;

            if (entry is SteamGridEntry steamGridEntry)
                return await DownloadGameArts(steamGridEntry, preview);
            else if (entry is IGDBEntry igdbEntry)
                return await DownloadGameArts(igdbEntry, preview);

            return true;
        }

        public async Task<bool> DownloadGameArts(SteamGridEntry entry, bool preview)
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
            }
            else
            {
                // download grid
                if (entry.Grid != null)
                    await DownloadGameArt(entry.Id, entry.Grid, LibraryType.cover);
                // download hero
                if (entry.Hero != null)
                    await DownloadGameArt(entry.Id, entry.Hero, LibraryType.artwork);
            }

            return true;
        }

        public async Task<bool> DownloadGameArt(LibraryEntry entry, int index, LibraryType libraryType)
        {
            if (entry is SteamGridEntry steamEntry)
                return await DownloadGameArt(steamEntry, index, libraryType);
            else if (entry is IGDBEntry igdbEntry)
                return await DownloadGameArt(igdbEntry, index, libraryType);

            return false;
        }

        public async Task<bool> DownloadGameArt(SteamGridEntry entry, int index, LibraryType libraryType)
        {
            SteamGridDbObject[] objects = libraryType.HasFlag(LibraryType.cover) ? entry.Grids : entry.Heroes;
            if (index < 0 || index >= objects.Length)
                return false;

            return await DownloadGameArt(entry.Id,
                libraryType.HasFlag(LibraryType.cover) ? entry.Grids[index] : entry.Heroes[index],
                libraryType);
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
                string directoryPath = Directory.GetParent(filePath).FullName;

                // check if file exists firts
                if (File.Exists(filePath))
                    return true;

                // update status
                AddStatus(ManagerStatus.Busy);

                // If the image directory does not exist, create it
                if (!Directory.Exists(directoryPath))
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

        public async Task<bool> DownloadGameArts(IGDBEntry entry, bool preview)
        {
            // check connection
            if (!IsConnected)
                return false;

            if (preview)
            {
                // download cover
                if (entry.Cover != null)
                    await DownloadGameArt(entry.Id, entry.Cover.ImageId, entry.Cover.Id.Value, LibraryType.cover | LibraryType.thumbnails, preview);
                // download artworks
                foreach (Artwork artwork in entry.Artworks)
                    await DownloadGameArt(entry.Id, artwork.ImageId, artwork.Id.Value, LibraryType.artwork | LibraryType.thumbnails, preview);
            }
            else
            {
                // download cover
                if (entry.Cover != null)
                    await DownloadGameArt(entry.Id, entry.Cover.ImageId, entry.Cover.Id.Value, LibraryType.cover, preview);
                // download artwork
                if (entry.Artwork != null)
                    await DownloadGameArt(entry.Id, entry.Artwork.ImageId, entry.Artwork.Id.Value, LibraryType.artwork, preview);
            }

            return true;
        }

        public async Task<bool> DownloadGameArt(IGDBEntry entry, int index, LibraryType libraryType)
        {
            if (libraryType.HasFlag(LibraryType.artwork) && index < 0 || index >= entry.Artworks.Count)
                return false;

            return await DownloadGameArt(entry.Id,
                libraryType.HasFlag(LibraryType.cover) ? entry.Cover.ImageId : entry.Artworks[index].ImageId,
                libraryType.HasFlag(LibraryType.cover) ? entry.Cover.Id.Value : entry.Artworks[index].Id.Value,
                libraryType, false);
        }

        public async Task<bool> DownloadGameArt(long gameId, string imageName, long? imageId, LibraryType libraryType, bool preview)
        {
            // check connection
            if (!IsConnected)
                return false;

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
                string filePath = GetGameArtPath(gameId, libraryType, (long)imageId, fileExtension);
                string directoryPath = Directory.GetParent(filePath).FullName;

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
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            // raise events
            OnNetworkAvailabilityChanged(null, null);

            base.Start();
        }

        public override void Stop()
        {
            if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
                return;

            base.PrepareStop();

            // manage events
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

            base.Stop();
        }

        private bool CheckInternetConnection()
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    // Using Google's public DNS server as an example.
                    PingReply reply = ping.Send("8.8.8.8", 3000); // Timeout in ms.
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private async void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            // Optional: wait a short time to allow network stabilization.
            await Task.Delay(2000);

            // check connection
            CheckInternetConnection();

            NetworkAvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
