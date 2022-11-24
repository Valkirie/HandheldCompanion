using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for HotkeysPage.xaml
    /// </summary>
    public partial class HotkeysPage : Page
    {
        public HotkeysPage()
        {
            InitializeComponent();

            HotkeysManager.HotkeyCreated += HotkeysManager_HotkeyCreated;
            HotkeysManager.HotkeyTypeCreated += HotkeysManager_HotkeyTypeCreated;
        }

        public HotkeysPage(string Tag) : this()
        {
            this.Tag = Tag;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        private void HotkeysManager_HotkeyTypeCreated(InputsHotkey.InputsHotkeyType type)
        {
            // These are special shortcut keys with no related events
            switch (type)
            {
                case InputsHotkey.InputsHotkeyType.UI:
                    return;
            }

            this.Dispatcher.Invoke(() =>
            {
                SimpleStackPanel stackPanel = new()
                {
                    Tag = type,
                    Spacing = 6
                };
                string text = EnumUtils.GetDescriptionFromEnumValue(type);
                stackPanel.Children.Add(new TextBlock() { Text = text, FontWeight = FontWeights.SemiBold });

                HotkeysPanel.Children.Add(stackPanel);
            });
        }

        private void HotkeysManager_HotkeyCreated(Hotkey hotkey)
        {
            // These are special shortcut keys with no related events
            if (hotkey.inputsHotkey.hotkeyType == InputsHotkey.InputsHotkeyType.UI)
                return;

            this.Dispatcher.Invoke(() =>
            {
                Border hotkeyBorder = hotkey.GetHotkey();
                if (hotkeyBorder is null || hotkeyBorder.Parent != null)
                    return;

                ushort idx = (ushort)hotkey.inputsHotkey.hotkeyType;
                SimpleStackPanel stackPanel = (SimpleStackPanel)HotkeysPanel.Children[idx];

                stackPanel.Children.Add(hotkeyBorder);
            });
        }
    }
}
