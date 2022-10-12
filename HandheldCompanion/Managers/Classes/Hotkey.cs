﻿using ControllerCommon.Utils;
using ModernWpf.Controls;
using System.Windows;
using System.Windows.Controls;
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
                MinWidth = 360
            };
            mainGrid.ColumnDefinitions.Add(colDef1);

            // create main panel
            mainPanel = new DockPanel();

            currentIcon = new FontIcon()
            {
                Height = 30,
                FontFamily = inputsHotkey.fontFamily,
                FontSize = inputsHotkey.fontSize,
                Glyph = inputsHotkey.Glyph
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

            mainButton = new Button()
            {
                MinWidth = 200,
                MaxWidth = 300,
                FontSize = 12,
                Height = 30
            };
            mainButton.Click += (sender, e) => ButtonButton_Click((Button)sender);

            deleteButton = new Button()
            {
                Height = 30,
                Content = new FontIcon() { Glyph = "\uE75C", FontSize = 14 },
                Style = Application.Current.FindResource("AccentButtonStyle") as Style
            };
            deleteButton.Click += (sender, e) => DeleteButton_Click();

            switch (inputsHotkey.hotkeyType)
            {
                case InputsHotkey.InputsHotkeyType.Custom:
                    {
                        var subButton = new Button()
                        {
                            MinWidth = 100,
                            MaxWidth = 200,
                            FontSize = 12,
                            Height = 30
                        };
                        subButton.Click += (sender, e) => ButtonButton_Click((Button)sender);

                        buttonPanel.Children.Add(subButton);
                    }
                break;
            }

            // add elements to main panel
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

        private void DeleteButton_Click()
        {
            InputsManager.ClearListening(inputsHotkey);
        }

        private void ButtonButton_Click(Button sender)
        {
            InputsManager.StartListening(inputsHotkey);

            // update button text
            sender.Content = Properties.Resources.OverlayPage_Listening;

            // update buton style
            sender.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
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
            SimpleStackPanel content = new() { Orientation = Orientation.Horizontal, Spacing = 6 };

            bool haskey = !string.IsNullOrEmpty(inputsChord.key);
            bool hasbuttons = (inputsChord.buttons != SharpDX.XInput.GamepadButtonFlags.None);

            string buttons = EnumUtils.GetDescriptionFromEnumValue(inputsChord.buttons);

            if (haskey && hasbuttons)
                content.Children.Add(new TextBlock() { Text = string.Join(" + ", inputsChord.key, buttons) });
            else if (haskey)
                content.Children.Add(new TextBlock() { Text = inputsChord.key });
            else if (hasbuttons)
                content.Children.Add(new TextBlock() { Text = buttons });

            if (content.Children.Count > 0)
            {
                TextBlock type = new TextBlock() { Text = inputsChord.type.ToString() };
                type.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");

                content.Children.Add(type);
            }

            mainButton.Content = content;

            deleteButton.IsEnabled = true;
        }
    }
}
