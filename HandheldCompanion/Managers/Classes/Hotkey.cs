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

        // serialized
        public ushort hotkeyId { get; set; }
        public InputsChord inputsChord { get; set; }
        public bool IsPinned { get; set; }

        // HotkeysPage UI
        public Border mainBorder;
        public Grid mainGrid = new();
        public DockPanel mainPanel = new();
        public FontIcon currentIcon;
        public SimpleStackPanel contentPanel;
        public TextBlock contentName;
        public TextBlock contentDesc;
        public SimpleStackPanel buttonPanel;
        public Button inputButton;
        public Button outputButton;
        public Button eraseButton;
        public Button pinButton;

        // QuickSettingsPage UI
        public SimpleStackPanel quickPanel;
        public Button quickButton;
        public FontIcon quickIcon;
        public TextBlock quickName;

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
                Width = new GridLength(50, GridUnitType.Pixel)
            };
            mainGrid.ColumnDefinitions.Add(colDef2);

            ColumnDefinition colDef3 = new ColumnDefinition()
            {
                Width = new GridLength(50, GridUnitType.Pixel)
            };
            mainGrid.ColumnDefinitions.Add(colDef3);

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

            inputButton = new Button()
            {
                Tag = "Chord",
                MinWidth = 200,
                FontSize = 12,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // todo: add localized tooltip text
            eraseButton = new Button()
            {
                Height = 30,
                Content = new FontIcon() { Glyph = "\uE75C", FontSize = 14 },
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = Application.Current.FindResource("AccentButtonStyle") as Style
            };
            eraseButton.Click += (sender, e) => ClearButton_Click();
            Grid.SetColumn(eraseButton, 2);

            // todo: add localized tooltip text
            pinButton = new Button()
            {
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(pinButton, 3);

            // add elements to main panel
            buttonPanel.Children.Add(inputButton);

            outputButton = new Button()
            {
                Tag = "Combo",
                MinWidth = 200,
                FontSize = 12,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            switch (inputsHotkey.hotkeyType)
            {
                case InputsHotkey.InputsHotkeyType.Custom:
                    buttonPanel.Children.Add(outputButton);
                    break;
            }

            // add elements to main panel
            mainPanel.Children.Add(currentIcon);
            mainPanel.Children.Add(contentPanel);

            // add elements to grid
            mainGrid.Children.Add(mainPanel);
            mainGrid.Children.Add(buttonPanel);
            mainGrid.Children.Add(eraseButton);
            mainGrid.Children.Add(pinButton);

            // add elements to border
            mainBorder.Child = mainGrid;

            // draw quick buttons
            quickPanel = new SimpleStackPanel() { Spacing = 6 };

            quickIcon = new FontIcon()
            {
                Height = 40,
                FontFamily = inputsHotkey.fontFamily,
                FontSize = inputsHotkey.fontSize,
                Glyph = inputsHotkey.Glyph
            };

            quickButton = new Button()
            {
                Content = quickIcon,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            quickName = new TextBlock()
            {
                Text = inputsHotkey.GetName(),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };

            quickPanel.Children.Add(quickButton);
            quickPanel.Children.Add(quickName);

            // update buttons name and states
            UpdateHotkey();
        }

        private void ClearButton_Click()
        {
            InputsManager.ClearListening(this);
        }

        public void StartListening(bool IsCombo)
        {
            // update button
            switch (IsCombo)
            {
                case true:
                    outputButton.Content = Properties.Resources.OverlayPage_Listening;
                    outputButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    inputButton.Content = Properties.Resources.OverlayPage_Listening;
                    inputButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
            }
        }

        public void StopListening(InputsChord inputsChord, bool IsCombo)
        {
            this.inputsChord = inputsChord;

            // update button
            switch (IsCombo)
            {
                case true:
                    outputButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
                case false:
                    inputButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }

            UpdateHotkey();
        }

        public void StartPinning()
        {
            IsPinned = true;

            UpdateHotkey();
        }

        public void StopPinning()
        {
            IsPinned = false;

            UpdateHotkey();
        }

        public Border GetHotkey()
        {
            return mainBorder;
        }

        public SimpleStackPanel GetPin()
        {
            return quickPanel;
        }

        private void UpdateHotkey()
        {
            bool haskey = !string.IsNullOrEmpty(inputsChord.SpecialKey);
            bool hasbuttons = (inputsChord.GamepadButtons != SharpDX.XInput.GamepadButtonFlags.None);
            bool hascombo = inputsChord.OutputKeys.Count != 0;

            string buttons = EnumUtils.GetDescriptionFromEnumValue(inputsChord.GamepadButtons);
            string combo = string.Join(", ", inputsChord.OutputKeys.Where(key => key.IsKeyDown));

            if (outputButton != null)
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
                outputButton.Content = comboContent;
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

            // update main button content
            inputButton.Content = mainContent;

            // update delete button status
            eraseButton.IsEnabled = haskey || hasbuttons || hascombo;

            // update pin button
            switch (IsPinned)
            {
                case true:
                    pinButton.Content = new FontIcon() { Glyph = "\uE77A", FontSize = 14 };
                    pinButton.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
                    break;
                case false:
                    pinButton.Content = new FontIcon() { Glyph = "\uE840", FontSize = 14 };
                    pinButton.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
                    break;
            }
        }
    }
}
