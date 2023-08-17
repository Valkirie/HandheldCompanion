using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Properties;
using HandheldCompanion.Views;
using Inkore.UI.WPF.Modern.Controls;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using static HandheldCompanion.Managers.InputsHotkey;
using static HandheldCompanion.Managers.InputsManager;

namespace HandheldCompanion.Managers;

public class Hotkey
{
    private bool _isEnabled = true;

    private bool _isPinned;
    public ushort hotkeyId;
    public InputsChord inputsChord = new();

    [JsonIgnore] public InputsHotkey inputsHotkey = new();

    // HotkeysPage UI
    private readonly HotkeyControl mainControl = new();

    public string Name;
    private readonly HotkeyQuickControl quickControl = new();
    private readonly Storyboard storyboard = new();

    public Hotkey()
    {
        mainControl.HotkeyOutput.Click += async (e, sender) =>
        {
            // workaround for gamepad navigation
            await Task.Delay(100);
            Listening?.Invoke(this, ListenerType.Output);
        };

        mainControl.HotkeyPin.Click += async (e, sender) =>
        {
            Pinning?.Invoke(this);
        };

        quickControl.QuickButton.Click += async (e, sender) =>
        {
            // workaround for gamepad navigation
            await Task.Delay(100);
            Summoned?.Invoke(this);
        };

        // define animation
        var opacityAnimation = new DoubleAnimation
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

    public bool IsPinned
    {
        get => _isPinned;

        set
        {
            // update pin button
            switch (value)
            {
                case true:
                    mainControl.HotkeyPin.Content = new FontIcon { Glyph = "\uE77A", FontSize = 14 };
                    mainControl.HotkeyPin.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    mainControl.HotkeyPin.Content = new FontIcon { Glyph = "\uE840", FontSize = 14 };
                    mainControl.HotkeyPin.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }

            _isPinned = value;
        }
    }

    [JsonIgnore]
    public bool IsEnabled
    {
        get => _isEnabled;

        set
        {
            mainControl.IsEnabled = value;
            quickControl.IsEnabled = value;

            _isEnabled = value;
        }
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

                mainControl.HotkeyInput.Click += async (e, sender) =>
                {
                    // workaround for gamepad navigation
                    await Task.Delay(100);
                    Listening?.Invoke(this, ListenerType.UI);
                };
                break;
            case InputsHotkeyType.Custom:
                mainControl.HotkeyCustomName.Visibility = Visibility.Visible;
                mainControl.HotkeyOutput.Visibility = Visibility.Visible;
                mainControl.HotkeyName.Visibility = Visibility.Collapsed;
                mainControl.HotkeyInput.Click += async (e, sender) =>
                {
                    // workaround for gamepad navigation
                    await Task.Delay(100);
                    Listening?.Invoke(this, ListenerType.Default);
                };
                break;
            default:
                mainControl.HotkeyInput.Click += async (e, sender) =>
                {
                    // workaround for gamepad navigation
                    await Task.Delay(100);
                    Listening?.Invoke(this, ListenerType.Default);
                };
                break;
        }
    }

    public void Draw()
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
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // update glyphs
            mainControl.HotkeyIcon.FontFamily = quickControl.QuickIcon.FontFamily = inputsHotkey.fontFamily;
            mainControl.HotkeyIcon.FontSize = quickControl.QuickIcon.FontSize = inputsHotkey.fontSize;
            mainControl.HotkeyIcon.Glyph = quickControl.QuickIcon.Glyph = inputsHotkey.Glyph;
        });
    }

    private void DrawName()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            mainControl.HotkeyDesc.Text = inputsHotkey.GetDescription();
            mainControl.HotkeyName.Text = quickControl.QuickName.Text = mainControl.HotkeyCustomName.Text = Name;
        });
    }

    private void HotkeyErase_Click(object sender, RoutedEventArgs e)
    {
        // restore default name
        Name = inputsHotkey.GetName();

        HotkeysManager.ClearHotkey(this);
        ClearListening(this);

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
                mainControl.HotkeyOutput.Content = Resources.OverlayPage_Listening;
                mainControl.HotkeyOutput.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                DrawOutput();
                break;
            case ListenerType.UI:
            case ListenerType.Default:
                mainControl.HotkeyInput.Content = Resources.OverlayPage_Listening;
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
                mainControl.HotkeyOutput.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
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
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
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
                var controller = ControllerManager.GetTargetController();
                if (controller is null)
                    controller = ControllerManager.GetEmulatedController();
                var device = MainWindow.CurrentDevice;

                foreach (var button in inputsChord.State.Buttons)
                {
                    UIElement? label = null;

                    var fontIcon = controller.GetFontIcon(button);
                    // we display only one label, default one is not enough
                    if (fontIcon.Glyph != IController.defaultGlyph)
                    {
                        if (fontIcon.Foreground is null)
                            fontIcon.SetResourceReference(Control.ForegroundProperty,
                                "SystemControlForegroundBaseMediumBrush");

                        label = fontIcon;
                    }

                    if (label is null && device is not null)
                    {
                        Label buttonLabel = new() { Content = device.GetButtonName(button) };
                        label = buttonLabel;
                    }

                    if (label is not null)
                        inputContent.Children.Add(label);
                }

                TextBlock type = new()
                {
                    Text = inputsChord.InputsType.ToString(),
                    VerticalAlignment = VerticalAlignment.Center
                };
                type.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");

                inputContent.Children.Add(type);
            }
            else
            {
                TextBlock fallback = new()
                {
                    Text = Resources.ResourceManager.GetString("InputsHotkey_fallbackInput")
                };
                fallback.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                // set fallback content
                inputContent.Children.Add(fallback);
            }

            // update main button content
            mainControl.HotkeyInput.Content = inputContent;
        });

        DrawErase();
    }

    private void DrawOutput()
    {
        // update button content
        switch (HasOutput())
        {
            case true:
                mainControl.HotkeyOutput.Content =
                    string.Join(", ", inputsChord.OutputKeys.Where(key => key.IsKeyDown));
                mainControl.HotkeyOutput.SetResourceReference(Control.ForegroundProperty,
                    "SystemControlForegroundBaseHighBrush");
                break;
            case false:
                mainControl.HotkeyOutput.Content = Resources.ResourceManager.GetString("InputsHotkey_fallbackOutput");
                mainControl.HotkeyOutput.SetResourceReference(Control.ForegroundProperty,
                    "SystemControlForegroundBaseMediumBrush");
                break;
        }

        DrawErase();
    }

    private void DrawPin()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // update pin button
            switch (IsPinned)
            {
                case true:
                    mainControl.HotkeyPin.Content = new FontIcon { Glyph = "\uE77A", FontSize = 14 };
                    mainControl.HotkeyPin.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    break;
                case false:
                    mainControl.HotkeyPin.Content = new FontIcon { Glyph = "\uE840", FontSize = 14 };
                    mainControl.HotkeyPin.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                    break;
            }
        });
    }

    private void DrawErase()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // update delete button status
            mainControl.HotkeyErase.IsEnabled = HasInput() || HasOutput();
        });
    }

    public void Highlight()
    {
        storyboard.Begin(mainControl.HotkeyInput);
    }

    public void SetToggle(bool toggle)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
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
        });
    }

    public void ControllerSelected(IController controller)
    {
        // (re)draw inputs based on IController type
        // todo: make this persistent/based on the parameter, just like LayoutPage
        // currently DrawInput() checks for the controller by itself
        DrawInput();
    }

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
}