using System.Windows;
using System.Windows.Controls;

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
