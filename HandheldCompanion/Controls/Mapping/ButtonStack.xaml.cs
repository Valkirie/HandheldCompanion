using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using iNKORE.UI.WPF.Modern.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Interaction logic for MappingStack.xaml
    /// </summary>
    public partial class ButtonStack : SimpleStackPanel
    {
        static Style buttonStyle = (Style)Application.Current.Resources["TabViewButtonStyle"];
        static FontFamily fontFamily = new("Segoe Fluent Icons");
        static Thickness margin = new(0, 0, 1, 0);
        static Thickness padding = new(2, 2, 0, 0);

        private ButtonFlags button;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(object sender, List<IActions> actions);
        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(object sender);

        public ButtonStack() : base()
        {
            InitializeComponent();
        }

        public ButtonStack(ButtonFlags button) : this()
        {
            this.button = button;

            // remove the xaml reference entry
            getGrid(0).Children.Clear(); Children.Clear();
            // add the first one and never remove it, only modify
            AddMappingToChildren(new ButtonMapping(button));
        }

        private Grid getGrid(int index)
        {
            return Children[index] as Grid;
        }

        private Button getButton(int index)
        {
            return getGrid(index).Children[0] as Button;
        }

        private ButtonMapping getMapping(int index)
        {
            return getGrid(index).Children[1] as ButtonMapping;
        }

        public void UpdateIcon(FontIcon newIcon, string newLabel)
        {
            getMapping(0).UpdateIcon(newIcon, newLabel);
        }

        public void UpdateSelections()
        {
            getMapping(0).UpdateSelections();
        }

        // actions cannot be null or empty
        public void SetActions(List<IActions> actions)
        {
            int mappingLen = Children.Count;
            int index = 0;
            foreach (var action in actions)
            {
                // reuse existing mappings
                if (index < mappingLen)
                    getMapping(index).SetIActions(action);
                // we need more
                else
                {
                    ButtonMapping mapping = new(button);
                    mapping.SetIActions(action);
                    AddMappingToChildren(mapping);
                }
                index++;
            }

            // if there were equal or more actions than mappings we're done
            int actionsLen = actions.Count;
            if (mappingLen <= actionsLen)
                return;

            // if there were more mappings, remove the remaining ones
            for (int i = actionsLen; i < mappingLen; i++)
                getGrid(i).Children.Clear();
            Children.RemoveRange(actionsLen, mappingLen - actionsLen);
        }

        public void Reset()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (i == 0)
                    getMapping(i).Reset();
                else
                    getGrid(i).Children.Clear();
            }
            Children.RemoveRange(1, Children.Count - 1);
        }

        private void ButtonMapping_Updated(ButtonFlags button)
        {
            List<IActions> actions = new();
            for (int i = 0; i < Children.Count; i++)
            {
                IActions action = getMapping(i).GetIActions();
                if (action is null)
                    continue;

                actions.Add(action);
            }

            if (actions.Count > 0)
                Updated?.Invoke(button, actions);
            else
                Deleted?.Invoke(button);
        }

        private void AddMappingToChildren(ButtonMapping mapping)
        {
            // doesn't matter if updated or deleted, we need to gather all active
            // we can't map those 1:1 anyway, as there can be more mappings than actions
            mapping.Updated += (sender, action) => ButtonMapping_Updated((ButtonFlags)sender);
            mapping.Deleted += (sender) => ButtonMapping_Updated((ButtonFlags)sender);

            int index = Children.Count;

            FontIcon fontIcon = new FontIcon();
            fontIcon.HorizontalAlignment = HorizontalAlignment.Center;
            fontIcon.VerticalAlignment = VerticalAlignment.Center;
            fontIcon.FontFamily = fontFamily;
            fontIcon.FontWeight = FontWeights.Bold;
            fontIcon.FontSize = 24;
            if (index == 0)
                fontIcon.Glyph = "\uECC8";
            else
                fontIcon.Glyph = "\uECC9";

            Button button = new();
            button.VerticalAlignment = VerticalAlignment.Top;
            button.Height = 48; button.Width = 48;
            button.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            button.Margin = margin;
            button.Padding = padding;  // the glyph is not centered within its box
            button.Style = buttonStyle;
            button.Tag = index;
            button.Click += Button_Click;
            button.Content = fontIcon;

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(mapping, 1);
            grid.Children.Add(button);
            grid.Children.Add(mapping);
            Children.Add(grid);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int index = (int)((Button)sender).Tag;

            // Add
            if (index == 0)
            {
                ButtonMapping mapping = new(button);
                AddMappingToChildren(mapping);
                // no need to register new mapping, it's empty, will be updated with event
            }
            // Del
            else
            {
                bool sendEvent = getMapping(index).GetIActions() is not null;

                getGrid(index).Children.Clear();
                Children.RemoveAt(index);

                // reindex remaining
                for (int i = 0; i < Children.Count; i++)
                    getButton(i).Tag = i;

                // removal of an actual action needs to be registered as it disappears without event
                if (sendEvent)
                    ButtonMapping_Updated(button);
            }
        }
    }
}