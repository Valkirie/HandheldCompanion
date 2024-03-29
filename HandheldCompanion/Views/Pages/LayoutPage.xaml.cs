using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;


public partial class LayoutPage : Page
{
    // Event to update ViewModel
    public event UpdatedLayoutHandler LayoutUpdated;
    public delegate void UpdatedLayoutHandler(Layout layout);

    // Getter to update layout in ViewModels
    public Layout CurrentLayout => currentTemplate.Layout;
    public LayoutTemplate currentTemplate = new();
    protected LockObject updateLock = new();

    // page vars
    private Dictionary<string, (ILayoutPage, NavigationViewItem)> pages;
    private ButtonsPage buttonsPage;
    private DpadPage dpadPage;
    private GyroPage gyroPage;
    private JoysticksPage joysticksPage;
    private TrackpadsPage trackpadsPage;
    private TriggersPage triggersPage;

    private NavigationView parentNavView;
    private string preNavItemTag;

    public LayoutPage()
    {
        InitializeComponent();
    }

    public LayoutPage(string Tag, NavigationView parent) : this()
    {
        this.Tag = Tag;
        this.parentNavView = parent;
    }

    // Initialize pages later so the reference can be made to layoutPage from MainWindow
    public void Initialize()
    {
        buttonsPage = new ButtonsPage();
        dpadPage = new DpadPage();
        gyroPage = new GyroPage();
        joysticksPage = new JoysticksPage();
        trackpadsPage = new TrackpadsPage();
        triggersPage = new TriggersPage();

        // create controller related pages
        this.pages = new()
        {
            // buttons
            { "ButtonsPage", ( buttonsPage, navButtons ) },
            { "DpadPage", ( dpadPage, navDpad ) },

            // triger
            { "TriggersPage", ( triggersPage, navTriggers ) },

            // axis
            { "JoysticksPage", ( joysticksPage, navJoysticks ) },
            { "TrackpadsPage", ( trackpadsPage, navTrackpads ) },

            // gyro
            { "GyroPage", ( gyroPage, navGyro ) },
        };

        // TODO: Temporary until conversion to MVVM
        foreach(var template in LayoutManager.Templates)
        {
            LayoutManager_Updated(template);
        }

        LayoutManager.Updated += LayoutManager_Updated;
        LayoutManager.Initialized += LayoutManager_Initialized;
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        DeviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceUpdated;
        DeviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceUpdated;
        ProfileManager.Updated += ProfileManager_Updated;
    }

