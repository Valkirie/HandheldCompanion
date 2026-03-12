using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Managers.LibraryManager;

namespace HandheldCompanion.ViewModels
{
    public class ProfileViewModel : BaseViewModel
    {
        public ICommand StartProcessCommand { get; private set; }
        public ICommand ToggleProcessCommand { get; private set; }
        public ICommand Navigate { get; private set; }
        public ICommand OpenLayout { get; private set; }

        public readonly bool IsQuickTools;
        public bool IsMainPage => !IsQuickTools;

        private Profile _Profile;
        public Profile Profile
        {
            get => _Profile;
            set
            {
                // todo: we need to check if _hotkey != value but this will return false because this is a pointer
                // I've implemented all required Clone() functions but not sure where to call them

                _Profile = value;

                // refresh all properties
                OnPropertyChanged(string.Empty);
                OnPropertyChanged(nameof(Name));
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public string Name => _Profile.Name;
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

        public ImageSource Icon
        {
            get
            {
                // todo: use Profile.ErrorCode instead (MissingExecutable | MissingPath)
                if (!string.IsNullOrEmpty(Profile.Path) && File.Exists(Profile.Path))
                {
                    Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(Profile.Path);
                    {
                        if (icon is not null)
                        {
                            ImageSource imgSource = icon.ToImageSource();
                            return icon.ToImageSource();
                        }
                    }
                }
                return null;
            }
        }

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

        public BitmapImage Cover
        {
            get
            {
                if (Profile.LibraryEntry is null)
                    return LibraryResources.MissingCover;

                long id = Profile.LibraryEntry.Id;
                long imageId = Profile.LibraryEntry.GetCoverId();
                string imageExtension = Profile.LibraryEntry.GetCoverExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.cover, imageId, imageExtension);
            }
        }

        public BitmapImage Artwork
        {
            get
            {
                if (Profile.LibraryEntry is null)
                    return null;

                long id = Profile.LibraryEntry.Id;
                long imageId = Profile.LibraryEntry.GetArtworkId();
                string imageExtension = Profile.LibraryEntry.GetArtworkExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.artwork, imageId, imageExtension);
            }
        }

        public BitmapImage Logo
        {
            get
            {
                if (Profile.LibraryEntry is null)
                    return null;

                long id = Profile.LibraryEntry.Id;
                long imageId = Profile.LibraryEntry.GetLogoId();
                string imageExtension = Profile.LibraryEntry.GetLogoExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.logo, imageId, imageExtension);
            }
        }

        public ProfileViewModel(Profile profile, bool isQuickTools)
        {
            Profile = profile;
            IsQuickTools = isQuickTools;

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
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = !string.IsNullOrEmpty(profile.LaunchString) ? profile.LaunchString : Profile.Executable,
                            WorkingDirectory = Directory.GetParent(Profile.Path)?.FullName ?? string.Empty,
                            Arguments = Profile.Arguments,
                            UseShellExecute = true,
                            Verb = runAsAdmin ? "runas" : string.Empty,
                        };

                        using (Process? process = Process.Start(psi))
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
