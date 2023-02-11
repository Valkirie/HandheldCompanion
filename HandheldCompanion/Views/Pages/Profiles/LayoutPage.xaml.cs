using ControllerCommon;
using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerService.Sensors;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages.Profiles.Controller;
using HandheldCompanion.Views.Windows;
using Microsoft.Win32.TaskScheduler;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Windows.Networking.NetworkOperators;
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

            // create controller related pages
            this._pages = new()
            {
                // buttons
                { "ButtonsPage", buttonsPage },
                { "DpadPage", dpadPage },

                // axis
                { "TriggersPage", triggersPage },
                { "JoysticksPage", joysticksPage },

                // gyro
                { "GyroPage", gyroPage },
            };

            foreach (ButtonMapping buttonMapping in buttonsPage.Mapping.Values.Union(dpadPage.Mapping.Values).Union(joysticksPage.MappingButtons.Values))
            {
                buttonMapping.Updated += ButtonMapping_Updated;
                buttonMapping.Deleted += ButtonMapping_Deleted;
            }

            foreach (AxisMapping axisMapping in joysticksPage.MappingAxis.Values)
            {
                axisMapping.Updated += AxisMapping_Updated;
                axisMapping.Deleted += AxisMapping_Deleted;
            }
        }

        private void ButtonMapping_Deleted(ButtonFlags button)
        {
            currentLayout.RemoveLayout(button);
        }

        private void ButtonMapping_Updated(ButtonFlags button, IActions action)
        {
            currentLayout.UpdateLayout(button, action);
        }

        private void AxisMapping_Deleted(AxisFlags axis)
        {
            currentLayout.RemoveLayout(axis);
        }

        private void AxisMapping_Updated(AxisFlags axis, IActions action)
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

        public void UpdateLayout(Layout layout)
        {
            // update current layout
            currentLayout = layout;
            currentLayout.Updated += CurrentLayout_Updated;

            // cascade update to (sub)pages
            buttonsPage.Refresh(currentLayout.ButtonLayout);
            dpadPage.Refresh(currentLayout.ButtonLayout);

            joysticksPage.Refresh(currentLayout.ButtonLayout, currentLayout.AxisLayout);
        }

        private void CurrentLayout_Updated(Layout layout)
        {
            // do something
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
    }
}
