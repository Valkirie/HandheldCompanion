using CommunityToolkit.Labs.WinUI;
using HandheldCompanion.UWP.Views.Pages;
using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace HandheldCompanion.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly Dictionary<string, Type> _pages;
        private readonly ResourceDictionary _compactResource;

        public MainPage()
        {
            _pages = new Dictionary<string, Type>
            {
                { "PerformancePage", typeof(PerformancePage) },
            };
            this.InitializeComponent();
            _compactResource = new ResourceDictionary
            {
                Source = new Uri("ms-appx:///Styles/CompactMode.xaml")
            };
            NavView_Navigate(_pages.First().Key);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var widget = e.Parameter as XboxGameBarWidget;
            if (widget != null)
            {
                SwitchCompactMode(widget.CompactModeEnabled);
                widget.CompactModeEnabledChanged += Widget_CompactModeEnabledChanged;
            }
            base.OnNavigatedTo(e);
        }

        private void Widget_CompactModeEnabledChanged(XboxGameBarWidget widget, object _)
        {
            bool enabled = widget.CompactModeEnabled;
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                SwitchCompactMode(widget.CompactModeEnabled);
                // Change theme to force reload ThemeResource
                var lastTheme = this.RequestedTheme;
                this.RequestedTheme = ElementTheme.Light;
                this.RequestedTheme = ElementTheme.Dark;
                this.RequestedTheme = lastTheme;
            });
        }

        private void SwitchCompactMode(bool enabled)
        {
            if (enabled)
            {
                if (!this.Resources.MergedDictionaries.Contains(_compactResource))
                    this.Resources.MergedDictionaries.Add(_compactResource);
            }
            else
                this.Resources.MergedDictionaries.Remove(_compactResource);
        }

        private void NavView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var targetPage = e.AddedItems.OfType<TokenItem>().FirstOrDefault()?.Tag as string;
            if (!string.IsNullOrEmpty(targetPage))
            {
                NavView_Navigate(targetPage);
            }
        }

        private void NavView_Navigate(string navItemTag)
        {
            NavView.SelectedItem = NavView.Items
                .OfType<TokenItem>()
                .FirstOrDefault(_item => _item.Tag?.ToString() == navItemTag);

            KeyValuePair<string, Type> item = _pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
            Type _pageType = item.Value;

            // Get the page type before navigation so you can prevent duplicate
            // entries in the backstack.
            Type preNavPageType = ContentFrame.CurrentSourcePageType;

            // Only navigate if the selected page isn't currently loaded.
            if (!(_pageType is null) && !Equals(preNavPageType, _pageType))
                NavView_Navigate(_pageType);
        }

        public void NavView_Navigate(Type pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
