using HandheldCompanion.Managers;
using iNKORE.UI.WPF.Modern.Controls;
using NAudio.Vorbis;
using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HandheldCompanion.UI
{
    public class UISounds
    {
        private static string appFolder = string.Empty;
        private static Timer soundTimer;
        private static string audioFilePath;

        public const string Expanded = "drop_001";
        public const string Collapse = "drop_002";
        public const string Focus = "bong_001";
        public const string Click = "bong_001";
        public const string ToggleOn = "switch_004";
        public const string ToggleOff = "switch_005";
        public const string Select = "switch_007";
        public const string Slide = "glitch_004";

        static UISounds()
        {
            // Get the current application folder
            appFolder = AppDomain.CurrentDomain.BaseDirectory;

            soundTimer = new(100) { AutoReset = false };
            soundTimer.Elapsed += SoundTimer_Elapsed;

            // Register the class handler for the Click event
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnClick));
            EventManager.RegisterClassHandler(typeof(RepeatButton), RepeatButton.ClickEvent, new RoutedEventHandler(OnClick));
            EventManager.RegisterClassHandler(typeof(UIElement), UIElement.GotFocusEvent, new RoutedEventHandler(OnFocus));
            EventManager.RegisterClassHandler(typeof(ToggleSwitch), ToggleSwitch.ToggledEvent, new RoutedEventHandler(OnToggle));
            EventManager.RegisterClassHandler(typeof(CheckBox), CheckBox.CheckedEvent, new RoutedEventHandler(OnCheck));
            EventManager.RegisterClassHandler(typeof(CheckBox), CheckBox.UncheckedEvent, new RoutedEventHandler(OnCheck));
            EventManager.RegisterClassHandler(typeof(Slider), Slider.ValueChangedEvent, new RoutedEventHandler(OnSlide));
            EventManager.RegisterClassHandler(typeof(RadioButtons), RadioButtons.SelectionChangedEvent, new RoutedEventHandler(OnSelect));
            EventManager.RegisterClassHandler(typeof(Expander), Expander.ExpandedEvent, new RoutedEventHandler(OnExpand));
            EventManager.RegisterClassHandler(typeof(Expander), Expander.CollapsedEvent, new RoutedEventHandler(OnExpand));
        }

        private static async void SoundTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            using (VorbisWaveReader waveReader = new VorbisWaveReader(audioFilePath))
            {
                using (WaveOutEvent waveOut = new WaveOutEvent())
                {
                    if (waveOut.DeviceNumber == -1)
                        return;

                    waveOut.Init(waveReader);
                    waveOut.Play();

                    // wait here until playback stops or should stop
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                        await Task.Delay(1);
                }
            }
        }

        private static void OnExpand(object sender, RoutedEventArgs e)
        {
            Expander uIElement = (Expander)sender;
            if (!uIElement.IsVisible)
                return;

            switch (uIElement.IsExpanded)
            {
                case true:
                    PlayOggFile("drop_001");
                    break;
                case false:
                    PlayOggFile("drop_002");
                    break;
            }
        }

        private static UIElement prevElement;
        private static void OnFocus(object sender, RoutedEventArgs e)
        {
            UIElement uIElement = (UIElement)sender;
            if (!uIElement.IsFocused || !uIElement.Focusable || !uIElement.IsVisible)
                return;

            // set default sound
            string sound = UISounds.Focus;

            switch (uIElement.GetType().Name)
            {
                case "TouchScrollViewer":
                    return;
                case "ComboBoxItem":
                    if (prevElement != null && prevElement is ComboBox)
                    {
                        // ComboBox was opened
                        sound = UISounds.Expanded;
                    }
                    break;
                case "ComboBox":
                    if (prevElement != null && prevElement is ComboBoxItem)
                    {
                        // ComboBox was closed
                        sound = UISounds.Collapse;
                    }
                    break;
            }

            prevElement = uIElement;

            PlayOggFile(sound);
        }

        private static void OnSelect(object sender, RoutedEventArgs e)
        {
            UIElement uIElement = (UIElement)sender;
            if (!uIElement.IsVisible)
                return;

            PlayOggFile(UISounds.Select);
        }

        private static void OnClick(object sender, RoutedEventArgs e)
        {
            UIElement uIElement = (UIElement)sender;
            if (!uIElement.IsVisible)
                return;

            PlayOggFile(UISounds.Focus);
        }

        private static void OnCheck(object sender, RoutedEventArgs e)
        {
            CheckBox uIElement = (CheckBox)sender;
            if (!uIElement.IsLoaded || !uIElement.IsVisible)
                return;

            switch (uIElement.IsChecked)
            {
                case true:
                    PlayOggFile(UISounds.ToggleOn);
                    break;
                case false:
                    PlayOggFile(UISounds.ToggleOff);
                    break;
            }
        }

        private static void OnToggle(object sender, RoutedEventArgs e)
        {
            ToggleSwitch uIElement = (ToggleSwitch)sender;
            if (!uIElement.IsLoaded || !uIElement.IsVisible)
                return;

            switch (uIElement.IsOn)
            {
                case true:
                    PlayOggFile(UISounds.ToggleOn);
                    break;
                case false:
                    PlayOggFile(UISounds.ToggleOff);
                    break;
            }
        }

        private static void OnSlide(object sender, RoutedEventArgs e)
        {
            Control uIElement = (Control)sender;
            if (!uIElement.IsLoaded || !uIElement.IsVisible)
                return;

            PlayOggFile(UISounds.Slide);
        }

        public static void PlayOggFile(string fileName)
        {
            bool Enabled = SettingsManager.GetBoolean("UISounds");
            if (!Enabled)
                return;

            // Concatenate /UI/Audio/{fileName}.ogg
            string audioFilePath = Path.Combine(appFolder, "UI", "Audio", fileName + ".ogg");
            if (!File.Exists(audioFilePath))
                return;

            // update file path
            UISounds.audioFilePath = audioFilePath;

            // reset timer
            soundTimer.Stop();
            soundTimer.Start();
        }
    }
}
