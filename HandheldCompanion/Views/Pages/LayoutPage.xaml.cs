using ControllerCommon.Actions;
using ControllerCommon.Devices;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages.Profiles.Controller;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Layout = ControllerCommon.Layout;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles
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

        private Dictionary<string, Page> _pages;

        private string preNavItemTag;

        private Layout currentLayout = new();

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

            foreach (ButtonMapping buttonMapping in buttonsPage.Mapping.Values.Union(dpadPage.Mapping.Values).Union(triggersPage.MappingButtons.Values).Union(joysticksPage.MappingButtons.Values).Union(trackpadsPage.MappingButtons.Values))
            {
                buttonMapping.Updated += (sender, action) => ButtonMapping_Updated((ButtonFlags)sender, action);
                buttonMapping.Deleted += (sender) => ButtonMapping_Deleted((ButtonFlags)sender);
            }

            foreach (TriggerMapping AxisMapping in triggersPage.MappingAxis.Values)
            {
                AxisMapping.Updated += (sender, action) => AxisMapping_Updated((AxisLayoutFlags)sender, action);
                AxisMapping.Deleted += (sender) => AxisMapping_Deleted((AxisLayoutFlags)sender);
            }

            foreach (AxisMapping axisMapping in joysticksPage.MappingAxis.Values.Union(trackpadsPage.MappingAxis.Values))
            {
                axisMapping.Updated += (sender, action) => AxisMapping_Updated((AxisLayoutFlags)sender, action);
                axisMapping.Deleted += (sender) => AxisMapping_Deleted((AxisLayoutFlags)sender);
            }

            LayoutManager.Initialized += LayoutManager_Initialized;
        }

        private void LayoutManager_Initialized()
        {
            int idx = cB_Layouts.Items.IndexOf(cB_LayoutsSplitterTemplates);

            foreach (LayoutTemplate layoutTemplate in LayoutManager.LayoutTemplates.Values)
            {
                idx++;
                cB_Layouts.Items.Insert(idx, layoutTemplate);
            }

            // todo: implement community layout support
        }

        private void ButtonMapping_Deleted(ButtonFlags button)
        {
            currentLayout.RemoveLayout(button);
        }

        private void ButtonMapping_Updated(ButtonFlags button, IActions action)
        {
            currentLayout.UpdateLayout(button, action);
        }

        private void AxisMapping_Deleted(AxisLayoutFlags axis)
        {
            currentLayout.RemoveLayout(axis);
        }

        private void AxisMapping_Updated(AxisLayoutFlags axis, IActions action)
        {
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
            currentLayout = layoutTemplate.Layout;

            // manage visibility
            LayoutPickerPanel.Visibility = layoutTemplate.IsTemplate ? Visibility.Collapsed : Visibility.Visible;

            RefreshLayout();
        }

        private void RefreshLayout()
        {
            // cascade update to (sub)pages
            buttonsPage.Refresh(currentLayout.ButtonLayout);
            dpadPage.Refresh(currentLayout.ButtonLayout);
            joysticksPage.Refresh(currentLayout.ButtonLayout, currentLayout.AxisLayout);
            triggersPage.Refresh(currentLayout.ButtonLayout, currentLayout.AxisLayout);
            trackpadsPage.Refresh(currentLayout.ButtonLayout, currentLayout.AxisLayout);

            // clear layout selection
            cB_Layouts.SelectedIndex = -1;
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

                ComboBoxItem comboBoxItem = (ComboBoxItem)item;
                comboBoxItem.Width = comboBox.ActualWidth - 30;
                comboBoxItem.InvalidateVisual();
            }
        }

        private async void ButtonApplyLayout_Click(object sender, RoutedEventArgs e)
        {
            if (cB_Layouts.SelectedItem is null)
                return;

            // TEMPORARY
            string temp = cB_Layouts.SelectedItem.ToString();
            LayoutTemplate layoutTemplate = LayoutManager.LayoutTemplates[temp];

            Task<ContentDialogResult> result = Dialog.ShowAsync(
                String.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, layoutTemplate.Name),
                String.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, layoutTemplate.Name),
                ContentDialogButton.Primary,
                $"{Properties.Resources.ProfilesPage_Cancel}",
                $"{Properties.Resources.ProfilesPage_Yes}");

            await result; // sync call

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    {
                        // update layout
                        currentLayout.AxisLayout = layoutTemplate.Layout.AxisLayout;
                        currentLayout.ButtonLayout = layoutTemplate.Layout.ButtonLayout;

                        RefreshLayout();
                    }
                    break;
            }
        }

        private void ButtonLayoutSettings_Click(object sender, RoutedEventArgs e)
        {
            // implement me
        }

        private void cB_Layouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonApplyLayout.IsEnabled = cB_Layouts.SelectedIndex != -1;
        }
    }
}
