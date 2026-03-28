using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static HandheldCompanion.Managers.LibraryManager;

namespace HandheldCompanion.ViewModels
{
    public class ProfileViewModel : BaseViewModel
    {
        public ICommand StartProcessCommand { get; private set; }
        public ICommand ToggleProcessCommand { get; private set; }
        public ICommand ToggleFavoriteCommand { get; private set; }
        public ICommand Navigate { get; private set; }
        public ICommand OpenLayout { get; private set; }

        public readonly bool IsQuickTools;
        public bool IsMainPage => !IsQuickTools;
        private readonly bool deferVisualLoading;
        private bool areVisualsVisible = true;
        private CancellationTokenSource? visualsLoadCancellationTokenSource;

        private Profile _Profile;
        public Profile Profile
        {
            get => _Profile;
            set
            {
                // Profile objects are mutable and re-assigned by reference after external mutation
                // (e.g. toggling IsLiked via gamepad). Reference equality would always be true in
                // that case, so we must always notify to keep bindings (IsLiked, templates, live
                // sort) and RefreshImages in sync.
                _Profile = value;

                // refresh all properties
                OnPropertyChanged(string.Empty);
                OnPropertyChanged(nameof(Name));

                RefreshImages();
            }
        }

        private void ApplyPlaceholderImages()
        {
            Cover = LibraryResources.MissingCover;
            Artwork = LibraryResources.MissingArtwork;
            Logo = null;
        }

        private void RefreshImages()
        {
            CancelPendingVisualLoad();

            if (deferVisualLoading && !areVisualsVisible)
            {
                ApplyPlaceholderImages();
                return;
            }

            if (_Profile.LibraryEntry is null)
            {
                ApplyPlaceholderImages();
                return;
            }

            long id = _Profile.LibraryEntry.Id;
            long coverId = _Profile.LibraryEntry.GetCoverId();
            string coverExtension = _Profile.LibraryEntry.GetCoverExtension(false);
            long artworkId = _Profile.LibraryEntry.GetArtworkId();
            string artworkExtension = _Profile.LibraryEntry.GetArtworkExtension(false);
            long logoId = _Profile.LibraryEntry.GetLogoId();
            string logoExtension = _Profile.LibraryEntry.GetLogoExtension(false);

            CancellationTokenSource cancellationTokenSource = new();
            visualsLoadCancellationTokenSource = cancellationTokenSource;

            _ = LoadImagesAsync(id, coverId, coverExtension, artworkId, artworkExtension, logoId, logoExtension, cancellationTokenSource);
        }

        private async Task LoadImagesAsync(long id, long coverId, string coverExtension, long artworkId, string artworkExtension, long logoId, string logoExtension, CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                CancellationToken cancellationToken = cancellationTokenSource.Token;

                (BitmapImage? cover, BitmapImage? artwork, BitmapImage? logo) = await Task.Run(() =>
                {
                    BitmapImage? cover = ManagerFactory.libraryManager.GetGameArt(id, LibraryType.cover, coverId, coverExtension);
                    BitmapImage? artwork = ManagerFactory.libraryManager.GetGameArt(id, LibraryType.artwork, artworkId, artworkExtension);
                    BitmapImage? logo = ManagerFactory.libraryManager.GetGameArt(id, LibraryType.logo, logoId, logoExtension);
                    return (cover, artwork, logo);
                }, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested || !ReferenceEquals(visualsLoadCancellationTokenSource, cancellationTokenSource))
                    return;

                await UIHelper.TryInvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested || !ReferenceEquals(visualsLoadCancellationTokenSource, cancellationTokenSource))
                        return;

