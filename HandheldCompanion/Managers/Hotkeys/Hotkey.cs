using ControllerCommon.Controllers;
using HandheldCompanion.Controllers;
using ModernWpf.Controls;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static HandheldCompanion.Managers.InputsManager;
using Application = System.Windows.Application;

namespace HandheldCompanion.Managers
{
    public class Hotkey
    {
        [JsonIgnore]
        public InputsHotkey inputsHotkey = new();

        public ushort hotkeyId;
        public InputsChord inputsChord = new();
        public bool IsPinned;
        public string Name;

        // HotkeysPage UI
        [JsonIgnore]
        public Border mainBorder;
        [JsonIgnore]
        public Grid mainGrid = new();
        [JsonIgnore]
        public DockPanel mainPanel = new();
        [JsonIgnore]
        public FontIcon currentIcon;
        [JsonIgnore]
        public SimpleStackPanel contentPanel;

        [JsonIgnore]
        public TextBlock contentName;
        [JsonIgnore]
        public TextBox customName;
        [JsonIgnore]
        public TextBlock contentDesc;

        [JsonIgnore]
        public SimpleStackPanel buttonPanel;
        [JsonIgnore]
        public Button inputButton;
        [JsonIgnore]
        public Button outputButton;
        [JsonIgnore]
        public Button eraseButton;
        [JsonIgnore]
        public Button pinButton;

        // QuickSettingsPage UI
        [JsonIgnore]
        public SimpleStackPanel quickPanel;
        [JsonIgnore]
        public Button quickButton;
        [JsonIgnore]
        public FontIcon quickIcon;
        [JsonIgnore]
        public TextBlock quickName;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Hotkey hotkey);

        public Hotkey()
        {
        }

        public Hotkey(ushort id, InputsHotkey _inputsHotkey)
        {
            hotkeyId = id;

            inputsHotkey = _inputsHotkey;
        }

        public Hotkey(ushort id)
        {
            hotkeyId = id;

            inputsHotkey = InputsHotkey.InputsHotkeys[id];
        }

        public void DrawControl(bool embedded = false)
        {
            if (mainBorder is not null)
                return;

            // create main border
            mainBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Visibility = Visibility.Visible,
                Tag = hotkeyId
            };

            if (!embedded)
                mainBorder.SetResourceReference(Control.BackgroundProperty, "ExpanderContentBackground");

            // main grid content
            // Define the Columns
            if (!embedded)
            {
                ColumnDefinition colDef0 = new ColumnDefinition()
                {
                    Width = new GridLength(5, GridUnitType.Star),
                    MinWidth = 200
                };
                mainGrid.ColumnDefinitions.Add(colDef0);
            }

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

