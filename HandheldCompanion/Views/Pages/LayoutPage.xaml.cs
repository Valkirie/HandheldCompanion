using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Application = System.Windows.Application;
using Layout = ControllerCommon.Layout;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for ControllerSettings.xaml
    /// </summary>
    public partial class LayoutPage : Page
    {
        // page vars
        private ButtonsPage buttonsPage = new();
        private DpadPage dpadPage = new();
        private TriggersPage triggersPage = new();
        private JoysticksPage joysticksPage = new();
        private TrackpadsPage trackpadsPage = new();
        private GyroPage gyroPage = new();

        private Dictionary<string, ILayoutPage> _pages;

        private string preNavItemTag;

        private LayoutTemplate currentTemplate = new();
        private Layout currentLayout = new();

        protected object updateLock = new();

        public LayoutPage()
        {
            InitializeComponent();
        }

        public LayoutPage(string Tag) : this()
        {
            this.Tag = Tag;

            // manage layout pages visibility
            navTrackpads.Visibility = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.Trackpads) ? Visibility.Visible : Visibility.Collapsed;
            navGyro.Visibility = MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.InternalSensor) || MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ExternalSensor) || MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.ControllerSensor) ? Visibility.Visible : Visibility.Collapsed;

            // create controller related pages
            this._pages = new()
            {
                // buttons
                { "ButtonsPage", buttonsPage },
                { "DpadPage", dpadPage },

                // triger
                { "TriggersPage", triggersPage },

                // axis
                { "JoysticksPage", joysticksPage },
                { "TrackpadsPage", trackpadsPage },

                // gyro
                { "GyroPage", gyroPage },
            };

            foreach (ButtonMapping buttonMapping in buttonsPage.MappingButtons.Values.Union(dpadPage.MappingButtons.Values).Union(triggersPage.MappingButtons.Values).Union(joysticksPage.MappingButtons.Values).Union(trackpadsPage.MappingButtons.Values))
            {
                buttonMapping.Updated += (sender, action) => ButtonMapping_Updated((ButtonFlags)sender, action);
                buttonMapping.Deleted += (sender) => ButtonMapping_Deleted((ButtonFlags)sender);
            }

            foreach (TriggerMapping AxisMapping in triggersPage.MappingTriggers.Values)
            {
                AxisMapping.Updated += (sender, action) => AxisMapping_Updated((AxisLayoutFlags)sender, action);
                AxisMapping.Deleted += (sender) => AxisMapping_Deleted((AxisLayoutFlags)sender);
            }

            foreach (AxisMapping axisMapping in joysticksPage.MappingAxis.Values.Union(trackpadsPage.MappingAxis.Values))
            {
                axisMapping.Updated += (sender, action) => AxisMapping_Updated((AxisLayoutFlags)sender, action);
                axisMapping.Deleted += (sender) => AxisMapping_Deleted((AxisLayoutFlags)sender);
            }

            LayoutManager.Updated += LayoutManager_Updated;
            LayoutManager.Initialized += LayoutManager_Initialized;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // auto-sort
            // cB_Layouts.Items.SortDescriptions.Add(new SortDescription("", ListSortDirection.Descending));
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            RefreshLayoutList();
        }

        private void LayoutManager_Initialized()
        {
            RefreshLayoutList();
        }

        private void LayoutManager_Updated(LayoutTemplate layoutTemplate)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Get template separator index
                int idx = -1;

                // search if we already have this template listed
                foreach (object item in cB_Layouts.Items)
                {
                    if (item.GetType() != typeof(ComboBoxItem))
                        continue;

                    // get template
                    ComboBoxItem parent = (ComboBoxItem)item;
                    if (parent.Content.GetType() != typeof(LayoutTemplate))
                        continue;

                    LayoutTemplate template = (LayoutTemplate)parent.Content;
                    if (template.Guid.Equals(layoutTemplate.Guid))
                    {
                        idx = cB_Layouts.Items.IndexOf(parent);
                        break;
                    }
                }

                if (idx != -1)
                {
                    // found it
                    cB_Layouts.Items[idx] = new ComboBoxItem() { Content = layoutTemplate };
                }
                else
                {
                    // new entry
                    if (layoutTemplate.IsTemplate)
                        idx = cB_Layouts.Items.IndexOf(cB_LayoutsSplitterTemplates) + 1;
                    else
                        idx = cB_Layouts.Items.IndexOf(cB_LayoutsSplitterCommunity) + 1;

                    cB_Layouts.Items.Insert(idx, new ComboBoxItem() { Content = layoutTemplate });
                }

                cB_Layouts.Items.Refresh();
                cB_Layouts.InvalidateVisual();
            });
        }

        private void SettingsManager_SettingValueChanged(string? name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "LayoutFilterOnDevice":
                        CheckBoxDeviceLayouts.IsChecked = Convert.ToBoolean(value);
                        RefreshLayoutList();
                        break;
                }
            });
        }

        private void RefreshLayoutList()
        {
            // Get filter settings
            bool FilterOnDevice = SettingsManager.GetBoolean("LayoutFilterOnDevice");

            // Get current controller
            IController controller = ControllerManager.GetTargetController();

            foreach (LayoutTemplate layoutTemplate in LayoutManager.Templates)
            {
                // get parent
                ComboBoxItem parent = layoutTemplate.Parent as ComboBoxItem;
                if (parent is null)
                    continue;

                if (layoutTemplate.ControllerType is not null && FilterOnDevice)
                {
                    if (layoutTemplate.ControllerType != controller.GetType())
                    {
                        parent.Visibility = Visibility.Collapsed;
                        continue;
                    }
                }

                parent.Visibility = Visibility.Visible;
            }
        }

        private void ButtonMapping_Deleted(ButtonFlags button)
        {
            if (Monitor.IsEntered(updateLock))
                return;

            currentLayout.RemoveLayout(button);
        }

        private void ButtonMapping_Updated(ButtonFlags button, IActions action)
        {
            if (Monitor.IsEntered(updateLock))
                return;

            currentLayout.UpdateLayout(button, action);
        }

        private void AxisMapping_Deleted(AxisLayoutFlags axis)
        {
            if (Monitor.IsEntered(updateLock))
                return;

            currentLayout.RemoveLayout(axis);
        }

        private void AxisMapping_Updated(AxisLayoutFlags axis, IActions action)
        {
            if (Monitor.IsEntered(updateLock))
                return;

            currentLayout.UpdateLayout(axis, action);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        public void UpdateLayout(LayoutTemplate layoutTemplate)
        {
            // update current layout
            currentTemplate = layoutTemplate;
            currentLayout = layoutTemplate.Layout;

            LayoutPickerPanel.Visibility = currentTemplate.IsTemplate ? Visibility.Collapsed : Visibility.Visible;

            UpdatePages();
        }

        private void UpdatePages()
        {
            if (Monitor.TryEnter(updateLock))
            {
                // cascade update to (sub)pages (async)
                Parallel.ForEach(_pages.Values, new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, page =>
                {
                    page.Refresh(currentLayout.ButtonLayout, currentLayout.AxisLayout);
                });

                // clear layout selection
                cB_Layouts.SelectedValue = null;

                Monitor.Exit(updateLock);

                currentLayout.UpdateLayout();
            }
        }

        #region UI
        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is not null)
            {
                NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
                string navItemTag = (string)navItem.Tag;

                switch (navItemTag)
                {
                    default:
                        preNavItemTag = navItemTag;
                        break;
                }

                NavView_Navigate(preNavItemTag);
            }
        }

        public void NavView_Navigate(string navItemTag)
        {
            var item = _pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
            Page _page = item.Value;

            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            var preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(_page is null) && !Type.Equals(preNavPageType, _page))
            {
                NavView_Navigate(_page);
            }
        }

        public void NavView_Navigate(Page _page)
        {
            ContentFrame.Navigate(_page);
        }

        private void navView_Loaded(object sender, RoutedEventArgs e)
        {
            // Add handler for ContentFrame navigation.
            ContentFrame.Navigated += On_Navigated;

            // NavView doesn't load any page by default, so load home page.
            navView.SelectedItem = navView.MenuItems[0];

            // If navigation occurs on SelectionChanged, this isn't needed.
            // Because we use ItemInvoked to navigate, we need to call Navigate
            // here to load the home page.
            preNavItemTag = "ButtonsPage";
            NavView_Navigate(preNavItemTag);
        }

        private void On_Navigated(object sender, NavigationEventArgs e)
        {
            navView.IsBackEnabled = ContentFrame.CanGoBack;

            if (ContentFrame.SourcePageType is not null)
            {
                var preNavPageType = ContentFrame.CurrentSourcePageType;
                var preNavPageName = preNavPageType.Name;

                var NavViewItem = navView.MenuItems
                    .OfType<NavigationViewItem>()
                    .Where(n => n.Tag.Equals(preNavPageName)).FirstOrDefault();

                if (!(NavViewItem is null))
                    navView.SelectedItem = NavViewItem;
            }
        }
        #endregion

        private void cB_Layouts_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            foreach (object item in comboBox.Items)
            {
                if (item.GetType() != typeof(ComboBoxItem))
                    continue;

                ComboBoxItem layoutTemplate = (ComboBoxItem)item;
                layoutTemplate.Width = comboBox.ActualWidth;
                layoutTemplate.InvalidateVisual();
            }
        }

        private async void ButtonApplyLayout_Click(object sender, RoutedEventArgs e)
        {
            if (cB_Layouts.SelectedItem is null)
                return;

            if (cB_Layouts.SelectedItem.GetType() != typeof(ComboBoxItem))
                return;

            // get parent
            ComboBoxItem parent = cB_Layouts.SelectedItem as ComboBoxItem;

            if (parent.Content.GetType() != typeof(LayoutTemplate))
                return;

            // get template
            LayoutTemplate layoutTemplate = (LayoutTemplate)parent.Content;

            Task<ContentDialogResult> result = Dialog.ShowAsync(
                String.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, currentTemplate.Name),
                String.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, currentTemplate.Name),
                ContentDialogButton.Primary,
                $"{Properties.Resources.ProfilesPage_Cancel}",
                $"{Properties.Resources.ProfilesPage_Yes}");

            await result; // sync call

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    {
                        // update layout
                        var newLayout = layoutTemplate.Layout.Clone() as Layout;
                        currentLayout.AxisLayout = newLayout.AxisLayout;
                        currentLayout.ButtonLayout = newLayout.ButtonLayout;

                        // update template
                        currentTemplate.Name = layoutTemplate.Name;
                        currentTemplate.Description = layoutTemplate.Description;
                        currentTemplate.Guid = layoutTemplate.Guid; // not needed

                        ProfilesPage.currentProfile.LayoutTitle = currentTemplate.Name;

                        UpdatePages();
                    }
                    break;
            }
        }

        private void cB_Layouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonApplyLayout.IsEnabled = cB_Layouts.SelectedIndex != -1;
        }

        private void CheckBoxDeviceLayouts_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("LayoutFilterOnDevice", CheckBoxDeviceLayouts.IsChecked);
        }

        private void LayoutExportButton_Click(object sender, RoutedEventArgs e)
        {
            LayoutTemplate newLayout = new()
            {
                Layout = currentLayout,
                Name = LayoutTitle.Text,
                Description = LayoutDescription.Text,
                Author = LayoutAuthor.Text,
                Executable = currentTemplate.Executable,
                Product = ProfilesPage.currentProfile.Name,
                IsCommunity = true,
                IsTemplate = false
            };

            if ((bool)CheckBoxDeviceLayouts.IsChecked)
                newLayout.ControllerType = ControllerManager.GetTargetController().GetType();
            
            /* System.Windows.Forms.SaveFileDialog saveFileDialog = new()
            {
                FileName = $"{newLayout.Name}_{newLayout.Product}_{newLayout.Author}",
                AddExtension = true,
                DefaultExt = "json",
                Filter = "Layout Files (*.json)|*.json",
                InitialDirectory = LayoutManager.InstallPath,
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) */
            LayoutManager.SerializeLayoutTemplate(newLayout);

            // close flyout
            LayoutFlyout.Hide();

            // display message
            // todo: localize me
            _ = Dialog.ShowAsync("Layout template exported",
                             $"{currentTemplate.Name} was exported.",
                             ContentDialogButton.Primary, null, $"{Properties.Resources.ProfilesPage_OK}");
        }

        private void Flyout_Opening(object sender, object e)
        {
            // manage visibility
            LayoutTitle.Text = $"{currentTemplate.Name} - {currentTemplate.Product}";
            LayoutDescription.Text = currentTemplate.Description;
            LayoutAuthor.Text = currentTemplate.Author;
        }

        private void LayoutCancelButton_Click(object sender, RoutedEventArgs e)
        {
            LayoutFlyout.Hide();
        }
    }
}
