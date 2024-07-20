using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public class ProcessExViewModel : BaseViewModel
    {
        QuickApplicationsPageViewModel PageViewModel;

        private ProcessEx _process;
        public ProcessEx Process
        {
            get => _process;
            set
            {
                // todo: we need to check if _hotkey != value but this will return false because this is a pointer
                // I've implemented all required Clone() functions but not sure where to call them

                UpdateProcess(_process, value);
                OnPropertyChanged(nameof(Process));
            }
        }

        public string Title => Process.MainWindowTitle;

        public ImageSource Icon => Process.ProcessIcon;

        public bool IsSuspended
        {
            get => Process.IsSuspended;
            set
            {
                if (value != IsSuspended)
                {
                    Process.IsSuspended = value;
                }
            }
        }

        public string Executable => Process.Executable;

        public bool FullScreenOptimization
        {
            get => !Process.FullScreenOptimization;
            set { } // empty set to allow binding to ToggleSwitch.IsOn
        }

        public bool HighDPIAware
        {
            get => !Process.HighDPIAware;
            set { } // empty set to allow binding to ToggleSwitch.IsOn
        }

        public bool HasTwoScreen => Screen.AllScreens.Length > 1;
        private Screen CurrentScreen = Screen.PrimaryScreen;
        public bool IsPrimaryScreen => CurrentScreen.Primary;

        public ICommand KillProcessCommand { get; private set; }
        public ICommand BringProcessCommand { get; private set; }
        public ICommand SwapScreenCommand { get; private set; }

        public ProcessExViewModel(ProcessEx process, QuickApplicationsPageViewModel pageViewModel)
        {
            Process = process;
            PageViewModel = pageViewModel;

            MultimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

            KillProcessCommand = new DelegateCommand(async () =>
            {
                Dialog dialog = new Dialog(OverlayQuickTools.GetCurrent())
                {
                    Title = "Terminate application",
                    Content = string.Format("Do you want to end the application '{0}'?", Title),
                    DefaultButton = ContentDialogButton.Close,
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
                };

                Task<ContentDialogResult> dialogTask = dialog.ShowAsync();
                await dialogTask; // sync call

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        Process.Process?.Kill();
                        break;
                    default:
                        dialog.Hide();
                        break;
                }
            });

            BringProcessCommand = new DelegateCommand(async () =>
            {
                OverlayQuickTools qtWindow = OverlayQuickTools.GetCurrent();
                
                ContentDialogResult result = await qtWindow.applicationsPage.SnapDialog.ShowAsync(qtWindow);
                switch(result)
                {
                    case ContentDialogResult.None:
                        qtWindow.applicationsPage.SnapDialog.Hide();
                        return;
                }

                // Get the screen where the reference window is located
                Screen screen = Screen.FromHandle(Process.MainWindowHandle);
                if (screen is null)
                    return;

                WinAPI.MoveWindow(Process.MainWindowHandle, screen, PageViewModel.windowPositions);
                WinAPI.MakeBorderless(Process.MainWindowHandle, PageViewModel.BorderlessEnabled && PageViewModel.BorderlessToggle);
                WinAPI.SetForegroundWindow(Process.MainWindowHandle);
            });

            SwapScreenCommand = new DelegateCommand(async () =>
            {
                Screen screen = Screen.AllScreens.Where(screen => screen.DeviceName != CurrentScreen.DeviceName).FirstOrDefault();
                WinAPI.MoveWindow(Process.MainWindowHandle, screen, WpfScreenHelper.Enum.WindowPositions.Maximize);
                WinAPI.SetForegroundWindow(Process.MainWindowHandle);

                OnPropertyChanged(nameof(IsPrimaryScreen));
            });
        }

        private void MultimediaManager_DisplaySettingsChanged(Managers.Desktop.DesktopScreen screen, Managers.Desktop.ScreenResolution resolution)
        {
            // update current screen
            CurrentScreen = Screen.FromHandle(Process.MainWindowHandle);

            OnPropertyChanged(nameof(IsPrimaryScreen));
            OnPropertyChanged(nameof(HasTwoScreen));
        }

        private void UpdateProcess(ProcessEx oldProcess, ProcessEx newProcess)
        {
            if (oldProcess is not null)
            {
                oldProcess.Refreshed -= ProcessRefreshed;
            }

            newProcess.Refreshed += ProcessRefreshed;

            _process = newProcess;
        }

        private void ProcessRefreshed(object? sender, EventArgs e)
        {
            // Property Changed is called here as processes are refreshed at an interval
            OnPropertyChanged(nameof(IsSuspended));
            OnPropertyChanged(nameof(FullScreenOptimization));
            OnPropertyChanged(nameof(HighDPIAware));
            OnPropertyChanged(nameof(Title));

            // update current screen
            CurrentScreen = Screen.FromHandle(Process.MainWindowHandle);
            OnPropertyChanged(nameof(IsPrimaryScreen));
        }

        public override void Dispose()
        {
            if (_process is not null)
                _process.Refreshed -= ProcessRefreshed;

            base.Dispose();
        }
    }
}
