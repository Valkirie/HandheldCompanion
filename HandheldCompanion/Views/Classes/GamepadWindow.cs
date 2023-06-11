using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ControllerCommon.Utils;

namespace HandheldCompanion.Views.Classes
{
    public class GamepadWindow : Window
    {
        public List<Control> elements = new();

        public GamepadWindow()
        {
            LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            // Track when objects are added and removed
            if (visualAdded != null && visualAdded.GetType() == typeof(Control))
                elements.Add((Control)visualAdded);

            if (visualRemoved != null && visualRemoved.GetType() == typeof(Control))
                elements.Remove((Control)visualRemoved);

            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            elements = WPFUtils.FindChildren(this);
        }
    }
}
