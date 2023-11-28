using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HandheldCompanion.Controls.Hints
{
    /// <summary>
    /// Interaction logic for IHint.xaml
    /// </summary>
    public partial class IHint : UserControl
    {
        public IHint()
        {
            InitializeComponent();
        }

        protected virtual void HintActionButton_Click(object sender, RoutedEventArgs e)
        { }

        public virtual void Stop()
        { }
    }
}
