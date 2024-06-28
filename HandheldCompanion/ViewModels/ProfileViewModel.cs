using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
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
        QuickApplicationsPageViewModel PageViewModel;

        public ICommand StartProcessCommand { get; private set; }

        private Profile _Profile;
        public Profile Profile
        {
            get => _Profile;
            set
            {
                _Profile = value;
                _Name = value.ToString();

                OnPropertyChanged(nameof(Profile));
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _Name = string.Empty;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public bool IsAvailable
        {
            get
            {
                return !ProcessManager.GetProcesses().Any(p => p.Path.Equals(Profile.Path));
            }
            set
            {
                OnPropertyChanged(nameof(IsAvailable));
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

        public ProfileViewModel(Profile profile, QuickApplicationsPageViewModel pageViewModel)
        {
            Profile = profile;
            PageViewModel = pageViewModel;

            ProcessManager.ProcessStarted += (processEx, onStartup) => ProcessManager_Changes();
            ProcessManager.ProcessStopped += (processEx) => ProcessManager_Changes();

            StartProcessCommand = new DelegateCommand(async () =>
            {
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
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = profile.Path;
            startInfo.Arguments = profile.Arguments;

                Process process = new();

                // Run the process start operation in a task to avoid blocking the UI thread
                await Task.Run(() =>
                {
                    // Start the process with the startInfo configuration
                    process = Process.Start(startInfo);
                    process.WaitForInputIdle();
                });

                dialog.Hide();

                if (process is not null && process.MainWindowHandle != IntPtr.Zero)
                    WinAPI.SetForegroundWindow(process.MainWindowHandle);
            });
        }

        private void ProcessManager_Changes()
        {
            IsAvailable = !ProcessManager.GetProcesses().Any(p => p.Path.Equals(Profile.Path));
        }
    }
}
