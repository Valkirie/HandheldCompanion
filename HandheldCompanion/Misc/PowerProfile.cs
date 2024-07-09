using HandheldCompanion.Managers;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Misc
{
    [Serializable]
    public class PowerProfile
    {
        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;

                // UI thread (async)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (UIElement uIElement in uIElements.Values)
                        uIElement.textBlock1.Text = value;
                });
            }
        }

        private string _Description;
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value;

                // UI thread (async)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (UIElement uIElement in uIElements.Values)
                        uIElement.textBlock2.Text = value;
                });
            }
        }

        public string FileName { get; set; }
        public bool Default { get; set; }
        public bool DeviceDefault { get; set; }

        public Version Version { get; set; } = new();
        public Guid Guid { get; set; } = Guid.NewGuid();

        public bool TDPOverrideEnabled { get; set; }
        public double[] TDPOverrideValues { get; set; }

        public bool CPUOverrideEnabled { get; set; }
        public double CPUOverrideValue { get; set; }

        public bool GPUOverrideEnabled { get; set; }
        public double GPUOverrideValue { get; set; }

        public bool AutoTDPEnabled { get; set; }
        public float AutoTDPRequestedFPS { get; set; } = 30.0f;

        public bool EPPOverrideEnabled { get; set; }
        public uint EPPOverrideValue { get; set; } = 50;

        public bool CPUCoreEnabled { get; set; }
        public int CPUCoreCount { get; set; } = MotherboardInfo.NumberOfCores;

        public CPUBoostLevel CPUBoostLevel { get; set; } = CPUBoostLevel.Enabled;

        public FanProfile FanProfile { get; set; } = new();

        public int OEMPowerMode { get; set; } = 0xFF;
        public Guid OSPowerMode { get; set; } = Managers.OSPowerMode.BetterPerformance;

        private Dictionary<Page, UIElement> uIElements = [];

        public PowerProfile()
        { }

        public PowerProfile(string name, string description)
        {
            Name = name;
            Description = description;

            // Remove any invalid characters from the input
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string output = Regex.Replace(name, "[" + invalidChars + "]", string.Empty);
            output = output.Trim();

            FileName = output;
        }

        public string GetFileName()
        {
            return $"{FileName}.json";
        }

        public bool IsDefault()
        {
            return Default;
        }

        public void DrawUI(Page page)
        {
            if (uIElements.ContainsKey(page))
                return;

            // Add to dictionnary
            UIElement uIElement = new(this, page);
            uIElement.textBlock1.Text = Name;
            uIElement.textBlock2.Text = Description;

            uIElements[page] = uIElement;
        }

        private struct UIElement
        {
            // UI Elements, move me!
            public Button button;
            public Grid grid;
            public DockPanel dockPanel;
            public RadioButtons radioButtons;
            public RadioButton radioButton;
            public SimpleStackPanel simpleStackPanel;
            public TextBlock textBlock1 = new();
            public TextBlock textBlock2 = new();

            public UIElement(PowerProfile powerProfile, Page page)
            {
                // Create a button
                button = new Button
                {
                    Height = 66,
                    Margin = new Thickness(-16),
                    Padding = new Thickness(50, 12, 12, 12),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,

                    // Stored current profile, might be useful
                    Tag = powerProfile
                };

                // Create a grid
                grid = new Grid();

                // Define the Columns
                var colDef1 = new ColumnDefinition
                {
                    Width = new GridLength(10, GridUnitType.Star),
                };
                grid.ColumnDefinitions.Add(colDef1);

                var colDef2 = new ColumnDefinition
                {
                    MinWidth = 40
                };
                grid.ColumnDefinitions.Add(colDef2);

                // Create a dock panel
                dockPanel = new DockPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Create a radio buttons
                radioButtons = new RadioButtons
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Create a radio button
                radioButton = new RadioButton
                {
                    GroupName = $"PowerProfile{page.Name}"
                };

                // Create a simple stack panel
                simpleStackPanel = new()
                {
                    Margin = new Thickness(0, -10, 0, 0)
                };

                // Create a text block for the controller layout
                textBlock1.Style = (Style)Application.Current.Resources["BodyTextBlockStyle"];

                // Create a text block for the controller layout description
                textBlock2.TextWrapping = TextWrapping.NoWrap;
                textBlock2.Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"];
                textBlock2.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                // Add the text blocks to the simple stack panel
                simpleStackPanel.Children.Add(textBlock1);
                simpleStackPanel.Children.Add(textBlock2);

                // Add the simple stack panel to the radio button control
                radioButton.Content = simpleStackPanel;
                radioButton.Checked += RadioButton_Checked;
                radioButton.Unchecked += RadioButton_Unchecked;

                // Add the radio button to the radio buttons control
                radioButtons.Items.Add(radioButton);

                // Add the radio buttons control to the dock panel
                dockPanel.Children.Add(radioButtons);

                // Create a font icon
                FontIcon fontIcon = new FontIcon
                {
                    Margin = new Thickness(0, 0, 7, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    FontSize = 12,
                    Glyph = "\uE974",
                    FontFamily = new("Segoe Fluent Icons")
                };
                Grid.SetColumn(fontIcon, 1);

                // Add the dock panel and the font icon to the grid
                grid.Children.Add(dockPanel);
                grid.Children.Add(fontIcon);

                // Add the grid to the button
                button.Content = grid;
            }

            private void RadioButton_Checked(object sender, RoutedEventArgs e)
            {
                // do something
            }

            private void RadioButton_Unchecked(object sender, RoutedEventArgs e)
            {
                // do something
            }
        }

        public Button GetButton(Page page)
        {
            if (uIElements.TryGetValue(page, out UIElement UI))
                return UI.button;

            return null;
        }

        public RadioButton GetRadioButton(Page page)
        {
            if (uIElements.TryGetValue(page, out UIElement UI))
                return UI.radioButton;

            return null;
        }

        public void Check(Page page)
        {
            if (uIElements.TryGetValue(page, out UIElement UI))
                UI.radioButton.IsChecked = true;
        }

        public void Uncheck(Page page)
        {
            if (uIElements.TryGetValue(page, out UIElement UI))
                UI.radioButton.IsChecked = false;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
