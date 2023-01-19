using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerService.Sensors;
using HandheldCompanion.Managers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages.Profiles.Controller
{
    /// <summary>
    /// Interaction logic for ButtonsPage.xaml
    /// </summary>
    public partial class ButtonsPage : Page
    {
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey;

        public ButtonsPage()
        {
            InitializeComponent();
        }

        public ButtonsPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }
    }
}
