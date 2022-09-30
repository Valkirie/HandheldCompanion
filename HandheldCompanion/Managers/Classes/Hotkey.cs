using ControllerCommon.Processor;
using ControllerCommon.Utils;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Windows.ApplicationModel.Contacts;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;

namespace HandheldCompanion.Managers.Classes
{
    public class Hotkey
    {
        public InputsHotkey inputsHotkey;
        public ushort hotkeyId { get; set; }

        public InputsChord inputsChord { get; set; }

        // UI vars
        public Border mainBorder;
        public Grid mainGrid;
        public DockPanel mainPanel;

        public FontIcon currentIcon;

        public SimpleStackPanel contentPanel;
        public TextBlock contentName;
        public TextBlock contentDesc;

        public SimpleStackPanel buttonPanel;
        public FontIcon buttonIcon;
        public Button mainButton;

        public Button deleteButton;

        public Hotkey()
        {
        }

        public Hotkey(ushort id, InputsHotkey inputsHotkey)
        {
            hotkeyId = id;
            this.inputsHotkey = inputsHotkey;
            inputsChord = new();
        }

        public Hotkey(ushort id)
        {
            hotkeyId = id;
            this.inputsHotkey = InputsHotkey.Hotkeys[id];
            inputsChord = new();
        }

        public void DrawControl()
        {
            if (mainBorder != null)
                return;

            // create main border
            mainBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Visibility = Visibility.Visible,
                Tag = hotkeyId
            };
            mainBorder.SetResourceReference(Control.BackgroundProperty, "SystemControlBackgroundChromeMediumLowBrush");

            // create main grid
            mainGrid = new();

            // Define the Columns
            ColumnDefinition colDef0 = new ColumnDefinition()
            {
                Width = new GridLength(6, GridUnitType.Star),
                MinWidth = 200
            };
            mainGrid.ColumnDefinitions.Add(colDef0);

            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(4, GridUnitType.Star),
                MinWidth = 200
            };
            mainGrid.ColumnDefinitions.Add(colDef1);

            // create main panel
            mainPanel = new DockPanel();

            currentIcon = new FontIcon()
            {
                Height = 30,
                FontFamily = inputsHotkey.fontFamily,
                Glyph = inputsHotkey.GetGlyph(),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // create content panel
            contentPanel = new SimpleStackPanel()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            contentName = new TextBlock()
            {
                Text = inputsHotkey.GetName(),
                FontSize = 14
            };

            contentDesc = new TextBlock()
            {
                Text = inputsHotkey.GetDescription(),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
            contentDesc.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

            // add elements to content panel
            contentPanel.Children.Add(contentName);
            contentPanel.Children.Add(contentDesc);

            buttonPanel = new SimpleStackPanel()
            {
                Spacing = 12,
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(buttonPanel, 1);

            buttonIcon = new FontIcon()
            {
                Height = 30
            };

            mainButton = new Button()
            {
                Width = 200,
                Height = 30
            };
            mainButton.Click += ButtonButton_Click;

            deleteButton = new Button()
            {
                Height = 30,
                Content = new FontIcon() { Glyph = "\uE75C", FontSize = 14 }
            };
            deleteButton.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            deleteButton.Click += DeleteButton_Click;

            // add elements to main panel
            buttonPanel.Children.Add(buttonIcon);
            buttonPanel.Children.Add(mainButton);
            buttonPanel.Children.Add(deleteButton);

            // add elements to main panel
            mainPanel.Children.Add(currentIcon);
            mainPanel.Children.Add(contentPanel);

            // add elements to grid
            mainGrid.Children.Add(mainPanel);
            mainGrid.Children.Add(buttonPanel);

            // add elements to border
            mainBorder.Child = mainGrid;

            // update buttons name and states
            UpdateButtons();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            InputsManager.ClearListening(inputsHotkey.Listener);
        }

        private void ButtonButton_Click(object sender, RoutedEventArgs e)
        {
            InputsManager.StartListening(inputsHotkey.Listener);

            // update button text
            mainButton.Content = Properties.Resources.OverlayPage_Listening;

            // update buton style
            mainButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
        }

        public Border GetBorder()
        {
            return mainBorder;
        }

        public Button GetMainButton()
        {
            return mainButton;
        }

        public Button GetDeleteButton()
        {
            return deleteButton;
        }

        public void UpdateButtons()
        {
            switch (inputsChord.type)
            {
                default:
                case InputsChordType.None:
                    mainButton.Content = "";
                    buttonIcon.Glyph = "\uE9CE";
                    deleteButton.IsEnabled = false;
                    break;
                case InputsChordType.Gamepad:
                    mainButton.Content = EnumUtils.GetDescriptionFromEnumValue(inputsChord.buttons);
                    buttonIcon.Glyph = "\uE7FC";
                    deleteButton.IsEnabled = true;
                    break;
                case InputsChordType.Keyboard:
                    mainButton.Content = string.Join(", ", inputsChord.name);
                    buttonIcon.Glyph = "\uE765";
                    deleteButton.IsEnabled = true;
                    break;
            }
        }
    }
}