    private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
    {
        if (!MainWindow.CurrentPageName.Equals("LayoutPage"))
            return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (source)
            {
                case UpdateSource.QuickProfilesPage:
                    {
                        if (currentTemplate.Name.Equals(profile.LayoutTitle))
                            UpdateLayout(profile.Layout);
                    }
                    break;
            }
        });
    }

    private void ControllerManager_ControllerSelected(IController controller)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            RefreshLayoutList();

            // cascade update to (sub)pages
            foreach (var page in pages.Values)
            {
                page.Item2.IsEnabled = page.Item1.IsEnabled();
            }
        });
    }

    private void DeviceManager_UsbDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
    {
        IController controller = ControllerManager.GetTargetController();

        // lazy
        if (controller is not null)
            ControllerManager_ControllerSelected(controller);
    }

    private void LayoutManager_Initialized()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            RefreshLayoutList();
        });
    }

    private void LayoutManager_Updated(LayoutTemplate layoutTemplate)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // Get template separator index
            var idx = -1;

            // search if we already have this template listed
            foreach (var item in cB_Layouts.Items)
            {
                if (item is not ComboBoxItem)
                    continue;

                // get template
                var parent = (ComboBoxItem)item;
                if (parent.Content is not LayoutTemplate)
                    continue;

                var template = (LayoutTemplate)parent.Content;
                if (template.Guid.Equals(layoutTemplate.Guid))
                {
                    idx = cB_Layouts.Items.IndexOf(parent);
                    break;
                }
            }

            if (idx != -1)
            {
                // found it
                cB_Layouts.Items[idx] = new ComboBoxItem { Content = layoutTemplate };
            }
            else
            {
                // new entry
                if (layoutTemplate.IsInternal)
                    idx = cB_Layouts.Items.IndexOf(cB_LayoutsSplitterTemplates) + 1;
                else
                    idx = cB_Layouts.Items.IndexOf(cB_LayoutsSplitterCommunity) + 1;

                cB_Layouts.Items.Insert(idx, new ComboBoxItem { Content = layoutTemplate });
            }

            cB_Layouts.Items.Refresh();
            cB_Layouts.InvalidateVisual();
        });
    }

    private void SettingsManager_SettingValueChanged(string? name, object value)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "LayoutFilterOnDevice":
                    CheckBoxDeviceLayouts.IsChecked = Convert.ToBoolean(value);
                    RefreshLayoutList();
                    break;
            }
        });
    }

    private void RefreshLayoutList()
    {
        // Get filter settings
        var FilterOnDevice = SettingsManager.GetBoolean("LayoutFilterOnDevice");

        // Get current controller
        var controller = ControllerManager.GetTargetController();

        foreach (var layoutTemplate in LayoutManager.Templates)
        {
            // get parent
            if (layoutTemplate.Parent is not ComboBoxItem parent)
                continue;

            if (layoutTemplate.ControllerType is not null && FilterOnDevice)
                if (layoutTemplate.ControllerType != controller?.GetType())
                {
                    parent.Visibility = Visibility.Collapsed;
                    continue;
                }

            parent.Visibility = Visibility.Visible;
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    public void UpdateLayout(Layout layout)
    {
        currentTemplate.Layout = layout;

        UpdatePages();
    }

    public void UpdateLayoutTemplate(LayoutTemplate layoutTemplate)
    {
        // TODO: Not entirely sure what is going on here, but the old templates were still sending
        // events. Shouldn't they be destroyed? Either there is a bug or I don't understand something
        // in C# (probably the latter). Either way this handles/fixes/workarounds the issue.
        if (layoutTemplate.Layout != currentTemplate.Layout)
            currentTemplate = layoutTemplate;

        UpdatePages();
    }

    private void UpdatePages()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // This is a very important lock, it blocks backward events to the layout when
            // this is actually the backend that triggered the update. Notifications on higher
            // levels (pages and mappings) could potentially be blocked for optimization.
            using (new ScopedLock(updateLock))
            {
                // Invoke Layout Updated to trigger ViewModel updates
                LayoutUpdated?.Invoke(currentTemplate.Layout);

                // clear layout selection
                cB_Layouts.SelectedValue = null;

                CheckBoxDefaultLayout.IsChecked = currentTemplate.Layout.IsDefaultLayout;
                CheckBoxDefaultLayout.IsEnabled = currentTemplate.Layout != LayoutManager.GetDesktop();
            }
        });
    }

    private void cB_Layouts_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var comboBox = (ComboBox)sender;
        foreach (var item in comboBox.Items)
        {
            if (item is not ComboBoxItem)
                continue;

            var layoutTemplate = (ComboBoxItem)item;
            layoutTemplate.Width = comboBox.ActualWidth;
            layoutTemplate.InvalidateVisual();
        }
    }

    private async void ButtonApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if (cB_Layouts.SelectedItem is null)
            return;

        if (cB_Layouts.SelectedItem is not ComboBoxItem)
            return;

        // get parent
        var parent = cB_Layouts.SelectedItem as ComboBoxItem;

        if (parent.Content is not LayoutTemplate)
            return;

        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureApplyTemplate1, currentTemplate.Name),
            Content = string.Format(Properties.Resources.ProfilesPage_AreYouSureApplyTemplate2, currentTemplate.Name),
            DefaultButton = ContentDialogButton.Close,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                {
                    // do not overwrite currentTemplate and currentTemplate.Layout as a whole
                    // because they both have important Update notifitications set

                    // get template
                    LayoutTemplate layoutTemplate = (LayoutTemplate)parent.Content;
                    var newLayout = layoutTemplate.Layout.Clone() as Layout;
                    currentTemplate.Layout.AxisLayout = newLayout.AxisLayout;
                    currentTemplate.Layout.ButtonLayout = newLayout.ButtonLayout;
                    currentTemplate.Layout.GyroLayout = newLayout.GyroLayout;

                    currentTemplate.Name = layoutTemplate.Name;
                    currentTemplate.Description = layoutTemplate.Description;
                    currentTemplate.Guid = layoutTemplate.Guid; // not needed

                    // the whole layout has been updated without notification, trigger one
                    currentTemplate.Layout.UpdateLayout();

                    UpdatePages();
                }
                break;
        }
    }

    private void cB_Layouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ButtonApplyLayout.IsEnabled = cB_Layouts.SelectedIndex != -1;
    }

    private void CheckBoxDeviceLayouts_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("LayoutFilterOnDevice", CheckBoxDeviceLayouts.IsChecked);
    }

    private void LayoutExportButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutTemplate newLayout = new()
        {
            Layout = currentTemplate.Layout,
            Name = LayoutTitle.Text,
            Description = LayoutDescription.Text,
            Author = LayoutAuthor.Text,
            Executable = SaveGameInfo.IsChecked == true ? currentTemplate.Executable : "",
            Product = SaveGameInfo.IsChecked == true ? currentTemplate.Product : "",
            IsInternal = false
        };

        if (newLayout.Name == string.Empty)
        {
            LayoutFlyout.Hide();

            // todo: translate me
            _ = new Dialog(MainWindow.GetCurrent())
            {
                Title = "Layout template name cannot be empty",
                Content = "Layout was not exported.",
                PrimaryButtonText = Properties.Resources.ProfilesPage_OK
            }.ShowAsync();

            return;
        }

        if (ExportForCurrent.IsChecked == true)
            newLayout.ControllerType = ControllerManager.GetTargetController()?.GetType();

        LayoutManager.SerializeLayoutTemplate(newLayout);

        // todo: translate me
        _ = new Dialog(MainWindow.GetCurrent())
        {
            Title = "Layout template exported",
            Content = $"{newLayout.Name} was exported.",
            PrimaryButtonText = Properties.Resources.ProfilesPage_OK
        }.ShowAsync();
    }

    private void Flyout_Opening(object sender, object e)
    {
        if (currentTemplate.Executable == string.Empty && currentTemplate.Product == string.Empty)
            SaveGameInfo.IsChecked = SaveGameInfo.IsEnabled = false;
        else
            SaveGameInfo.IsChecked = SaveGameInfo.IsEnabled = true;

        var separator = currentTemplate.Name.Length > 0 && currentTemplate.Product.Length > 0 ? " - " : "";

        LayoutTitle.Text = $"{currentTemplate.Name}{separator}{currentTemplate.Product}";
        LayoutDescription.Text = currentTemplate.Description;
        LayoutAuthor.Text = currentTemplate.Author;
    }

    private void SaveGameInfo_Toggled(object sender, RoutedEventArgs e)
    {
        if (SaveGameInfo.IsChecked == true)
        {
            var separator = currentTemplate.Name.Length > 0 && currentTemplate.Product.Length > 0 ? " - " : "";
            LayoutTitle.Text = $"{currentTemplate.Name}{separator}{currentTemplate.Product}";
        }
        else
        {
            LayoutTitle.Text = $"{currentTemplate.Name}";
        }
    }

    private void LayoutCancelButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutFlyout.Hide();
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is null)
            return;

        NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
        preNavItemTag = (string)navItem.Tag;
        NavView_Navigate(preNavItemTag);
    }

    public void NavView_Navigate(string navItemTag)
    {
        var item = pages.FirstOrDefault(p => p.Key.Equals(navItemTag));
        Page _page = item.Value.Item1;

        // Get the page type before navigation so you can prevent duplicate
        // entries in the backstack.
        var preNavPageType = ContentFrame.CurrentSourcePageType;

        // Only navigate if the selected page isn't currently loaded.
        if (!(_page is null) && !Equals(preNavPageType, _page)) NavView_Navigate(_page);
    }

    public void NavView_Navigate(Page _page)
    {
        ContentFrame.Navigate(_page);
    }

    private void navView_Loaded(object sender, RoutedEventArgs e)
    {
        // Add handler for ContentFrame navigation.
        ContentFrame.Navigated += On_Navigated;

        // NavView doesn't load any page by default, so load home page.
        navView.SelectedItem = navView.MenuItems[0];

        // If navigation occurs on SelectionChanged, this isn't needed.
        // Because we use ItemInvoked to navigate, we need to call Navigate
        // here to load the home page.
        preNavItemTag = "ButtonsPage";
        NavView_Navigate(preNavItemTag);
    }

    private void On_Navigated(object sender, NavigationEventArgs e)
    {
        navView.IsBackEnabled = ContentFrame.CanGoBack;

        if (ContentFrame.SourcePageType is not null)
        {
            var preNavPageType = ContentFrame.CurrentSourcePageType;
            var preNavPageName = preNavPageType.Name;

            var NavViewItem = navView.MenuItems
                .OfType<NavigationViewItem>().FirstOrDefault(n => n.Tag.Equals(preNavPageName));

            if (!(NavViewItem is null))
                navView.SelectedItem = NavViewItem;

            string header = currentTemplate.Product.Length > 0 ?
                    "Profile: " + currentTemplate.Product : "Layout: Desktop";
            parentNavView.Header = new TextBlock() { Text = header };
        }
    }

    private void CheckBoxDefaultLayout_Checked(object sender, RoutedEventArgs e)
    {
        var isDefaultLayout = (bool)CheckBoxDefaultLayout.IsChecked;
        var prevDefaultLayoutProfile = ProfileManager.GetProfileWithDefaultLayout();

        currentTemplate.Layout.IsDefaultLayout = isDefaultLayout;
        currentTemplate.Layout.UpdateLayout();

        // If option is enabled and a different default layout profile exists, we want to set option false on prev profile.
        if (isDefaultLayout && prevDefaultLayoutProfile != null && prevDefaultLayoutProfile.Layout != currentTemplate.Layout)
        {
            prevDefaultLayoutProfile.Layout.IsDefaultLayout = false;
            ProfileManager.UpdateOrCreateProfile(prevDefaultLayoutProfile);
        }
    }
}