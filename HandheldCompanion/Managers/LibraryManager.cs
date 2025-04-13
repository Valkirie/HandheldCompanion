using craftersmine.SteamGridDBNet;
using Fastenshtein;
using HandheldCompanion.Devices;
using HandheldCompanion.Libraries;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.Pages;
using IGDB;
using IGDB.Models;
using Sentry.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;
using static HandheldCompanion.Managers.LibraryManager;

namespace HandheldCompanion.Managers
{
    public static class LibraryResources
    {
        // GameArt
        public static BitmapImage MissingCover = new BitmapImage(new Uri("pack://application:,,,/Resources/IGDB/MissingCover.png"));
    }

    public class LibraryManager : IManager
    {
        public enum LibraryType
        {
            cover,
            artwork,
            thumbnails,
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

        public string GetGameArtPath(long gameId, LibraryType libraryType, long imageId)
        {            
            // check if the game has art
            string filePath = libraryType switch
            {
                LibraryType.thumbnails => "thumbnails",
                _ => string.Empty,
            };

            return Path.Combine(ManagerPath, gameId.ToString(), filePath, $"{imageId}.png");
        }

        public BitmapImage GetGameArt(long gameId, LibraryType libraryType, long imageId)
        {
            string fileName = GetGameArtPath(gameId, libraryType, imageId);
            if (!File.Exists(fileName))
                return LibraryResources.MissingCover;

            return new BitmapImage(new Uri(fileName));
        }

        public async Task<IEnumerable<SteamGridDbGame>> GetGamesSteam(string name)
        {
            // check connection
            if (!IsConnected)
                return Array.Empty<SteamGridDbGame>();

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

                    // Query IGDB using the search query.
                    SteamGridDbGame[]? games = await steamGridDb.SearchForGamesAsync(name);

                    // If results were found, return them.
                    if (games != null && games.Length > 0)
                        return games.OrderBy(g => g.Name);
                }
            }
            catch { }
            finally
            {
                // update status
                RemoveStatus(ManagerStatus.Busy);
            }

            // If no results were found with any substring, return an empty array.
            return Array.Empty<SteamGridDbGame>();
        }

