using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using System;
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
        public ICommand Navigate { get; private set; }

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

        public string Name => _Profile.Name;
        public string Description => _Profile.GetOwnerName();

        public bool IsAvailable => !ProcessManager.GetProcesses().Any(p => p.Path.Equals(Profile.Path));

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

        public ProfileViewModel(Profile profile, bool isQuickTools)
        {
            Profile = profile;
            IsQuickTools = isQuickTools;

            ManagerFactory.processManager.ProcessStarted += ProcessManager_ProcessStarted;
            ManagerFactory.processManager.ProcessStopped += ProcessManager_ProcessStopped;

            StartProcessCommand = new DelegateCommand(async () =>
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
                    // display dialog
                    dialog.Show();
                    return;
                }

                // localize me
                dialog.UpdateContent("Please wait while we initialize the application.");
                dialog.PrimaryButtonText = string.Empty;
                dialog.CanClose = false;

                // display dialog
                dialog.Show();

                try
                {
                    // 3) Kick off the process off the UI thread
                    await Task.Run(() =>
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = Profile.Path,
                            Arguments = Profile.Arguments,
                            UseShellExecute = true
                        };

                        using (Process? process = Process.Start(psi))
                        {
                            // failed to start the process
                            if (process == null)
                                return;

                            // give it a moment to initialize
                            try
                            {
                                process.WaitForInputIdle(3000);
                            }
                            catch { }

                            // process has exited
                            if (process.HasExited)
                                return;

                            // wait up to 10 sec for any visible window
                            IntPtr hWnd = ProcessUtils.WaitForVisibleWindow(process, 10);
                            if (hWnd != IntPtr.Zero)
                            {
                                if (IsMainPage)
                                    MainWindow.GetCurrent().SetState(WindowState.Minimized);

                                ProcessUtils.SetForegroundWindow(hWnd);
                            }

                            // hide the dialog
                            UIHelper.TryInvoke(() => dialog.Hide());

                            process.WaitForExit();

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

            Navigate = new DelegateCommand(async () =>
            {
                if (Profile.IsSubProfile)
                {
                    Profile MasterProfile = ManagerFactory.profileManager.GetProfileForSubProfile(Profile);
                    MainWindow.profilesPage.cB_Profiles.SelectedItem = MasterProfile;
                    MainWindow.profilesPage.cb_SubProfilePicker.SelectedItem = Profile;
                }
                else
                {
                    MainWindow.profilesPage.cB_Profiles.SelectedItem = Profile;
                    MainWindow.profilesPage.cb_SubProfilePicker.SelectedIndex = 0;
                }

                MainWindow.GetCurrent().NavigateToPage("ProfilesPage");
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

            base.Dispose();
        }

        private void ProcessManager_Changes(string path)
        {
            if (path.Equals(Profile.Path))
                OnPropertyChanged(nameof(IsAvailable));
        }
    }
}
