using GameLib.Core;
using GameLib.Plugin.BattleNet.Model;
using GameLib.Plugin.EA.Model;
using GameLib.Plugin.Epic.Model;
using GameLib.Plugin.Gog.Model;
using GameLib.Plugin.Origin.Model;
using GameLib.Plugin.Rockstar.Model;
using GameLib.Plugin.Steam.Model;
using GameLib.Plugin.Ubisoft.Model;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Platforms.Discovery;
using HandheldCompanion.Shared;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public partial class LibraryPageViewModel
    {
        private static readonly (GamePlatform Platform, string Title)[] SupportedLaunchers =
        [
            (GamePlatform.BattleNet, Properties.Resources.Library_ScanBattleNet),
            (GamePlatform.EADesktop, Properties.Resources.Library_ScanEADesktop),
            (GamePlatform.Epic, Properties.Resources.Library_ScanEpic),
            (GamePlatform.GOG, Properties.Resources.Library_ScanGOG),
            (GamePlatform.MicrosoftStore, Properties.Resources.Library_ScanMicrosoftStore),
            (GamePlatform.Origin, Properties.Resources.Library_ScanOrigin),
            (GamePlatform.RiotGames, Properties.Resources.Library_ScanRiotGames),
            (GamePlatform.Rockstar, Properties.Resources.Library_ScanRockstar),
            (GamePlatform.Steam, Properties.Resources.Library_ScanSteam),
            (GamePlatform.UbisoftConnect, Properties.Resources.Library_ScanUbisoftConnect)
        ];

        private bool _isScanningLibrary;
        public bool IsScanningLibrary
        {
            get => _isScanningLibrary;
            private set => SetProperty(ref _isScanningLibrary, value);
        }

        private bool _isScanPreparing;
        public bool IsScanPreparing
        {
            get => _isScanPreparing;
            private set => SetProperty(ref _isScanPreparing, value);
        }

        private string _scanPlatformText = string.Empty;
        public string ScanPlatformText
        {
            get => _scanPlatformText;
            private set => SetProperty(ref _scanPlatformText, value);
        }

        private string _scanProgressText = string.Empty;
        public string ScanProgressText
        {
            get => _scanProgressText;
            private set => SetProperty(ref _scanProgressText, value);
        }

        private double _scanProgressValue;
        public double ScanProgressValue
        {
            get => _scanProgressValue;
            private set => SetProperty(ref _scanProgressValue, value);
        }

        private double _scanProgressMaximum;
        public double ScanProgressMaximum
        {
            get => _scanProgressMaximum;
            private set => SetProperty(ref _scanProgressMaximum, value);
        }

        private readonly Dictionary<Type, GamePlatform> keyValuePairs = new()
        {
            { typeof(BattleNetGame), GamePlatform.BattleNet },
            { typeof(EpicGame), GamePlatform.Epic },
            { typeof(GogGame), GamePlatform.GOG },
            { typeof(OriginGame), GamePlatform.Origin },
            { typeof(GameLib.Plugin.RiotGames.Model.Game), GamePlatform.RiotGames },
            { typeof(RockstarGame), GamePlatform.Rockstar },
            { typeof(SteamGame), GamePlatform.Steam },
            { typeof(UbisoftGame), GamePlatform.UbisoftConnect },
            { typeof(EAGame), GamePlatform.EADesktop },
            { typeof(global::HandheldCompanion.Platforms.Games.MicrosoftStoreGame), GamePlatform.MicrosoftStore },
        };

        public IReadOnlyList<EmulatorDefinition> SupportedEmulators { get; } = EmulatorDefinitions.All;
        public ObservableCollection<LibraryScanTarget> EmulatorScanTargets { get; } = [];

        private void AddEmulatorScanTarget(EmulatorDefinition definition)
        {
            EmulatorScanTargets.Add(new(definition.Id, definition.Name));
        }

        private void RemoveEmulatorScanTarget(EmulatorDefinition definition)
        {
            for (int index = 1; index < EmulatorScanTargets.Count; index++)
            {
                if (EmulatorScanTargets[index].Target == definition.Id)
                {
                    EmulatorScanTargets.RemoveAt(index);
                    return;
                }
            }
        }

        private void UpdateEmulatorScanTarget(EmulatorDefinition definition)
        {
            for (int index = 1; index < EmulatorScanTargets.Count; index++)
            {
                if (EmulatorScanTargets[index].Target == definition.Id)
                {
                    EmulatorScanTargets[index] = new(definition.Id, definition.Name);
                    return;
                }
            }
        }

        private ICommand CreateScanLibraryCommand()
        {
            return new DelegateCommand<object>(param => _ = ScanLibraryAsync(param));
        }

        private async Task ScanLibraryAsync(object? param)
        {
            if (IsScanningLibrary)
                return;

            string target = param?.ToString() ?? string.Empty;
            string targetName = GetScanTargetDisplayName(target);
            ContentDialogResult result = await new Dialog(MainWindow.GetCurrent())
            {
                Title = string.Format(Properties.Resources.LibraryScanTitle, targetName),
                Content = string.Format(Properties.Resources.LibraryScanContent, targetName),
                CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
            }.ShowAsync();

            if (result != ContentDialogResult.Primary)
                return;

            IsScanningLibrary = true;
            IsScanPreparing = true;
            ScanPlatformText = targetName;
            ScanProgressText = string.Format(Properties.Resources.Library_ScanDiscoveringGames, ScanPlatformText);
            ScanProgressValue = 0;
            ScanProgressMaximum = 0;

            try
            {
                await Task.Run(() => ScanGames(target));
                MarkLibraryChecked();
            }
            finally
            {
                IsScanPreparing = false;
                IsScanningLibrary = false;
            }
        }

        private void ScanGames(string target)
        {
            IEnumerable<(string Target, string Name)> scanTargets = GetScanTargets(target);

            foreach ((string scanTarget, string scanTargetName) in scanTargets)
            {
                _uiContext.Post(_ =>
                {
                    ScanPlatformText = scanTargetName;
                    ScanProgressText = string.Format(Properties.Resources.Library_ScanScanning, scanTargetName);
                }, null);

                List<IGame> games = PlatformManager.GetGamesForScanTarget(scanTarget).ToList();
                _uiContext.Post(_ =>
                {
                    IsScanPreparing = false;
                    ScanProgressMaximum = games.Count;
                    ScanProgressValue = 0;
                    ScanProgressText = games.Count == 0
                        ? Properties.Resources.Library_ScanNoGamesFound
                        : string.Format(Properties.Resources.Library_ScanProgress, 0, games.Count);
                }, null);

                for (int index = 0; index < games.Count; index++)
                {
                    ProcessGame(games[index]);
                    int completed = index + 1;
                    _uiContext.Post(_ =>
                    {
                        ScanPlatformText = scanTargetName;
                        ScanProgressValue = completed;
                        ScanProgressText = string.Format(Properties.Resources.Library_ScanProgress, completed, games.Count);
                    }, null);
                }
            }
        }

        private IEnumerable<(string Target, string Name)> GetScanTargets(string target)
        {
            if (string.Equals(target, "All", StringComparison.OrdinalIgnoreCase))
                return SupportedLaunchers.Select(platform => (platform.Platform.ToString(), platform.Title))
                    .Concat(EmulatorScanTargets.Skip(1).Select(target => (target.Target, target.Name)));

            if (string.Equals(target, "Launchers", StringComparison.OrdinalIgnoreCase))
                return SupportedLaunchers.Select(platform => (platform.Platform.ToString(), platform.Title));

            if (string.Equals(target, "Emulators", StringComparison.OrdinalIgnoreCase))
                return SupportedEmulators.Select(definition => (definition.Id, definition.Name));

            return [(target, GetScanTargetDisplayName(target))];
        }

        private void ProcessGame(IGame game)
        {
            Profile? profile = FindExistingProfile(game);

            bool isCreation = profile is null || profile.Default;
            if (isCreation)
                profile = new Profile(game.Executable);

            if (game is DiscoveredGame childGame && !childGame.IsEmulator)
            {
                Profile parentProfile = ManagerFactory.profileManager.GetProfileFromPath(childGame.Executable, true, true);
                if (!parentProfile.Default)
                {
                    profile.IsSubProfile = true;
                    profile.ParentGuid = parentProfile.Guid;
                }
            }

            IEnumerable<string> executables = game.Executables.Where(exe =>
                exe.IndexOf("redist", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("crash", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("updater", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("cheat", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("editor", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("tool", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("uninst", StringComparison.OrdinalIgnoreCase) < 0 &&
                exe.IndexOf("installer", StringComparison.OrdinalIgnoreCase) < 0);

            if (string.IsNullOrWhiteSpace(profile.Path) && !executables.Any())
            {
                LogManager.LogError("Skipping game '{0}' because it has no path and no executables.", game.Name);
                return;
            }

            if (string.IsNullOrEmpty(profile.Path) && executables.Any())
                profile.Path = executables.First();

            profile.Name = game.Name;

            if (game is DiscoveredGame emulatorGame)
                profile.Arguments = emulatorGame.Arguments;
            else if (game is GogGame gogGame)
            {
                string platformPath = PlatformManager.GOGGalaxy.ExecutablePath;
                profile.LaunchString = platformPath;
                profile.Arguments = $"/command=runGame /gameId={gogGame.Id} /path=\"{gogGame.InstallDir}\"";
            }
            else
                profile.LaunchString = game.LaunchString;

            profile.PlatformType = game is DiscoveredGame discovered ? discovered.PlatformType : keyValuePairs[game.GetType()];
            profile.Executables = executables.ToList();

            ManagerFactory.profileManager.UpdateOrCreateProfile(profile, isCreation ? UpdateSource.Creation : UpdateSource.LibraryUpdate);
        }

        private static Profile? FindExistingProfile(IGame game)
        {
            if (game is DiscoveredGame discoveredGame)
                return discoveredGame.IsEmulator
                    ? FindEmulatorProfile(discoveredGame)
                    : FindRomProfile(discoveredGame);

            IEnumerable<string> executables = game.Executables.Any() ? game.Executables : [game.Executable];
            return executables
                .Select(executable => ManagerFactory.profileManager.GetProfileFromPath(executable, true, true))
                .FirstOrDefault(profile => !profile.Default);
        }

        private static Profile? FindEmulatorProfile(DiscoveredGame game)
        {
            return ManagerFactory.profileManager.GetProfiles()
                .FirstOrDefault(profile => !profile.Default && !profile.IsSubProfile &&
                    ProfileContainsAnyPath(profile, game.Executables));
        }

        private static Profile? FindRomProfile(DiscoveredGame game)
        {
            Profile parentProfile = ManagerFactory.profileManager.GetProfileFromPath(game.Executable, true, true);
            return ManagerFactory.profileManager.GetSubProfilesFromProfile(parentProfile)
                .FirstOrDefault(profile => string.Equals(profile.Arguments, game.Arguments, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ProfileContainsAnyPath(Profile profile, IEnumerable<string> paths)
        {
            return paths.Any(path => ProfileContainsPath(profile, path));
        }

        private static bool ProfileContainsPath(Profile profile, string path)
        {
            return PathsEqual(profile.Path, path) ||
                profile.Executables.Any(executable => PathsEqual(executable, path));
        }

        private static string GetScanTargetDisplayName(string target)
        {
            if (string.Equals(target, "All", StringComparison.OrdinalIgnoreCase))
                return Properties.Resources.Library_ScanAllPlatformsAndEmulators;
            if (string.Equals(target, "Launchers", StringComparison.OrdinalIgnoreCase))
                return Properties.Resources.Library_ScanAllLaunchers;
            if (string.Equals(target, "Emulators", StringComparison.OrdinalIgnoreCase))
                return Properties.Resources.Library_ScanAllEmulators;
            return target;
        }

        private static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                return false;

            try
            {
                string firstFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
                string secondFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
                return string.Equals(firstFullPath, secondFullPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
