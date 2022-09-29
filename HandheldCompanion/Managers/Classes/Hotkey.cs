using ControllerCommon.Utils;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Windows.ApplicationModel.Contacts;
using static System.Net.Mime.MediaTypeNames;

namespace HandheldCompanion.Managers.Classes
{
    public class Hotkey
    {
        public InputsHotkey hotkey { get; set; }
        public InputsChord chord { get; set; }

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
        public Button buttonButton;

        public Hotkey()
        {
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
                Tag = hotkey.GetId()
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
                Height = 40,
                Glyph = hotkey.GetGlyph(),
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
                Text = hotkey.GetName(),
                FontSize = 14
            };

            contentDesc = new TextBlock()
            {
                Text = hotkey.GetDescription(),
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
                Height = 40
            };

            buttonButton = new Button()
            {
                Width = 200,
                Height = 30
            };

            UpdateButton();

            // add elements to main panel
            buttonPanel.Children.Add(buttonIcon);
            buttonPanel.Children.Add(buttonButton);

            // add elements to main panel
            mainPanel.Children.Add(currentIcon);
            mainPanel.Children.Add(contentPanel);

            // add elements to grid
            mainGrid.Children.Add(mainPanel);
            mainGrid.Children.Add(buttonPanel);

            // add elements to border
            mainBorder.Child = mainGrid;
        }

        public Border GetBorder()
        {
            return mainBorder;
        }

        public Button GetButton()
        {
            return buttonButton;
        }

        public void UpdateButton()
        {
            switch (chord.type)
            {
                default:
                case InputsChordType.None:
                    buttonButton.Content = "";
                    break;
                case InputsChordType.Gamepad:
                    buttonButton.Content = EnumUtils.GetDescriptionFromEnumValue(chord.buttons);
                    break;
                case InputsChordType.Keyboard:
                    // todo, display custom button name instead
                    buttonButton.Content = string.Join(", ", chord.name);
                    break;
            }

            buttonIcon.Glyph = TriggerTypeToGlyph(chord.type);
        }

        public static string TriggerTypeToGlyph(InputsChordType type)
        {
            switch (type)
            {
                default:
                case InputsChordType.None:
                    return "\uE9CE";
                case InputsChordType.Gamepad:
                    return "\uE7FC";
                case InputsChordType.Keyboard:
                    return "\uE765";
            }
        }
    }
}