            if (!embedded)
            {
                ColumnDefinition colDef3 = new ColumnDefinition()
                {
                    Width = new GridLength(50, GridUnitType.Pixel)
                };
                mainGrid.ColumnDefinitions.Add(colDef3);
            }

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
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
            };

            customName = new TextBox()
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Margin = new Thickness(0, 0, 12, 0)
            };
            customName.TextChanged += (sender, e) => customName_Changed();

            contentDesc = new TextBlock()
            {
                Text = inputsHotkey.GetDescription(),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
            contentDesc.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

            buttonPanel = new SimpleStackPanel()
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (!embedded)
                Grid.SetColumn(buttonPanel, 1);
            else
                Grid.SetColumn(buttonPanel, 0);

            inputButton = new Button()
            {
                Tag = "Chord",
                MinWidth = 200,
                FontSize = 12,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
            };

            // todo: add localized tooltip text
            eraseButton = new Button()
            {
                Height = 40,
                Content = new FontIcon() { Glyph = "\uE75C", FontSize = 14 },
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = Application.Current.FindResource("AccentButtonStyle") as Style,
                ToolTip = "Reset hotkey"
            };
            eraseButton.Click += (sender, e) => ClearButton_Click();
            Grid.SetColumn(eraseButton, 2);

            // todo: add localized tooltip text
            pinButton = new Button()
            {
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Pin/unpin hotkey to quicktools action center"
            };

            if (!embedded)
                Grid.SetColumn(pinButton, 3);

            // add elements to main panel
            buttonPanel.Children.Add(inputButton);

            outputButton = new Button()
            {
                Tag = "Combo",
                MinWidth = 200,
                FontSize = 12,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // add elements to content panel
            switch (inputsHotkey.hotkeyType)
            {
                case InputsHotkey.InputsHotkeyType.Custom:
                    buttonPanel.Children.Add(outputButton);
                    contentPanel.Children.Add(customName);
                    break;
                default:
                    contentPanel.Children.Add(contentName);
                    contentPanel.Children.Add(contentDesc);
                    break;
            }

            if (!embedded)
            {
                // add elements to main panel
                mainPanel.Children.Add(currentIcon);
                mainPanel.Children.Add(contentPanel);

                // add elements to grid
                mainGrid.Children.Add(mainPanel);
            }

            mainGrid.Children.Add(buttonPanel);
            mainGrid.Children.Add(eraseButton);

            if (!embedded)
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

            // refresh name
            if (!string.IsNullOrEmpty(Name))
                quickName.Text = contentName.Text = customName.Text = Name;
            else
                quickName.Text = contentName.Text = customName.Text = inputsHotkey.GetName();

            quickPanel.Children.Add(quickButton);
            quickPanel.Children.Add(quickName);

            // update buttons name and states
            Draw();
        }

        private void customName_Changed()
        {
            this.Name = quickName.Text = customName.Text;

            Updated?.Invoke(this);
        }

        private void ClearButton_Click()
        {
            // restore default name
            customName.Text = inputsHotkey.GetName();

            HotkeysManager.ClearHotkey(this);
            InputsManager.ClearListening(this);
        }

        public void StartListening(ListenerType type)
        {
            // update button
            switch (type)
            {
                case ListenerType.Output:
                    outputButton.Content = Properties.Resources.OverlayPage_Listening;
                    outputButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    DrawOutput();
                    break;
                case ListenerType.UI:
                case ListenerType.Default:
                    inputButton.Content = Properties.Resources.OverlayPage_Listening;
                    inputButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    DrawInput();
                    break;
            }
        }

        public void StopListening(InputsChord inputsChord, ListenerType type)
        {
            this.inputsChord = inputsChord;

            // update button
            switch (type)
            {
                case ListenerType.Output:
                    outputButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    DrawOutput();
                    break;
                case ListenerType.UI:
                case ListenerType.Default:
                    inputButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    DrawInput();
                    break;
            }
        }

        public void StartPinning()
        {
            IsPinned = true;
            DrawPin();
        }

        public void StopPinning()
        {
            IsPinned = false;
            DrawPin();
        }

        public SimpleStackPanel GetButtonPanel()
        {
            return buttonPanel;
        }

        public Border GetHotkey()
        {
            return mainBorder;
        }

        public SimpleStackPanel GetPin()
        {
            return quickPanel;
        }

        private bool HasInput()
        {
            return inputsChord.State.Buttons.Any();
        }

        private bool HasOutput()
        {
            return inputsChord.OutputKeys.Any();
        }

        public void Draw()
        {
            DrawInput();
            DrawOutput();
            DrawPin();
        }

        public void DrawInput()
        {
            if (inputButton is null)
                return;

            // mainButton content
            SimpleStackPanel inputContent = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6
            };

            if (HasInput())
            {
                TextBlock inputText = new() { FontFamily = new FontFamily("PromptFont"), FontSize = 24 };

                ControllerType controllerType = ControllerManager.GetTargetControllerType();
                switch (controllerType)
                {
                    case ControllerType.XInput:
                        inputText.Text = string.Join("", inputsChord.State.Buttons.Select(XInputController.GetGlyph));
                        break;
                    default:
                        inputText.Text = string.Join("", inputsChord.State.Buttons.Select(DInputController.GetGlyph));
                        break;
                }

                inputContent.Children.Add(inputText);

                // only display inputsChord type (click, hold) if inputs were captured
                TextBlock type = new TextBlock()
                {
                    Text = inputsChord.InputsType.ToString(),
                    VerticalAlignment = VerticalAlignment.Center
                };
                type.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");

                inputContent.Children.Add(type);
            }
            else
            {
                TextBlock fallback = new TextBlock()
                {
                    Text = Properties.Resources.ResourceManager.GetString("InputsHotkey_fallbackInput")
                };
                fallback.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                // set fallback content
                inputContent.Children.Add(fallback);
            }

            // update main button content
            inputButton.Content = inputContent;

            DrawErase();
        }

        private void DrawOutput()
        {
            if (outputButton is null)
                return;

            // comboContent content
            SimpleStackPanel outputContent = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            };

            TextBlock outputText = new TextBlock();
            switch (HasOutput())
            {
                case true:
                    outputText.Text = string.Join(", ", inputsChord.OutputKeys.Where(key => key.IsKeyDown));
                    outputText.SetResourceReference(Control.ForegroundProperty, "");
                    break;
                case false:
                    outputText.Text = Properties.Resources.ResourceManager.GetString("InputsHotkey_fallbackOutput");
                    outputText.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
                    break;
            }
            outputContent.Children.Add(outputText);

            // update button content
            outputButton.Content = outputContent;

            DrawErase();
        }

        private void DrawPin()
        {
            // update pin button
            switch (IsPinned)
            {
                case true:
                    pinButton.Content = new FontIcon() { Glyph = "\uE77A", FontSize = 14 };
                    pinButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    pinButton.Content = new FontIcon() { Glyph = "\uE840", FontSize = 14 };
                    pinButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }
        }

        private void DrawErase()
        {
            // update delete button status
            eraseButton.IsEnabled = HasInput() || HasOutput();
        }

        public void Highlight()
        {
            DoubleAnimation opacityAnimation = new DoubleAnimation()
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1)
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);

            Storyboard.SetTarget(opacityAnimation, inputButton);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

            storyboard.Begin(inputButton);
        }

        public void SetToggle(bool toggle)
        {
            switch (toggle)
            {
                case true:
                    quickButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    quickButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }
        }
    }
}
