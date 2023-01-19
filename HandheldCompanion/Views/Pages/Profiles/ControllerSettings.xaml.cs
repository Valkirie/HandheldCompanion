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

namespace HandheldCompanion.Views.Pages.Profiles
{
    /// <summary>
    /// Interaction logic for ControllerSettings.xaml
    /// </summary>
    public partial class ControllerSettings : Page
    {
        private Profile currentProfile;
        private Hotkey ProfilesPageHotkey;

        public ControllerSettings()
        {
            InitializeComponent();
        }

        public ControllerSettings(string Tag) : this()
        {
            this.Tag = Tag;

            PipeClient.ServerMessage += OnServerMessage;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            PipeClient.ServerMessage -= OnServerMessage;
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }
    }
}
