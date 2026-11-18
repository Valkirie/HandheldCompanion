using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Helpers
{
    /// <summary>
    /// Gives controls a UI Automation name/help text when the XAML does not provide one, so screen readers
    /// (NVDA, Narrator, ...) can announce them, and builds spoken descriptions for <see cref="ScreenReader"/>.
    /// </summary>
    public static class AccessibilityHelper
    {
        private const int MaxTextLength = 300;
        private const int MaxDepth = 12;

        // marks names/help texts we generated ourselves, so they can be refreshed (recycled containers, language change)
        private static readonly DependencyProperty IsAutoNamedProperty =
            DependencyProperty.RegisterAttached("IsAutoNamed", typeof(bool), typeof(AccessibilityHelper), new PropertyMetadata(false));

        private static readonly DependencyProperty IsLabelledProperty =
            DependencyProperty.RegisterAttached("IsLabelled", typeof(bool), typeof(AccessibilityHelper), new PropertyMetadata(false));

        private static readonly DependencyProperty IsAutoHelpProperty =
            DependencyProperty.RegisterAttached("IsAutoHelp", typeof(bool), typeof(AccessibilityHelper), new PropertyMetadata(false));

        // Segoe Fluent/MDL2 glyphs commonly used on icon-only buttons
        private static readonly Dictionary<string, string> GlyphNames = new()
        {
            [""] = "Menu",
            [""] = "Brightness",
            [""] = "Down",
            [""] = "Up",
            [""] = "Edit",
            [""] = "Add",
            [""] = "Cancel",
            [""] = "More",
            [""] = "Settings",
            [""] = "Mail",
            [""] = "People",
            [""] = "Pin",
            [""] = "Link",
            [""] = "Filter",
            [""] = "Search",
            [""] = "Forward",
            [""] = "Back",
            [""] = "Refresh",
            [""] = "Share",
            [""] = "Lock",
            [""] = "Favorite",
            [""] = "Favorite",
            [""] = "Remove",
            [""] = "Done",
            [""] = "Full screen",
            [""] = "Delete",
            [""] = "Save",
            [""] = "Keyboard",
            [""] = "Volume",
            [""] = "Play",
            [""] = "Pause",
            [""] = "Previous",
            [""] = "Next",
            [""] = "Unpin",
            [""] = "Profile",
            [""] = "Redo",
            [""] = "Undo",
            [""] = "Power",
            [""] = "Home",
            [""] = "View",
            [""] = "Clear",
            [""] = "Sync",
            [""] = "Download",
            [""] = "Help",
            [""] = "Upload",
            [""] = "Import",
            [""] = "Close",
            [""] = "Copy",
            [""] = "Open file",
            [""] = "Information",
            [""] = "Like",
            [""] = "Like",
        };

        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
                return;
            initialized = true;

            // Loaded is only broadcast to subtrees that have Loaded handlers, so it can't be used here.
            // Instead: label on focus, and label whole trees when windows load, pages navigate or scroll content changes.
            EventManager.RegisterClassHandler(typeof(Control), UIElement.PreviewGotKeyboardFocusEvent, new System.Windows.Input.KeyboardFocusChangedEventHandler(Control_PreviewGotKeyboardFocus), true);
            EventManager.RegisterClassHandler(typeof(ScrollViewer), ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(ScrollViewer_ScrollChanged), true);
        }

        private static void Control_PreviewGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            // refresh generated labels before the screen reader asks: list containers get recycled with different data
            if (ReferenceEquals(sender, e.NewFocus) && sender is Control control)
                EnsureLabel(control);
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // new content was laid out (page navigation, expander opened, items added)
            if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0 || e.ViewportHeightChange != 0)
                LabelTreeDeferred((ScrollViewer)sender);
        }

        private static readonly HashSet<DependencyObject> pendingTrees = [];

        /// <summary>Labels every control under <paramref name="root"/> once layout has settled.</summary>
        public static void LabelTreeDeferred(DependencyObject? root)
        {
            if (root is null || !pendingTrees.Add(root))
                return;

            root.Dispatcher.BeginInvoke(() =>
            {
                pendingTrees.Remove(root);
                LabelTree(root);
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        public static void LabelTree(DependencyObject root)
        {
            // already labelled controls are refreshed on focus only, keeps repeated walks cheap
            if (root is Control control && control.ReadLocalValue(IsLabelledProperty) is not true)
            {
                EnsureLabel(control);
                control.SetValue(IsLabelledProperty, true);
            }

            int count = root is Visual ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (int i = 0; i < count; i++)
                LabelTree(VisualTreeHelper.GetChild(root, i));
        }

        #region labelling

        public static void EnsureLabel(Control control)
        {
            try
            {
                switch (control)
                {
                    case SettingsCard card:
                        {
                            // header-less cards inside a SettingsExpander belong to the expander's setting
                            (string cardHeader, string cardDescription) = (Text(card.Header), Text(card.Description));
                            if (string.IsNullOrEmpty(cardHeader))
                                (cardHeader, cardDescription) = GetCardContext(card);

                            SetName(control, cardHeader);
                            SetHelp(control, cardDescription);
                            return;
                        }

                    case SettingsExpander expander:
                        SetName(control, Text(expander.Header));
                        SetHelp(control, Text(expander.Description));
                        return;
                }

                if (!IsLabelCandidate(control))
                    return;

                // template parts of a settings row (SettingsExpander's expand toggle, ...) are the row itself
                if (GetTemplatedSettingsParent(control) is { } row)
                {
                    SetName(control, row.Header);
                    SetHelp(control, row.Description);
                    return;
                }

                (string header, string description) = GetCardContext(control);

                if (NeedsName(control))
                {
                    string name = FindName(control, header);
                    SetName(control, name);
                }

                // the card description is the best "what does this do" hint we have
                if (string.IsNullOrEmpty(description) && string.IsNullOrEmpty(header) && control is not ListBoxItem)
                    description = NearbyLabel(control).Help;

                // always called, so a refreshed (recycled) control with no help source drops its old generated help
                SetHelp(control, !string.IsNullOrEmpty(description) ? description : Text(control.ToolTip));
            }
            catch
            {
                // labelling must never break the UI
            }
        }

        private static bool IsLabelCandidate(Control control)
        {
            return control is ButtonBase or ToggleSwitch or RangeBase or ComboBox or TextBoxBase or PasswordBox
                or NumberBox or ListBoxItem or TreeViewItem or NavigationViewItem or Expander;
        }

        private static bool NeedsName(Control control)
        {
            // explicit names (and names we generated before) win, LabeledBy too
            if (control.ReadLocalValue(IsAutoNamedProperty) is true)
                return true;
            if (!string.IsNullOrEmpty(AutomationProperties.GetName(control)) || AutomationProperties.GetLabeledBy(control) is not null)
                return false;

            // data-bound list items whose type has no ToString() override are announced as "Namespace.Type"
            if (control is ListBoxItem { Content: { } item } && item is not string && item is not UIElement)
            {
                string? itemText = item.ToString();
                return string.IsNullOrEmpty(itemText) || itemText == item.GetType().FullName;
            }

            // icon-font glyphs used as text content ("") are read as garbage
            AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(control);
            return string.IsNullOrEmpty(Clean(peer?.GetName()));
        }

        private static string FindName(Control control, string cardHeader)
        {
            string name;

            // visible text inside buttons/items
            if (control is ContentControl contentControl)
            {
                name = contentControl.Content is UIElement or string
                    ? Text(contentControl.Content)
                    : VisualText(contentControl);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            // Header properties (ToggleSwitch, NumberBox, ComboBox templates, ...)
            PropertyInfo? headerProperty = control.GetType().GetProperty("Header", BindingFlags.Public | BindingFlags.Instance);
            if (headerProperty is not null && headerProperty.GetIndexParameters().Length == 0)
            {
                name = Text(headerProperty.GetValue(control));
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            if (control is AppBarButton appBarButton)
            {
                name = Text(appBarButton.Label);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            // value tooltips (sliders) are not a name
            name = Text(control.ToolTip);
            if (name.Any(char.IsLetter))
                return name;

            if (control is not ListBoxItem and not TreeViewItem)
            {
                if (!string.IsNullOrEmpty(cardHeader))
                    return cardHeader;

                name = NearbyLabel(control).Name;
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            return IconName(control);
        }

        private static void SetName(Control control, string name)
        {
            bool auto = control.ReadLocalValue(IsAutoNamedProperty) is true;
            if (!auto && (!string.IsNullOrEmpty(AutomationProperties.GetName(control)) || AutomationProperties.GetLabeledBy(control) is not null))
                return;

            if (string.IsNullOrEmpty(name))
            {
                if (auto)
                {
                    control.ClearValue(AutomationProperties.NameProperty);
                    control.ClearValue(IsAutoNamedProperty);
                }
                return;
            }

            if (AutomationProperties.GetName(control) != name)
                AutomationProperties.SetName(control, name);
            control.SetValue(IsAutoNamedProperty, true);
        }

        private static void SetHelp(Control control, string help)
        {
            // explicit help text wins
            bool auto = control.ReadLocalValue(IsAutoHelpProperty) is true;
            if (!auto && !string.IsNullOrEmpty(AutomationProperties.GetHelpText(control)))
                return;

            // value tooltips (sliders) are not a description, and don't repeat the name
            bool usable = !string.IsNullOrEmpty(help)
                && help.Any(char.IsLetter)
                && !string.Equals(help, AutomationProperties.GetName(control), StringComparison.CurrentCultureIgnoreCase);

            if (!usable)
            {
                // only clear what we generated ourselves
                if (auto)
                {
                    control.ClearValue(AutomationProperties.HelpTextProperty);
                    control.ClearValue(IsAutoHelpProperty);
                }
                return;
            }

            if (AutomationProperties.GetHelpText(control) != help)
                AutomationProperties.SetHelpText(control, help);
            control.SetValue(IsAutoHelpProperty, true);
        }

        /// <summary>Header/description of the nearest SettingsCard or SettingsExpander hosting the control.</summary>
        private static (string Header, string Description) GetCardContext(DependencyObject element)
        {
            DependencyObject? current = Parent(element);
            for (int i = 0; current is not null && i < MaxDepth * 2; i++, current = Parent(current))
            {
                switch (current)
                {
                    case SettingsCard card:
                        string header = Text(card.Header);
                        if (!string.IsNullOrEmpty(header))
                            return (header, Text(card.Description));
                        break;  // unnamed nested card, keep looking (e.g. SettingsExpander items)
                    case SettingsExpander expander:
                        return (Text(expander.Header), Text(expander.Description));
                    case Page or Window or ItemsPresenter:
                        return (string.Empty, string.Empty);
                }
            }

            return (string.Empty, string.Empty);
        }

        /// <summary>Header/description of the SettingsCard/SettingsExpander whose template contains the control.</summary>
        private static (string Header, string Description)? GetTemplatedSettingsParent(Control control)
        {
            DependencyObject? current = control.TemplatedParent;
            for (int i = 0; current is not null && i < 4; i++)
            {
                switch (current)
                {
                    case SettingsExpander expander:
                        return (Text(expander.Header), Text(expander.Description));
                    case SettingsCard card:
                        return (Text(card.Header), Text(card.Description));
                }

                current = (current as FrameworkElement)?.TemplatedParent;
            }

            return null;
        }

        /// <summary>
        /// Label laid out next to the control: a title (+ description) before it, a caption right below it,
        /// or an icon in front of it (icon + slider rows).
        /// </summary>
        private static (string Name, string Help) NearbyLabel(FrameworkElement element)
        {
            FrameworkElement current = element;
            for (int level = 0; level < 3; level++)
            {
                if (VisualTreeHelper.GetParent(current) is not Panel panel)
                    break;

                int index = panel.Children.IndexOf(current);
                for (int i = index - 1; i >= 0; i--)
                {
                    UIElement sibling = panel.Children[i];
                    if (sibling.Visibility != Visibility.Visible || ContainsFocusable(sibling))
                        continue;

                    List<string> parts = TextParts(sibling);
                    if (parts.Count > 0)
                        return (parts[0], string.Join(", ", parts.Skip(1)));
                }

                if (level == 0)
                {
                    // caption under the control (tiles)
                    if (index + 1 < panel.Children.Count && panel.Children[index + 1] is TextBlock { Visibility: Visibility.Visible } caption)
                    {
                        string text = Clean(caption.Text);
                        if (text.Any(char.IsLetter))
                            return (text, string.Empty);
                    }

                    // icon in front of the control
                    if (index > 0 && panel.Children[index - 1] is FontIcon { ActualGlyph: { } glyph } && GlyphNames.TryGetValue(glyph, out string? iconName))
                        return (iconName, string.Empty);
                }

                current = panel;
            }

            return (string.Empty, string.Empty);
        }

        /// <summary>Each visible text of a label block separately (title, description, ...), only those with letters.</summary>
        private static List<string> TextParts(DependencyObject element)
        {
            List<string> parts = [];
            CollectTextParts(element, parts, 0);
            return parts;
        }

        private static void CollectTextParts(DependencyObject element, List<string> parts, int depth)
        {
            if (depth > MaxDepth || element is IconElement || element is UIElement { Visibility: not Visibility.Visible })
                return;

            string? text = element switch
            {
                TextBlock textBlock => textBlock.Text,
                AccessText accessText => accessText.Text,
                Label { Content: string content } => content,
                _ => null,
            };

            if (text is not null)
            {
                text = Clean(text);
                if (text.Any(char.IsLetter))
                    parts.Add(text);
                return;
            }

            int count = element is Visual ? VisualTreeHelper.GetChildrenCount(element) : 0;
            for (int i = 0; i < count; i++)
                CollectTextParts(VisualTreeHelper.GetChild(element, i), parts, depth + 1);
        }

        private static bool ContainsFocusable(DependencyObject element)
        {
            if (element is Control { Focusable: true })
                return true;

            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
                if (ContainsFocusable(VisualTreeHelper.GetChild(element, i)))
                    return true;

            return false;
        }

        private static string IconName(Control control)
        {
            object? icon = control switch
            {
                AppBarButton appBarButton => appBarButton.Icon,
                ContentControl contentControl => contentControl.Content,
                _ => null,
            };

            FontIcon? fontIcon = icon as FontIcon ?? FindVisualChild<FontIcon>(icon as DependencyObject ?? control);
            string? glyph = fontIcon?.ActualGlyph;
            if (glyph is not null && GlyphNames.TryGetValue(glyph, out string? name))
                return name;

            return string.Empty;
        }

        /// <summary>Names a blank, color-coded choice button (position pickers), including whether it is the selected one.</summary>
        public static void SetChoiceName(Control control, string name, bool selected)
        {
            AutomationProperties.SetName(control, selected ? $"{name}, selected" : name);
        }

        #endregion

        #region descriptions

        /// <summary>Spoken description of a control: "name, type, state, unavailable. help".</summary>
        public static string Describe(Control control)
        {
            EnsureLabel(control);

            AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(control);

            string name = AutomationProperties.GetName(control);
            if (string.IsNullOrEmpty(name))
                name = Clean(peer?.GetName());
            if (string.IsNullOrEmpty(name))
                name = FindName(control, GetCardContext(control).Header);

            string type = ControlType(control, peer);
            string state = DescribeState(control);
            string help = AutomationProperties.GetHelpText(control);

            List<string> parts = [name, type, state];
            if (!control.IsEnabled)
                parts.Add("unavailable");

            string result = Join(", ", parts);
            if (!string.IsNullOrEmpty(help) && !result.Contains(help, StringComparison.CurrentCultureIgnoreCase))
                result = Join(". ", [result, help]);

            return result;
        }

        /// <summary>Current value/state only, used when the focused control changes value.</summary>
        public static string DescribeState(Control control)
        {
            switch (control)
            {
                case ToggleSwitch toggleSwitch:
                    return toggleSwitch.IsOn ? "on" : "off";

                case RadioButton radioButton:
                    return radioButton.IsChecked == true ? "selected" : "not selected";

                case CheckBox checkBox:
                    return checkBox.IsChecked switch { true => "checked", false => "not checked", _ => "partially checked" };

                case ToggleButton { TemplatedParent: Expander expanderToggle }:
                    return expanderToggle.IsExpanded ? "expanded" : "collapsed";

                case ToggleButton toggleButton:
                    return toggleButton.IsChecked == true ? "pressed" : "not pressed";

                case Slider slider:
                    return FormatNumber(slider.Value, slider.AutoToolTipPrecision);

                case RangeBase rangeBase:
                    return FormatNumber(rangeBase.Value, 2);

                case NumberBox numberBox:
                    return double.IsNaN(numberBox.Value) ? numberBox.Text ?? string.Empty : FormatNumber(numberBox.Value, 2);

                case ComboBox comboBox:
                    {
                        string text = comboBox.IsEditable ? comboBox.Text : Text(comboBox.SelectionBoxItem);
                        if (string.IsNullOrEmpty(text) && comboBox.SelectedItem is not null)
                            text = comboBox.ItemContainerGenerator.ContainerFromItem(comboBox.SelectedItem) is Control container
                                ? VisualText(container)
                                : comboBox.SelectedItem.ToString() ?? string.Empty;
                        return string.IsNullOrEmpty(text) ? "no selection" : text;
                    }

                case PasswordBox:
                    return string.Empty;

                case TextBox textBox:
                    return string.IsNullOrEmpty(textBox.Text) ? "blank" : textBox.Text;

                case SettingsExpander settingsExpander:
                    return settingsExpander.IsExpanded ? "expanded" : "collapsed";

                case Expander expander:
                    return expander.IsExpanded ? "expanded" : "collapsed";

                case NavigationViewItem navigationViewItem:
                    return navigationViewItem.IsSelected ? "selected" : string.Empty;

                case MenuItem menuItem:
                    if (menuItem.IsCheckable)
                        return menuItem.IsChecked ? "checked" : "not checked";
                    return menuItem.HasItems ? "submenu" : string.Empty;

                case ListBoxItem listBoxItem:
                    return listBoxItem.IsSelected ? "selected" : string.Empty;
            }

            return string.Empty;
        }

        private static string ControlType(Control control, AutomationPeer? peer)
        {
            switch (control)
            {
                case ToggleSwitch:
                    return "switch";
                case SettingsCard:
                    return "button";
                case NavigationViewItem:
                    return "tab";
                case ComboBoxItem:
                case ListBoxItem:
                    return "item";
            }

            try
            {
                return peer?.GetLocalizedControlType() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatNumber(double value, int precision)
        {
            return Math.Round(value, Math.Clamp(precision, 0, 4)).ToString(CultureInfo.CurrentCulture);
        }

        #endregion

        #region text extraction

        /// <summary>Readable text of a header/content/tooltip object, ignoring icon glyphs.</summary>
        public static string Text(object? value)
        {
            StringBuilder builder = new();
            AppendText(value, builder, 0, false);
            return Truncate(builder.ToString());
        }

        /// <summary>Readable text of a whole subtree, without the text of nested buttons/inputs (dialog bodies).</summary>
        public static string StaticText(object? value)
        {
            StringBuilder builder = new();
            AppendText(value, builder, 0, true);
            return Truncate(builder.ToString());
        }

        private static string VisualText(DependencyObject element)
        {
            StringBuilder builder = new();
            AppendChildren(element, builder, 1, false);
            return Truncate(builder.ToString());
        }

        private static void AppendText(object? value, StringBuilder builder, int depth, bool skipControls)
        {
            if (value is null || depth > MaxDepth || builder.Length > MaxTextLength)
                return;

            switch (value)
            {
                case string text:
                    Append(builder, text);
                    return;

                case IconElement or Image:
                    return;

                case UIElement { Visibility: not Visibility.Visible }:
                    return;

                case Control control when skipControls && depth > 0 && control.Focusable:
                    return;

                case TextBlock textBlock:
                    Append(builder, textBlock.Text);
                    return;

                case AccessText accessText:
                    Append(builder, accessText.Text);
                    return;

                case TextBox textBox:
                    Append(builder, textBox.Text);
                    return;

                case FrameworkElement element when !string.IsNullOrEmpty(AutomationProperties.GetName(element)):
                    Append(builder, AutomationProperties.GetName(element));
                    return;

                case ContentControl contentControl when contentControl.Content is string or UIElement:
                    AppendText(contentControl.Content, builder, depth + 1, skipControls);
                    return;

                case DependencyObject dependencyObject:
                    AppendChildren(dependencyObject, builder, depth, skipControls);
                    return;

                default:
                    Append(builder, value.ToString());
                    return;
            }
        }

        private static void AppendChildren(DependencyObject element, StringBuilder builder, int depth, bool skipControls)
        {
            int count = element is Visual ? VisualTreeHelper.GetChildrenCount(element) : 0;
            if (count == 0)
            {
                // not rendered yet: fall back to the logical tree
                foreach (object child in LogicalTreeHelper.GetChildren(element))
                    AppendText(child, builder, depth + 1, skipControls);
                return;
            }

            for (int i = 0; i < count; i++)
                AppendText(VisualTreeHelper.GetChild(element, i), builder, depth + 1, skipControls);
        }

        private static void Append(StringBuilder builder, string? text)
        {
            text = Clean(text);
            if (string.IsNullOrEmpty(text))
                return;

            if (builder.Length > 0)
                builder.Append(", ");
            builder.Append(text);
        }

        /// <summary>Removes icon-font glyphs (private use area, PromptFont arrows/symbols) and collapses whitespace.</summary>
        private static string Clean(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            StringBuilder builder = new(text.Length);
            foreach (char c in text)
            {
                if (char.GetUnicodeCategory(c) == UnicodeCategory.PrivateUse)
                    continue;
                if (c is >= '←' and <= '⏿' or >= '①' and <= '➿')
                    continue;

                builder.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }

            string cleaned = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return cleaned.Any(char.IsLetterOrDigit) ? cleaned : string.Empty;
        }

        private static string Truncate(string text)
        {
            return text.Length <= MaxTextLength ? text : text[..MaxTextLength];
        }

        private static string Join(string separator, IEnumerable<string> parts)
        {
            return string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        #endregion

        #region tree helpers

        private static DependencyObject? Parent(DependencyObject element)
        {
            DependencyObject? parent = element is Visual ? VisualTreeHelper.GetParent(element) : null;
            return parent ?? LogicalTreeHelper.GetParent(element);
        }

        private static T? FindVisualChild<T>(DependencyObject? element) where T : DependencyObject
        {
            if (element is null)
                return null;

            int count = element is Visual ? VisualTreeHelper.GetChildrenCount(element) : 0;
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                if (child is T match)
                    return match;
                if (FindVisualChild<T>(child) is T nested)
                    return nested;
            }

            return null;
        }

        #endregion
    }
}
