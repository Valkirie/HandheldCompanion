using ControllerCommon.Utils;
using ModernWpf.Controls;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using static HandheldCompanion.Managers.EnergyManager;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;

namespace HandheldCompanion.Managers.Classes
{
    public class ProcessEx
    {
        public Process Process;
        public IntPtr MainWindowHandle;
        public object processInfo;
        public QualityOfServiceLevel QoL;

        public uint Id;
        public string Name;
        public string Executable;
        public string Path;
        public bool Bypassed;

        private ThreadWaitReason threadWaitReason = ThreadWaitReason.UserRequest;

        // UI vars
        public Border processBorder;
        public Grid processGrid;
        public TextBlock processName;
        public Image processIcon;
        public Button processSuspend;
        public Button processResume;
        public FontIcon processQoS;

        public ProcessEx(Process process)
        {
            this.Process = process;
            this.Id = (uint)process.Id;
        }

        public void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                Process.Refresh();

                if (Process.HasExited)
                    return;

                var processThread = Process.Threads[0];

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (MainWindowHandle != IntPtr.Zero)
                    {
                        if (processBorder is null)
                            return;

                        processBorder.Visibility = Visibility.Visible;
                        string MainWindowTitle = ProcessUtils.GetWindowTitle(MainWindowHandle);
                        if (!string.IsNullOrEmpty(MainWindowTitle))
                            processName.Text = MainWindowTitle;
                    }
                    else
                        processBorder.Visibility = Visibility.Collapsed;

                    // manage process state
                    switch (processThread.ThreadState)
                    {
                        case ThreadState.Wait:

                            if (processThread.WaitReason != threadWaitReason)
                            {
                                switch (processThread.WaitReason)
                                {
                                    case ThreadWaitReason.Suspended:
                                        processSuspend.Visibility = Visibility.Collapsed;
                                        processResume.Visibility = Visibility.Visible;

                                        processResume.IsEnabled = true;
                                        break;
                                    default:
                                        processSuspend.Visibility = Visibility.Visible;
                                        processResume.Visibility = Visibility.Collapsed;

                                        processSuspend.IsEnabled = true;
                                        break;
                                }
                            }

                            threadWaitReason = processThread.WaitReason;
                            break;
                        default:
                            threadWaitReason = ThreadWaitReason.UserRequest;
                            break;
                    }

                    // manage process throttling
                    // var result = EnergyManager.GetProcessInfo(Process.Handle, WinAPI.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, out processInfo);
                    switch (QoL)
                    {
                        // HighQoS
                        case QualityOfServiceLevel.Default:
                        case QualityOfServiceLevel.High:
                            processQoS.Glyph = "\uE945";
                            processQoS.Foreground = new SolidColorBrush(Color.FromRgb(25, 144, 161));
                            break;
                        // EcoQoS
                        case QualityOfServiceLevel.Eco:
                            processQoS.Glyph = "\uE8BE";
                            processQoS.Foreground = new SolidColorBrush(Color.FromRgb(193, 127, 48));
                            break;
                    }
                }), DispatcherPriority.ContextIdle);
            }
            catch (Exception) { }
        }

        public Border GetControl()
        {
            return processBorder;
        }

        public void DrawControl()
        {
            if (processBorder != null)
                return;

            processBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Visibility = Visibility.Collapsed,
                Tag = Name
            };
            processBorder.SetResourceReference(Control.BackgroundProperty, "LayerOnMicaBaseAltFillColorDefaultBrush");

            // Create Grid
            processGrid = new();

            // Define the Columns
            ColumnDefinition colDef0 = new ColumnDefinition()
            {
                Width = new GridLength(32, GridUnitType.Pixel),
                MinWidth = 32
            };
            processGrid.ColumnDefinitions.Add(colDef0);

            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(8, GridUnitType.Star)
            };
            processGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 120
            };
            processGrid.ColumnDefinitions.Add(colDef2);

            ColumnDefinition colDef3 = new ColumnDefinition()
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 32
            };
            processGrid.ColumnDefinitions.Add(colDef3);

            // Create PersonPicture
            var icon = Icon.ExtractAssociatedIcon(Path);
            processIcon = new Image()
            {
                Height = 32,
                Width = 32,
                Source = icon.ToImageSource()
            };
            Grid.SetColumn(processIcon, 0);
            processGrid.Children.Add(processIcon);

            // Create SimpleStackPanel
            var StackPanel = new SimpleStackPanel()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Create TextBlock(s)
            processName = new TextBlock()
            {
                FontSize = 14,
                Text = Name,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            processName.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            StackPanel.Children.Add(processName);

            var processExecutable = new TextBlock()
            {
                FontSize = 12,
                Text = Executable,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            processExecutable.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            StackPanel.Children.Add(processExecutable);

            Grid.SetColumn(StackPanel, 1);
            processGrid.Children.Add(StackPanel);

            // Create Download Button
            processSuspend = new Button()
            {
                FontSize = 14,
                Content = "Suspend", // localize me !
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                // Style = Application.Current.FindResource("DefaultButtonStyle") as Style
            };
            processSuspend.Click += ProcessSuspend_Click;

            Grid.SetColumn(processSuspend, 2);
            processGrid.Children.Add(processSuspend);

            // Create Install Button
            processResume = new Button()
            {
                FontSize = 14,
                Content = "Resume", // localize me !
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed,
                Style = Application.Current.FindResource("AccentButtonStyle") as Style
            };
            processResume.Click += ProcessResume_Click;

            Grid.SetColumn(processResume, 2);
            processGrid.Children.Add(processResume);

            // Create EcoQos indicator
            processQoS = new FontIcon()
            {
                FontSize = 14,
                Glyph = "\uE945",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            Grid.SetColumn(processQoS, 3);
            processGrid.Children.Add(processQoS);

            processBorder.Child = processGrid;
        }

        private void ProcessResume_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processResume.IsEnabled = false;
                ProcessUtils.NtResumeProcess(Process.Handle);
                Task.Delay(500);
                ProcessUtils.ShowWindow(MainWindowHandle, ProcessUtils.SW_RESTORE);
            }));
        }

        private void ProcessSuspend_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processSuspend.IsEnabled = false;

                ProcessUtils.ShowWindow(MainWindowHandle, ProcessUtils.SW_MINIMIZE);
                Task.Delay(500);
                ProcessUtils.NtSuspendProcess(Process.Handle);
            }));
        }
    }
}
