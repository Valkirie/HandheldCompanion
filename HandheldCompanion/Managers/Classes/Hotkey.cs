using ControllerCommon.Utils;
using ModernWpf.Controls;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;

namespace HandheldCompanion.Managers.Classes
{
    public class Hotkey
    {
        // not serialized
        public InputsHotkey inputsHotkey;
        public bool IsCombo;

        // serialized
        public ushort hotkeyId { get; set; }
        public InputsChord inputsChord { get; set; }

        // UI vars
        public Border mainBorder;
        public Grid mainGrid = new();
        public DockPanel mainPanel = new();
        public FontIcon currentIcon;
        public SimpleStackPanel contentPanel;
        public TextBlock contentName;
        public TextBlock contentDesc;
        public SimpleStackPanel buttonPanel;

        public Button mainButton;
        public Button deleteButton;
        public Button comboButton;

        public Hotkey()
        {
        }

        public Hotkey(ushort id, InputsHotkey _inputsHotkey)
        {
            hotkeyId = id;

            inputsHotkey = _inputsHotkey;
            inputsChord = new();
        }

        public Hotkey(ushort id)
        {
            hotkeyId = id;

            inputsHotkey = InputsHotkey.Hotkeys[id];
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

            // main grid content
            // Define the Columns
            ColumnDefinition colDef0 = new ColumnDefinition()
            {
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            mainGrid.ColumnDefinitions.Add(colDef0);

            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            mainGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(60, GridUnitType.Pixel)
            };
            mainGrid.ColumnDefinitions.Add(colDef2);

            // main panel content
            currentIcon = new FontIcon()
            {
                Height = 40,
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
                TextWrapping = TextWrapping.Wrap,
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
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(buttonPanel, 1);

            mainButton = new Button()
            {
                Tag = "Chord",
                MinWidth = 200,
                FontSize = 12,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            mainButton.Click += (sender, e) => ButtonButton_Click((Button)sender);

            deleteButton = new Button()
            {
                Height = 30,
                Content = new FontIcon() { Glyph = "\uE75C", FontSize = 14 },
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = Application.Current.FindResource("AccentButtonStyle") as Style
            };
            deleteButton.Click += (sender, e) => DeleteButton_Click();
            Grid.SetColumn(deleteButton, 2);

            // add elements to main panel
            buttonPanel.Children.Add(mainButton);

            switch (inputsHotkey.hotkeyType)
            {
                case InputsHotkey.InputsHotkeyType.Custom:
                    {
                        comboButton = new Button()
                        {
                            Tag = "Combo",
                            MinWidth = 200,
                            FontSize = 12,
                            Height = 30,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                        };
                        comboButton.Click += (sender, e) => ButtonButton_Click((Button)sender);

                        buttonPanel.Children.Add(comboButton);
                    }
                    break;
            }

            // add elements to main panel
            mainPanel.Children.Add(currentIcon);
            mainPanel.Children.Add(contentPanel);

            // add elements to grid
            mainGrid.Children.Add(mainPanel);
            mainGrid.Children.Add(buttonPanel);
            mainGrid.Children.Add(deleteButton);

            // add elements to border
            mainBorder.Child = mainGrid;

            // update buttons name and states
            UpdateHotkey();
        }

        private void DeleteButton_Click()
        {
            InputsManager.ClearListening(this);
        }

        private void ButtonButton_Click(Button sender)
        {
            switch (sender.Tag)
            {
                case "Combo":
                    IsCombo = true;
                    break;
                default:
                case "Chord":
                    IsCombo = false;
                    break;
            }

            InputsManager.StartListening(this);

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

        public void UpdateHotkey(bool StopListening = false)
        {
            if (StopListening)
            {
                // restore default style
                switch (IsCombo)
                {
                    case true:
                        comboButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                        break;
                    default:
                    case false:
                        mainButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                        break;
                }
            }

            bool haskey = !string.IsNullOrEmpty(inputsChord.SpecialKey);
            bool hasbuttons = (inputsChord.GamepadButtons != SharpDX.XInput.GamepadButtonFlags.None);
            bool hascombo = inputsChord.OutputKeys.Count != 0;

            string buttons = EnumUtils.GetDescriptionFromEnumValue(inputsChord.GamepadButtons);
            string combo = string.Join(", ", inputsChord.OutputKeys.Where(key => key.IsKeyDown));

            if (comboButton != null)
            {
                // comboContent content
                SimpleStackPanel comboContent = new()
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                };

                if (hascombo)
                {
                    comboContent.Children.Add(new TextBlock()
                    {
                        Text = combo
                    });
                }
                else
                {
                    // set fallback content
                    TextBlock fallback = new TextBlock()
                    {
                        Text = Properties.Resources.ResourceManager.GetString("InputsHotkey_fallbackOutput")
                    };
                    fallback.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                    comboContent.Children.Add(fallback);
                }

                // update button content
                comboButton.Content = comboContent;
            }

            // mainButton content
            SimpleStackPanel mainContent = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            };

            if (haskey && hasbuttons)
            {
                mainContent.Children.Add(new TextBlock()
                {
                    Text = string.Join(" + ", inputsChord.SpecialKey, buttons)
                });
            }
            else if (haskey)
            {
                mainContent.Children.Add(new TextBlock()
                {
                    Text = inputsChord.SpecialKey
                });
            }
            else if (hasbuttons)
            {
                mainContent.Children.Add(new TextBlock()
                {
                    Text = buttons
                });
            }

            // only display inputsChord type (click, hold) if inputs were captured
            if (mainContent.Children.Count > 0)
            {
                TextBlock type = new TextBlock()
                {
                    Text = inputsChord.InputsType.ToString()
                };
                type.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");

                mainContent.Children.Add(type);
            }
            else
            {
                TextBlock fallback = new TextBlock()
                {
                    Text = Properties.Resources.ResourceManager.GetString("InputsHotkey_fallbackInput")
                };
                fallback.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                // set fallback content
                mainContent.Children.Add(fallback);
            }

            // update button content
            mainButton.Content = mainContent;

            // update delete button status
            deleteButton.IsEnabled = haskey || hasbuttons || hascombo;
        }
    }
}
