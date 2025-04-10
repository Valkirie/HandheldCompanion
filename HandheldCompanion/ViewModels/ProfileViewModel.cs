using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public class ProfileViewModel : BaseViewModel
    {
        public ICommand StartProcessCommand { get; private set; }

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

        public string Name => _Profile.ToString();

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

        public ProfileViewModel(Profile profile)
        {
            Profile = profile;

            ManagerFactory.processManager.ProcessStarted += ProcessManager_ProcessStarted;
            ManagerFactory.processManager.ProcessStopped += ProcessManager_ProcessStopped;

            StartProcessCommand = new DelegateCommand(async () =>
            {
                if (!File.Exists(profile.Path))
                {
                    // localize me
                    new Dialog(OverlayQuickTools.GetCurrent())
                    {
                        Title = "Quick start",
                        Content = "The system cannot find the file specified.",
                        PrimaryButtonText = Properties.Resources.ProfilesPage_OK
                    }.Show();

                    return;
                }

                // localize me
                Dialog dialog = new Dialog(OverlayQuickTools.GetCurrent())
                {
                    Title = "Quick start",
                    Content = "Please wait while we initialize the application.",
                    CanClose = false
                };

                // display dialog
                dialog.Show();

                // Create a new instance of ProcessStartInfo
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = profile.Path,
                    Arguments = profile.Arguments
                };

                Process process = new() { StartInfo = startInfo };

                // Run the process start operation in a task to avoid blocking the UI thread
                await Task.Run(() =>
                {
                    process.Start();
                    process.WaitForInputIdle(4000);
                });

                dialog.Hide();

                if (process is not null && process.MainWindowHandle != IntPtr.Zero)
                    WinAPI.SetForegroundWindow(process.MainWindowHandle);
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
