using Fastenshtein;
using HandheldCompanion.Devices;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.Managers
{
    public class LibraryManager : IManager
    {
        public enum LibraryType
        {
            cover,
            artwork,
            screenshot,
            directory
        }

        #region events
        public event EventHandler NetworkAvailabilityChanged;
        #endregion

        // Network
        public bool IsConnected => CheckInternetConnection();

        // IGDB
        private IGDBClient IGDBClient = new IGDBClient(SentryConfig.IGDB_CLIENT_ID, SentryConfig.IGDB_CLIENT_SECRET);

        public LibraryManager()
        {
            // initialize path
            ManagerPath = Path.Combine(App.SettingsPath, "library");

            // create path
            if (!Directory.Exists(ManagerPath))
                Directory.CreateDirectory(ManagerPath);
        }

        public string GetGameArtPath(long? gameId, LibraryType libraryType = LibraryType.directory)
        {
            // check if the game has art
            string imageId = libraryType switch
            {
                LibraryType.cover => "cover.png",
                LibraryType.artwork => "artwork.png",
                LibraryType.screenshot => "screenshot.png",
                _ => string.Empty,
            };

            return Path.Combine(ManagerPath, gameId.ToString(), imageId);
        }

        public ImageBrush GetGameArt(long gameId, LibraryType libraryType)
        {
            string fileName = GetGameArtPath(gameId, libraryType);
            if (!File.Exists(fileName))
                return null;

            return new ImageBrush(new BitmapImage(new Uri(fileName)))
            {
                Stretch = Stretch.UniformToFill,
                Opacity = 1.0,
                ImageSource = new BitmapImage(new Uri(fileName))
            };
        }

        public async Task<Game[]> GetGames(string name)
        {
            // check connection
            if (!IsConnected)
                return Array.Empty<Game>();

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
                Game[] games = await IGDBClient.QueryAsync<Game>(
                    IGDBClient.Endpoints.Games,
                    query: $"fields id,name,summary,storyline,cover.image_id,artworks.image_id,screenshots.image_id; search \"{searchQuery}\";");

                // If results were found, return them.
                if (games != null && games.Length > 0)
                    return games;
            }

            // If no results were found with any substring, return an empty array.
            return Array.Empty<Game>();
        }

        /// <summary>
        /// Returns the best matching game based on fuzzy comparison
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<Game> GetGame(string name)
        {
            // Retrieve games based on the original search logic.
            Game[] games = await GetGames(name);
            if (games == null || games.Length == 0)
                return null;

            // Clean the input name and convert to lowercase for case-insensitive comparison.
            string cleanedName = RemoveSpecialCharacters(name).ToLowerInvariant();

            Game bestGame = games.FirstOrDefault();
            int bestScore = 999;

            foreach (Game game in games)
            {
                // Clean and normalize the game name.
                string gameNameCleaned = RemoveSpecialCharacters(game.Name).ToLowerInvariant();

                // Compute a fuzzy match score between the game name and the input name.
                int score = Levenshtein.Distance(gameNameCleaned, cleanedName);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestGame = game;
                }
            }

            return bestGame;
        }

        public async Task<bool> DownloadGameArts(Game game)
        {
            // check connection
            if (!IsConnected)
                return false;

            // download cover
            if (game.Cover != null && !string.IsNullOrEmpty(game.Cover.Value.ImageId))
                await DownloadGameArt(game.Id, game.Cover.Value.ImageId, LibraryType.cover);

            // download artwork
            if (game.Artworks != null && game.Artworks.Values.Length > 0)
                await DownloadGameArt(game.Id, game.Artworks.Values[0].ImageId, LibraryType.artwork);

            // download screenshot
            if (game.Screenshots != null && game.Screenshots.Values.Length > 0)
                await DownloadGameArt(game.Id, game.Screenshots.Values[0].ImageId, LibraryType.screenshot);

            return true;
        }

        public async Task<bool> DownloadGameArt(long? gameId, string imageId, LibraryType libraryType)
        {
            // check connection
            if (!IsConnected)
                return false;

            string imageUrl = ImageHelper.GetImageUrl(imageId: imageId, size: libraryType == LibraryType.cover ? ImageSize.CoverBig : ImageSize.ScreenshotHuge, retina: true);
            if (string.IsNullOrEmpty(imageUrl))
                return false;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(imageUrl.Replace("//", "https://"));
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                        string directoryPath = GetGameArtPath(gameId);
                        string filePath = GetGameArtPath(gameId, libraryType);

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
