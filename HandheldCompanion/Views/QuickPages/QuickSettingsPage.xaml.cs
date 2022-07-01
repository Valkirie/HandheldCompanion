using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickSettingsPage.xaml
    /// </summary>
    public partial class QuickSettingsPage : Page
    {
        private bool Initialized;

        public QuickSettingsPage()
        {
            InitializeComponent();
        }

        private void Scrolllock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            QuickTools.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            QuickTools.scrollLock = false;
        }
    }
}