                    Cover = cover;
                    Artwork = artwork;
                    Logo = logo;
                }, DispatcherPriority.Render).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelPendingVisualLoad()
        {
            CancellationTokenSource? cancellationTokenSource = visualsLoadCancellationTokenSource;
            visualsLoadCancellationTokenSource = null;

            if (cancellationTokenSource is null)
                return;

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch
            {
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        public void SetVisualsVisible(bool isVisible)
        {
            if (!deferVisualLoading)
                return;

            if (areVisualsVisible == isVisible)
                return;

            areVisualsVisible = isVisible;
            RefreshImages();
        }

        public override string ToString()
        {
            return Name;
        }

        public string Name => _Profile.Name;
        public int SortOrder => _Profile.IsSubProfile ? 1 : 0;
        public string Description => _Profile.IsSubProfile ? _Profile.GetOwnerName() : _Profile.PlatformType.ToString();

        public DateTime DateCreated => _Profile.DateCreated;
        public DateTime DateModified => _Profile.DateModified;
        public DateTime LastUsed => _Profile.LastUsed;
        public bool IsLiked => _Profile.IsLiked;

        public GamePlatform PlatformType => _Profile.PlatformType;

        public bool IsRunning => ProcessManager.GetProcesses().Any(p => p.Path.Equals(Profile.Path));
        public bool IsAvailable => _Profile.CanExecute && !ProcessManager.GetProcesses().Any(p => p.Path.Equals(Profile.Path));
        public bool CanStopProcess => IsRunning;
        public bool CanToggleProcess => IsAvailable || CanStopProcess;

        private bool _IsBusy;
        public bool IsBusy
        {
            get => _IsBusy;
            set
            {
                if (value != _IsBusy)
                {
                    _IsBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                }
            }
        }

        public ImageSource? Icon => _Profile.Icon;

        public Image Platform
        {
            get
            {
                switch (PlatformType)
                {
                    default:
                    case GamePlatform.Generic:
                        return null;
                    case GamePlatform.Steam:
                        return PlatformManager.Steam.GetLogo();
                    case GamePlatform.Origin:
                        return PlatformManager.Origin.GetLogo();
                    case GamePlatform.EADesktop:
                        return PlatformManager.EADesktop.GetLogo();
                    case GamePlatform.UbisoftConnect:
                        return PlatformManager.UbisoftConnect.GetLogo();
                    case GamePlatform.GOG:
                        return PlatformManager.GOGGalaxy.GetLogo();
                    case GamePlatform.BattleNet:
                        return PlatformManager.BattleNet.GetLogo();
                    case GamePlatform.Epic:
                        return PlatformManager.Epic.GetLogo();
                    case GamePlatform.RiotGames:
                        return PlatformManager.RiotGames.GetLogo();
                    case GamePlatform.Rockstar:
                        return PlatformManager.Rockstar.GetLogo();
                }
            }
        }

        private BitmapImage? _cover;
        public BitmapImage? Cover
        {
            get => _cover;
            set
            {
                if (_cover != value)
                {
                    _cover = value;
                    OnPropertyChanged(nameof(Cover));
                }
            }
        }

        private BitmapImage? _artwork;
        public BitmapImage? Artwork
        {
            get => _artwork;
            set
            {
                if (_artwork != value)
                {
                    _artwork = value;
                    OnPropertyChanged(nameof(Artwork));
                }
            }
        }

        private BitmapImage? _logo;
        public BitmapImage? Logo
        {
            get => _logo;
            set
            {
                if (_logo != value)
                {
                    _logo = value;
                    OnPropertyChanged(nameof(Logo));
                }
            }
        }

        public ProfileViewModel(Profile profile, bool isQuickTools, bool deferVisualLoading = false)
        {
            IsQuickTools = isQuickTools;
            this.deferVisualLoading = deferVisualLoading;
            areVisualsVisible = !deferVisualLoading;
            Profile = profile;

            ManagerFactory.processManager.ProcessStarted += ProcessManager_ProcessStarted;
            ManagerFactory.processManager.ProcessStopped += ProcessManager_ProcessStopped;

            StartProcessCommand = new DelegateCommand<bool>(async runAsAdmin =>
            {
                // localize me
                Dialog dialog = new Dialog(isQuickTools ? OverlayQuickTools.GetCurrent() : MainWindow.GetCurrent())
                {
                    Title = "Launching",
                    Content = "The system cannot find the file specified.",
                    PrimaryButtonText = Properties.Resources.ProfilesPage_OK,
                    CanClose = true,
                };

                if (!File.Exists(profile.Path))
                {
                    ContentDialogResult result = await dialog.ShowAsync();
                    switch (result)
                    {
                        case ContentDialogResult.None:
                            dialog.Hide();
                            break;
                    }
                    return;
                }

                // localize me
                dialog.UpdateContent("Please wait while we initialize the application.");
                dialog.PrimaryButtonText = string.Empty;
                dialog.CanClose = false;

                // display dialog
                dialog.ShowAsync();

                try
                {
                    // set profile as favorite
                    ManagerFactory.profileManager.SetFavorite(Profile);

                    await Task.Run(async () =>
                    {
                        using (Process? process = profile.Launch(runAsAdmin))
                        {
                            // failed to start the process
                            if (process == null)
                                return;

                            // wait up to 60 sec for any visible window
                            List<string> execs = profile.GetExecutables(true);

                            Task timeout = Task.Delay(TimeSpan.FromSeconds(60));
                            while (!timeout.IsCompleted && !ProcessManager.GetProcesses().Any(p => execs.Contains(p.Path)))
                                await Task.Delay(300).ConfigureAwait(false);

                            if (ProcessManager.GetProcesses().Any(p => execs.Contains(p.Path)))
                                MainWindow.GetCurrent().SetState(WindowState.Minimized);

                            // hide the dialog
                            UIHelper.TryInvoke(() => dialog.Hide());

                            // Wait until none of the known executables are running
                            while (ProcessManager.GetProcesses().Any(p => execs.Contains(p.Path)))
                                await Task.Delay(1000).ConfigureAwait(false);

                            if (IsMainPage)
                                MainWindow.GetCurrent().SetState(WindowState.Normal);
                        }
                    }).ConfigureAwait(false);
                }
                catch { }
                finally
                {
                    // always hide the dialog
                    UIHelper.TryInvoke(() => { dialog.Hide(); });
                }
            });

            ToggleProcessCommand = new DelegateCommand(async () =>
            {
                if (CanStopProcess)
                {
                    ProcessEx? processEx = ProcessManager.GetProcesses().FirstOrDefault(p => p.Path.Equals(Profile.Path));
                    if (processEx is not null)
                    {
                        ProcessExViewModel processViewModel = new(processEx, IsQuickTools);
                        try
                        {
                            processViewModel.KillProcessCommand.Execute(null);
                        }
                        finally
                        {
                            // Don't dispose - let the async command complete
                            // processViewModel.Dispose();
                        }
                    }

                    return;
                }

                if (IsAvailable)
                    StartProcessCommand.Execute(false);
            });

            ToggleFavoriteCommand = new DelegateCommand(() =>
            {
                Profile.IsLiked = !Profile.IsLiked;
                OnPropertyChanged(nameof(IsLiked));
                ManagerFactory.profileManager.UpdateOrCreateProfile(Profile);
            });

            Navigate = new DelegateCommand(async () =>
            {
                var page = MainWindow.profilesPage;

                // Set the selected main profile via ViewModel (MVVM)
                Profile target = Profile.IsSubProfile
                    ? ManagerFactory.profileManager.GetParent(Profile)
                    : Profile;

                // Use ViewModel instead of direct control access
                page.viewModel.SelectedMainProfile = target;

                // Set selected sub-profile
                if (Profile.IsSubProfile)
                    page.viewModel.SelectedProfile = Profile;

                MainWindow.GetCurrent().NavigateToPage("ProfilesPage");
            });

            OpenLayout = new DelegateCommand(() =>
            {
                var page = MainWindow.profilesPage;

                // Set the selected main profile via ViewModel (MVVM)
                Profile target = Profile.IsSubProfile
                    ? ManagerFactory.profileManager.GetParent(Profile)
                    : Profile;

                // Use ViewModel instead of direct control access
                page.viewModel.SelectedMainProfile = target;

                // Set selected sub-profile
                if (Profile.IsSubProfile)
                    page.viewModel.SelectedProfile = Profile;

                // prepare layout editor
                LayoutTemplate layoutTemplate = new(target.Layout)
                {
                    Name = target.LayoutTitle,
                    Description = Properties.Resources.ProfilesPage_Layout_Desc,
                    Author = Environment.UserName,
                    Executable = target.Executable,
                    Product = target.Name,
                };

                MainWindow.layoutPage.UpdateLayoutTemplate(layoutTemplate);
                MainWindow.NavView_Navigate(MainWindow.layoutPage);
            });
        }

        private void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            ProcessManager_Changes(processEx.Path);
        }

        private void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            ProcessManager_Changes(processEx.Path);
        }

        public override void Dispose()
        {
            SetVisualsVisible(false);
            CancelPendingVisualLoad();

            ManagerFactory.processManager.ProcessStarted -= ProcessManager_ProcessStarted;
            ManagerFactory.processManager.ProcessStopped -= ProcessManager_ProcessStopped;

            // dispose commands
            StartProcessCommand = null;
            OpenLayout = null;

            base.Dispose();
        }

        private void ProcessManager_Changes(string path)
        {
            if (path.Equals(Profile.Path))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsAvailable));
                OnPropertyChanged(nameof(CanStopProcess));
                OnPropertyChanged(nameof(CanToggleProcess));
            }
        }
    }
}
