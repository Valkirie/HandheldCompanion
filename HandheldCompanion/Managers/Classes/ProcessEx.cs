using ControllerCommon;
using ControllerCommon.Utils;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using static HandheldCompanion.Managers.EnergyManager;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;
using Brush = System.Windows.Media.Brush;
using Image = System.Windows.Controls.Image;

namespace HandheldCompanion.Managers.Classes
{
    public class ProcessEx
    {
        public Process Process;
        public List<int> Children = new();

        public IntPtr MainWindowHandle;
        public object processInfo;
        public QualityOfServiceLevel EcoQoS;

        public int Id;
        public string Name;
        public string Executable;
        public string Path;
        public bool IsIgnored;

        private ThreadWaitReason threadWaitReason = ThreadWaitReason.UserRequest;

        // UI vars
        public Expander processExpander;
        public Grid processGrid;

        public TextBlock processName;
        public TextBlock processDescription;

        public Image processIcon;
        public Button processSuspend;
        public Button processResume;

        public SimpleStackPanel processStackPanel;
        public TextBlock processQoS;

        public event ChildProcessCreatedEventHandler ChildProcessCreated;
        public delegate void ChildProcessCreatedEventHandler(ProcessEx parent, int Id);

        public ProcessEx(Process process)
        {
            this.Process = process;
            this.Id = process.Id;
        }

        public void Refresh()
        {
            try
            {
                Process.Refresh();

                if (Process.HasExited)
                    return;

                // refresh all child processes
                List<int> childs = ProcessUtils.GetChildIds(this.Process);

                // remove exited children
                Children.RemoveAll(item => !childs.Contains(item));

                // raise event on new children
                foreach (int child in childs.Where(item => !Children.Contains(item)))
                {
                    Children.Add(child);
                    ChildProcessCreated?.Invoke(this, child);
                }

                var processThread = Process.Threads[0];

                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (MainWindowHandle != IntPtr.Zero)
                    {
                        if (processExpander is null)
                            return;

                        processExpander.Visibility = Visibility.Visible;
                        string MainWindowTitle = ProcessUtils.GetWindowTitle(MainWindowHandle);
                        if (!string.IsNullOrEmpty(MainWindowTitle))
                            processDescription.Text = MainWindowTitle;
                    }
                    else
                        processExpander.Visibility = Visibility.Collapsed;

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
                    processQoS.Text = EnumUtils.GetDescriptionFromEnumValue(EcoQoS);
                }), DispatcherPriority.ContextIdle);
            }
            catch (Exception) { }
        }

        public Expander GetControl()
        {
            return processExpander;
        }

        public bool IsSuspended()
        {
            return threadWaitReason == ThreadWaitReason.Suspended;
        }

        public void DrawControl()
        {
            if (processExpander != null)
                return;

            processExpander = new Expander()
            {
                Padding = new Thickness(20, 12, 12, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                Tag = Name
            };
            processExpander.SetResourceReference(Control.BackgroundProperty, "LayerOnMicaBaseAltFillColorDefaultBrush");

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
                Width = new GridLength(2, GridUnitType.Star),
                MinWidth = 120
            };
            processGrid.ColumnDefinitions.Add(colDef2);

            // Create PersonPicture
            var icon = Icon.ExtractAssociatedIcon(Path);
            processIcon = new Image()
            {
                Height = 24,
                Width = 24,
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
                Text = Executable,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            processName.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            StackPanel.Children.Add(processName);

            processDescription = new TextBlock()
            {
                FontSize = 12,
                Text = Name,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            processDescription.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            StackPanel.Children.Add(processDescription);

            Grid.SetColumn(StackPanel, 1);
            processGrid.Children.Add(StackPanel);

            // Create Download Button
            processSuspend = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.ResourceManager.GetString("ProcessEx_processSuspend"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            processSuspend.Click += ProcessSuspend_Click;

            Grid.SetColumn(processSuspend, 2);
            processGrid.Children.Add(processSuspend);

            // Create Install Button
            processResume = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.ResourceManager.GetString("ProcessEx_processResume"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed,
                Style = Application.Current.FindResource("AccentButtonStyle") as Style
            };
            processResume.Click += ProcessResume_Click;

            Grid.SetColumn(processResume, 2);
            processGrid.Children.Add(processResume);

            // Create EcoQoS indicator
            processQoS = new TextBlock()
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            processStackPanel = new SimpleStackPanel()
            {
                Spacing = 12,
                Margin = new Thickness(30, 0, 0, 0),
            };

            Grid row1 = new Grid();
            row1.Children.Add(new TextBlock() { Text = "Process ID", HorizontalAlignment = HorizontalAlignment.Left });
            row1.Children.Add(new TextBlock() { Text = Id.ToString(), HorizontalAlignment = HorizontalAlignment.Right });
            processStackPanel.Children.Add(row1);

            processStackPanel.Children.Add(new Separator()
            {
                Margin = new Thickness(-50, 0, -20, 0),
                Background = Application.Current.FindResource("SystemControlBackgroundChromeMediumBrush") as Brush
            });

            Grid row2 = new Grid();
            row2.Children.Add(new TextBlock() { Text = "EcoQoS", HorizontalAlignment = HorizontalAlignment.Left });
            row2.Children.Add(processQoS);
            processStackPanel.Children.Add(row2);

            processExpander.Header = processGrid;
            processExpander.Content = processStackPanel;
        }

        private void ProcessResume_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processResume.IsEnabled = false;
                ProcessManager.ResumeProcess(this);
            }));
        }

        private void ProcessSuspend_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                processSuspend.IsEnabled = false;
                ProcessManager.SuspendProcess(this);
            }));
        }
    }
}
