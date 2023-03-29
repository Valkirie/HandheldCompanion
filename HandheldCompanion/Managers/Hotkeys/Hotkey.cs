using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Inputs;
using HandheldCompanion.Controls;
using HandheldCompanion.Views;
using ModernWpf.Controls;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using static HandheldCompanion.Managers.InputsHotkey;
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

        public string Name;

        private bool _isPinned;
        public bool IsPinned
        {
            get
            {
                return _isPinned;
            }

            set
            {
                // update pin button
                switch (value)
                {
                    case true:
                        mainControl.HotkeyPin.Content = new FontIcon() { Glyph = "\uE77A", FontSize = 14 };
                        mainControl.HotkeyPin.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                        break;
                    case false:
                        mainControl.HotkeyPin.Content = new FontIcon() { Glyph = "\uE840", FontSize = 14 };
                        mainControl.HotkeyPin.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                        break;
                }

                _isPinned = value;
            }
        }

        private bool _isEnabled = true;
        [JsonIgnore]
        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }

            set
            {
                mainControl.IsEnabled = value;
                quickControl.IsEnabled = value;

                _isEnabled = value;
            }
        }

        // HotkeysPage UI
        private HotkeyControl mainControl = new();
        private HotkeyQuickControl quickControl = new();
        private Storyboard storyboard = new Storyboard();

        #region events
        public event ListeningEventHandler Listening;
        public delegate void ListeningEventHandler(Hotkey hotkey, ListenerType type);
        public event PinningEventHandler Pinning;
        public delegate void PinningEventHandler(Hotkey hotkey);
        public event SummonedEventHandler Summoned;
        public delegate void SummonedEventHandler(Hotkey hotkey);
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Hotkey hotkey);
        #endregion

        public Hotkey()
        {
            mainControl.HotkeyOutput.Click += (e, sender) => Listening?.Invoke(this, ListenerType.Output);
            mainControl.HotkeyPin.Click += (e, sender) => Pinning?.Invoke(this);
            quickControl.QuickButton.Click += (e, sender) => Summoned?.Invoke(this);

            // define animation
            DoubleAnimation opacityAnimation = new DoubleAnimation()
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1)
            };

            storyboard.Children.Add(opacityAnimation);

            Storyboard.SetTarget(opacityAnimation, mainControl.HotkeyInput);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
        }

        public Hotkey(ushort id) : this()
        {
            hotkeyId = id;
        }

        public void SetInputsHotkey(InputsHotkey inputsHotkey)
        {
            this.inputsHotkey = inputsHotkey;

            switch (inputsHotkey.hotkeyType)
            {
                case InputsHotkeyType.Embedded:
                    mainControl.HotkeyPanel.Visibility = Visibility.Collapsed;
                    mainControl.HotkeyPin.Visibility = Visibility.Collapsed;

                    mainControl.HotkeyButtons.Children.Remove(mainControl.HotkeyInput);
                    mainControl.EmbeddedGrid.Children.Add(mainControl.HotkeyInput);

                    mainControl.HotkeyGrid.Children.Remove(mainControl.HotkeyErase);
                    mainControl.EmbeddedGrid.Children.Add(mainControl.HotkeyErase);

                    mainControl.HotkeyInput.Click += (e, sender) => Listening?.Invoke(this, ListenerType.UI);
                    break;
                case InputsHotkeyType.Custom:
                    mainControl.HotkeyCustomName.Visibility = Visibility.Visible;
                    mainControl.HotkeyOutput.Visibility = Visibility.Visible;
                    mainControl.HotkeyName.Visibility = Visibility.Collapsed;
                    mainControl.HotkeyInput.Click += (e, sender) => Listening?.Invoke(this, ListenerType.Default);
                    break;
                default:
                    mainControl.HotkeyInput.Click += (e, sender) => Listening?.Invoke(this, ListenerType.Default);
                    break;
            }
        }

        public void Refresh()
        {
            // update name
            if (string.IsNullOrEmpty(Name) || inputsHotkey.hotkeyType != InputsHotkeyType.Custom)
                Name = inputsHotkey.GetName();

            mainControl.HotkeyCustomName.TextChanged += HotkeyCustomName_TextChanged;
            mainControl.HotkeyErase.Click += HotkeyErase_Click;

            // update buttons name and states
            DrawGlyph();
            DrawName();
            DrawInput();
            DrawOutput();
            DrawPin();
        }

        private void DrawGlyph()
        {
            // update glyphs
            mainControl.HotkeyIcon.FontFamily = quickControl.QuickIcon.FontFamily = inputsHotkey.fontFamily;
            mainControl.HotkeyIcon.FontSize = quickControl.QuickIcon.FontSize = inputsHotkey.fontSize;
            mainControl.HotkeyIcon.Glyph = quickControl.QuickIcon.Glyph = inputsHotkey.Glyph;
        }

        private void DrawName()
        {
            mainControl.HotkeyDesc.Text = inputsHotkey.GetDescription();
            mainControl.HotkeyName.Text = quickControl.QuickName.Text = mainControl.HotkeyCustomName.Text = this.Name;
        }

        private void HotkeyErase_Click(object sender, RoutedEventArgs e)
        {
            // restore default name
            Name = inputsHotkey.GetName();

            HotkeysManager.ClearHotkey(this);
            InputsManager.ClearListening(this);

            DrawName();
            DrawOutput();
        }

        private void HotkeyCustomName_TextChanged(object sender, TextChangedEventArgs e)
        {
            Name = mainControl.HotkeyName.Text = quickControl.QuickName.Text = mainControl.HotkeyCustomName.Text;

            Updated?.Invoke(this);
        }

        public void StartListening(ListenerType type)
        {
            // update button
            switch (type)
            {
                case ListenerType.Output:
                    mainControl.HotkeyOutput.Content = Properties.Resources.OverlayPage_Listening;
                    mainControl.HotkeyOutput.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    DrawOutput();
                    break;
                case ListenerType.UI:
                case ListenerType.Default:
                    mainControl.HotkeyInput.Content = Properties.Resources.OverlayPage_Listening;
                    mainControl.HotkeyInput.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
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
                    mainControl.HotkeyInput.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    DrawOutput();
                    break;
                case ListenerType.UI:
                case ListenerType.Default:
                    mainControl.HotkeyInput.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    DrawInput();
                    break;
            }
        }

        public HotkeyControl GetControl()
        {
            return mainControl;
        }

        public HotkeyQuickControl GetPin()
        {
            return quickControl;
        }

        private bool HasInput()
        {
            return inputsChord.State.Buttons.Any();
        }

        private bool HasOutput()
        {
            return inputsChord.OutputKeys.Any();
        }

        public void DrawInput()
        {
            // mainButton content
            SimpleStackPanel inputContent = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6
            };

            mainControl.MainGrid.IsEnabled = true;
            if (HasInput())
            {
                IController controller = ControllerManager.GetTargetController();
                IDevice device = MainWindow.CurrentDevice;

                foreach (ButtonFlags button in inputsChord.State.Buttons)
                {
                    if (controller is not null && controller.IsButtonSupported(button))
                    {
                        FontIcon fontIcon = controller.GetFontIcon(button);
                        if (fontIcon.Foreground is null)
                            fontIcon.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                        inputContent.Children.Add(fontIcon);
                    }
                    else if (device is not null)
                    {
                        Label buttonLabel = new Label() { Content = device.GetButtonName(button) };
                        inputContent.Children.Add(buttonLabel);
                    }
                }

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
            mainControl.HotkeyInput.Content = inputContent;

            DrawErase();
        }

        private void DrawOutput()
        {
            // update button content
            switch (HasOutput())
            {
                case true:
                    mainControl.HotkeyOutput.Content = string.Join(", ", inputsChord.OutputKeys.Where(key => key.IsKeyDown));
                    mainControl.HotkeyOutput.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
                    break;
                case false:
                    mainControl.HotkeyOutput.Content = Properties.Resources.ResourceManager.GetString("InputsHotkey_fallbackOutput");
                    mainControl.HotkeyOutput.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
                    break;
            }

            DrawErase();
        }

        private void DrawPin()
        {
            // update pin button
            switch (IsPinned)
            {
                case true:
                    mainControl.HotkeyPin.Content = new FontIcon() { Glyph = "\uE77A", FontSize = 14 };
                    mainControl.HotkeyPin.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    mainControl.HotkeyPin.Content = new FontIcon() { Glyph = "\uE840", FontSize = 14 };
                    mainControl.HotkeyPin.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }
        }

        private void DrawErase()
        {
            // update delete button status
            mainControl.HotkeyErase.IsEnabled = HasInput() || HasOutput();
        }

        public void Highlight()
        {
            storyboard.Begin(mainControl.HotkeyInput);
        }

        public void SetToggle(bool toggle)
        {
            switch (toggle)
            {
                case true:
                    quickControl.QuickButton.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    quickControl.QuickButton.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }
        }

        public void ControllerSelected(IController controller)
        {
            // (re)draw inputs based on IController type
            DrawInput();
        }
    }
}
