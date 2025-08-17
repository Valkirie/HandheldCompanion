using craftersmine.SteamGridDBNet;
using Fastenshtein;
using HandheldCompanion.Libraries;
using HandheldCompanion.Notifications;
using HandheldCompanion.Shared;
using IGDB;
using IGDB.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
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

        [Flags]
        public enum ErrorType
        {
            None = 0,
            NoResults = 1,
            Exception = 2,
        }

        #region events
        public event NetworkAvailabilityChangedEventHandler NetworkAvailabilityChanged;
        public delegate void NetworkAvailabilityChangedEventHandler(bool status);

        public delegate void ProfileStatusChangedEventHandler(Profile profile, ManagerStatus status);
        public event ProfileStatusChangedEventHandler ProfileStatusChanged;
        #endregion

        // Network
        public bool IsConnected = false;

        // IGDB
        private IGDBClient IGDBClient;
        private SteamGridDb steamGridDb;

        public bool HasIGDBClient => IGDBClient != null;
        public bool HasSteamGridDb => steamGridDb != null;

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

                    // check IGDB
                    if (libraryFamily.HasFlag(LibraryFamily.IGDB))
                    {
                        if (!HasIGDBClient)
                            return entries.Values;

                        // Query IGDB using the search query.
                        Game[] games = await IGDBClient.QueryAsync<Game>(IGDBClient.Endpoints.Games,
                            query: $"fields id,name,summary,storyline,category,cover.image_id,cover.url,artworks.image_id,artworks.url,screenshots.image_id,screenshots.url,first_release_date; search \"{searchQuery}\";");

                        await Parallel.ForEachAsync(games, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (game, cancellationToken) =>
                        {
                            long gameId = (long)game.Id;

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

                            lock (entries)
                                entries[gameId] = entry;
                        });
                    }

                    if (libraryFamily.HasFlag(LibraryFamily.SteamGrid))
                    {
                        if (!HasSteamGridDb)
                            return entries.Values;

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
                            grids = grids.OrderByDescending(grid => grid.Upvotes).ToArray();

                            SteamGridDbHero[]? heroes = await steamGridDb.GetHeroesByGameIdAsync(
                                gameId: game.Id,
                                types: SteamGridDbTypes.Static,
                                styles: SteamGridDbStyles.Alternate | SteamGridDbStyles.None | SteamGridDbStyles.Material,
                                dimensions: SteamGridDbDimensions.W1920H620,
                                formats: SteamGridDbFormats.Png | SteamGridDbFormats.Jpeg);

                            heroes = heroes.Where(game => !game.IsLocked).ToArray();
                            heroes = heroes.OrderByDescending(hero => hero.Upvotes).ToArray();

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
                    else
                    {
                        // update status
                        AddStatus(ManagerStatus.Failed, ErrorType.Exception, response.ReasonPhrase);
                        LogManager.LogError("Failed to download image: {0}", response.ReasonPhrase);
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

            return await DownloadGameArt(entry.Id,
                libraryType.HasFlag(LibraryType.cover) ? entry.Cover : entry.Artworks[index],
                libraryType, false);
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
                        AddStatus(ManagerStatus.Failed, ErrorType.Exception, response.ReasonPhrase);
                        LogManager.LogError("Failed to download image: {0}", response.ReasonPhrase);
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

        public void RefreshProfilesArts()
        {
            Parallel.ForEachAsync(ManagerFactory.profileManager.GetProfiles(true), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (profile, cancellationToken) =>
            {
                RefreshProfileArts(profile);
            });
        }

        public async void RefreshProfileArts(Profile profile, UpdateSource source = UpdateSource.LibraryUpdate)
        {
            // skip if profile is default
            if (profile.Default)
                return;

            LibraryEntry entry;
            if (profile.LibraryEntry is SteamGridEntry || profile.LibraryEntry is IGDBEntry)
            {
                entry = profile.LibraryEntry;
            }
            else
            {
                // update status
                ProfileStatusChanged?.Invoke(profile, ManagerStatus.Busy);

                IEnumerable<LibraryEntry> entries = await ManagerFactory.libraryManager.GetGames(LibraryFamily.SteamGrid, profile.Name);
                entry = ManagerFactory.libraryManager.GetGame(entries, profile.Name);

                // update status
                ProfileStatusChanged?.Invoke(profile, ManagerStatus.None);
            }

            // failed to retrieve a library entry
            if (entry is null)
                return;

            // download arts
            await UpdateProfileArts(profile, entry);

            // update profile
            ManagerFactory.profileManager.UpdateOrCreateProfile(profile, UpdateSource.LibraryUpdate);
        }

        public async Task UpdateProfileArts(Profile profile, LibraryEntry entry, int coverIndex = 0, int artworkIndex = 0)
        {
            // update status
            ProfileStatusChanged?.Invoke(profile, ManagerStatus.Busy);

            // update library entry
            if (entry is SteamGridEntry Steam)
            {
                if (Steam.Grid is null || coverIndex != 0)
                {
                    if (Steam.Grids?.Length > coverIndex)
                        Steam.Grid = Steam.Grids[coverIndex];
                }

                if (Steam.Hero is null || artworkIndex != 0)
                {
                    if (Steam.Heroes?.Length > artworkIndex)
                        Steam.Hero = Steam.Heroes[artworkIndex];
                }
            }
            else if (entry is IGDBEntry IGDB)
            {
                if (IGDB.Artwork is null)
                {
                    if (IGDB.Artworks?.Count > artworkIndex)
                        IGDB.Artwork = IGDB.Artworks[artworkIndex];
                }
            }

            // update target entry and name
            profile.LibraryEntry = entry;
            profile.Name = entry.Name;

            // download arts
            await ManagerFactory.libraryManager.DownloadGameArts(entry, false);

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