        public async Task<List<LibraryEntry>> GetGames(LibraryFamily libraryFamily, string name)
        {
            // prepare list
            List<LibraryEntry> entries = new();

            // check connection
            if (!IsConnected)
                return entries;

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

                    switch(libraryFamily)
                    {
                        case LibraryFamily.IGDB:
                            {
                                // Query IGDB using the search query.
                                Game[] games = await IGDBClient.QueryAsync<Game>(IGDBClient.Endpoints.Games,
                                    query: $"fields id,name,summary,storyline,category,cover.image_id,artworks.image_id,screenshots.image_id,first_release_date; search \"{searchQuery}\";");

                                foreach (Game game in games)
                                {
                                    entries.Add(new IGDBEntry((long)game.Id, game.Name, game.FirstReleaseDate.Value.DateTime)
                                    {
                                        Summary = game.Summary,
                                        Storyline = game.Storyline,
                                        Category = game.Category.HasValue ? game.Category.Value : Category.MainGame,
                                        Cover = game.Cover.Value,

                                        Artworks = game.Artworks.Values,
                                        Artwork = game.Artworks.Values?[0],

                                        Screenshots = game.Screenshots.Values,
                                        Screenshot = game.Screenshots.Values?[0],
                                    });
                                }
                            }
                            break;

                        case LibraryFamily.SteamGrid:
                            {
                                // Query IGDB using the search query.
                                SteamGridDbGame[]? games = await steamGridDb.SearchForGamesAsync(name);

                                foreach (SteamGridDbGame game in games)
                                {
                                    SteamGridDbGrid[]? grids = await steamGridDb.GetGridsByGameIdAsync(
                                        gameId: game.Id,
                                        types: SteamGridDbTypes.Static,
                                        styles: SteamGridDbStyles.Alternate | SteamGridDbStyles.None | SteamGridDbStyles.Material,
                                        dimensions: SteamGridDbDimensions.W600H900,
                                        formats: SteamGridDbFormats.Png,
                                        limit: 4);
                                    
                                    SteamGridDbHero[]? heroes = await steamGridDb.GetHeroesByGameIdAsync(
                                        gameId: game.Id,
                                        types: SteamGridDbTypes.Static,
                                        styles: SteamGridDbStyles.Alternate | SteamGridDbStyles.None | SteamGridDbStyles.Material,
                                        dimensions: SteamGridDbDimensions.W1920H620,
                                        formats: SteamGridDbFormats.Png,
                                        limit: 4);

                                    entries.Add(new SteamGridEntry((long)game.Id, game.Name, game.ReleaseDate)
                                    {
                                        Heroes = heroes,
                                        Grids = grids,

                                        Hero = heroes.FirstOrDefault(),
                                        Grid = grids.FirstOrDefault(),
                                    });
                                }
                            }
                            break;
                    }                    
                    
                    return entries.OrderBy(g => g.Name).ToList();
                }
            }
            catch { }
            finally
            {
                // update status
                RemoveStatus(ManagerStatus.Busy);
            }

            // If no results were found with any substring, return an empty array.
            return entries;
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
                foreach(SteamGridDbGrid grid in entry.Grids)
                    await DownloadGameArt(entry.Id, grid, LibraryType.thumbnails);
                // download hero
                foreach(SteamGridDbHero hero in entry.Heroes)
                    await DownloadGameArt(entry.Id, hero, LibraryType.thumbnails);
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

        public async Task<bool> DownloadGameArt(long gameId, SteamGridDbObject entry, LibraryType libraryType)
        {
            try
            {
                // update status
                AddStatus(ManagerStatus.Busy);

                using (HttpClient client = new HttpClient())
                {
                    string imageUrl = libraryType == LibraryType.thumbnails ? entry.ThumbnailImageUrl : entry.FullImageUrl;

                    HttpResponseMessage response = await client.GetAsync(imageUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                        string filePath = GetGameArtPath(gameId, libraryType, entry.Id);
                        string directoryPath = Directory.GetParent(filePath).FullName;

                        // If the image directory does not exist, create it
                        if (!Directory.Exists(directoryPath))
                            Directory.CreateDirectory(directoryPath);

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

            // download cover
            if (entry.Cover != null && !string.IsNullOrEmpty(entry.Cover.ImageId))
                await DownloadGameArt(entry.Id, entry.Cover.ImageId, LibraryType.cover, preview);

            // download artwork
            if (entry.Artwork != null)
                await DownloadGameArt(entry.Id, entry.Artwork.ImageId, LibraryType.artwork, preview);

            // download screenshot
            if (entry.Screenshot != null)
                await DownloadGameArt(entry.Id, entry.Screenshot.ImageId, LibraryType.artwork, preview);

            return true;
        }

        public async Task<bool> DownloadGameArt(long gameId, string imageId, LibraryType libraryType, bool preview)
        {
            // check connection
            if (!IsConnected)
                return false;

            ImageSize imageSize = ImageSize.CoverBig;
            switch(libraryType)
            {
                case LibraryType.cover:
                    imageSize = preview ? ImageSize.CoverSmall : ImageSize.CoverBig;
                    break;
                case LibraryType.artwork:
                    imageSize = preview ? ImageSize.ScreenshotMed : ImageSize.ScreenshotHuge;
                    break;
            }

            string imageUrl = ImageHelper.GetImageUrl(imageId: imageId, size: imageSize, retina: !preview);
            if (string.IsNullOrEmpty(imageUrl))
                return false;

            try
            {
                // update status
                AddStatus(ManagerStatus.Busy);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(imageUrl.Replace("//", "https://"));
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                        string filePath = GetGameArtPath(gameId, libraryType, long.Parse(imageId));
                        string directoryPath = Directory.GetParent(filePath).FullName;

                        // If the image directory does not exist, create it
                        if (!Directory.Exists(directoryPath))
                            Directory.CreateDirectory(directoryPath);

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
