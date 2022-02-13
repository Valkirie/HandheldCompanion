using ControllerCommon;
using Microsoft.Extensions.Logging;
using ModernWpf.Controls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml;
using Page = System.Windows.Controls.Page;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for Profiles.xaml
    /// </summary>
    public partial class ProfilesPage : Page
    {
        private MainWindow mainWindow;
        private ILogger microsoftLogger;

        public ProfileManager profileManager;

        // pipe vars
        PipeClient pipeClient;

        public ProfilesPage()
        {
            InitializeComponent();
        }

        public ProfilesPage(MainWindow mainWindow, ILogger microsoftLogger) : this()
        {
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = mainWindow.pipeClient;
            this.profileManager = mainWindow.profileManager;

            // initialize Profile Manager
            profileManager.Deleted += ProfileDeleted;
            profileManager.Updated += ProfileUpdated;
            profileManager.Start();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        #region UI
        public void ProfileUpdated(Profile profile)
        {
            // inform Service we have a new default profile
            if (profile.IsDefault)
                pipeClient.SendMessage(new PipeClientProfile() { profile = profile });

            this.Dispatcher.Invoke(() =>
            {
                int idx = cB_Profiles.Items.IndexOf(profile);

                foreach (Profile pr in cB_Profiles.Items)
                    if (pr.path == profile.path)
                    {
                        // IndexOf will always fail !
                        idx = cB_Profiles.Items.IndexOf(pr);
                        break;
                    }

                if (idx == -1)
                    cB_Profiles.Items.Add(profile);
                else
                    cB_Profiles.Items[idx] = profile;

                /* clone template
                string gridXaml = XamlWriter.Save(Button_Template);
                
                StringReader stringReader = new StringReader(gridXaml);
                XmlReader xmlReader = XmlReader.Create(stringReader);

                Button ProfileButton = (Button)XamlReader.Load(xmlReader);

                // update template before copy
                ProfileButton.Visibility = Visibility.Visible;
                ((TextBlock)ProfileButton.FindName("ProfileName")).Text = profile.name;
                ((TextBlock)ProfileButton.FindName("ProfilePath")).Text = profile.path;
                ((TextBlock)ProfileButton.FindName("ProfileKey")).Text = profile.name.Substring(0,1);

                StackPanel_Profiles.Children.Add(ProfileButton); */
            });
        }

        public void ProfileDeleted(Profile profile)
        {
            this.Dispatcher.Invoke(() =>
            {
                // todo
            });
        }
        #endregion

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // temp
            mainWindow.ContentFrame.Navigate(typeof(ProfilePage));
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // todo
        }
    }
}
