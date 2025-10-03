using CommunityToolkit.Labs.WinUI;
using HandheldCompanion.UWP.Views.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace HandheldCompanion.UWP.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly Dictionary<string, Type> _pages;

        public MainPage()
        {
            _pages = new Dictionary<string, Type>
            {
                { "PerformancePage", typeof(PerformancePage) },
            };
            this.InitializeComponent();
            NavView_Navigate(_pages.First().Key);
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
