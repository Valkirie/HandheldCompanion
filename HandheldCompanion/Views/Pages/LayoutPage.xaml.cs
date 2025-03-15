using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    protected object updateLock = new();

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
        DataContext = new LayoutPageViewModel(this);
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

        foreach(ILayoutPage page in pages.Values.Select(p => p.Item1))
        {
            if (page.DataContext is BaseViewModel baseViewModel)
                baseViewModel.PropertyChanged += (sender, e) => BaseViewModel_PropertyChanged(page, e);

            // force raise event, in case page is already loaded
            BaseViewModel_PropertyChanged(page, new PropertyChangedEventArgs("IsEnabled"));
        }

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ManagerFactory.profileManager.Updated += ProfileManager_Updated;
    }

    private void BaseViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch(e.PropertyName)
        {
            case "IsEnabled":
                break;
            default:
                return;
        }

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            if (sender is ILayoutPage layoutPage)
            {
                string key = pages.FirstOrDefault(kvp => kvp.Value.Item1 == layoutPage).Key;
                NavigationViewItem navItem = pages[key].Item2;
                navItem.IsEnabled = layoutPage.IsEnabled();
            }
        });
    }

    private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
    {
        if (!MainWindow.CurrentPageName.Equals("LayoutPage"))
            return;

        UIHelper.TryInvoke(() =>
        {
            switch (source)
            {
                case UpdateSource.QuickProfilesPage:
                    {
                        if (ProfilesPage.selectedProfile.Name.Equals(profile.Name))
                            UpdateLayout(profile.Layout);
                    }
                    break;
            }
        });
    }

    private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (name)
            {
                case "LayoutFilterOnDevice":
                    CheckBoxDeviceLayouts.IsChecked = Convert.ToBoolean(value);
                    break;
            }
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
        ((LayoutPageViewModel)DataContext).Dispose();

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
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
        currentTemplate = layoutTemplate;
        UpdatePages();
    }

    private void UpdatePages()
    {
        // This is a very important lock, it blocks backward events to the layout when
        // this is actually the backend that triggered the update. Notifications on higher
        // levels (pages and mappings) could potentially be blocked for optimization.
        lock (updateLock)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                // Invoke Layout Updated to trigger ViewModel updates
                LayoutUpdated?.Invoke(currentTemplate.Layout);

                // clear layout selection
                cB_Layouts.SelectedValue = null;
            });
        }
    }

    private async void ButtonApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if (cB_Layouts.SelectedItem is LayoutTemplateViewModel layoutTemplateViewModel)
        {
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
                        // get template
                        LayoutTemplate layoutTemplate = layoutTemplateViewModel.LayoutTemplate;

                        // do not overwrite currentTemplate and currentTemplate.Layout as a whole
                        // because they both have important Update notifitications set
                        using (Layout? newLayout = layoutTemplate.Layout.Clone() as Layout)
                        {
                            currentTemplate.Layout.AxisLayout = CloningHelper.DeepClone(newLayout.AxisLayout);
                            currentTemplate.Layout.ButtonLayout = CloningHelper.DeepClone(newLayout.ButtonLayout);
                            currentTemplate.Layout.GyroLayout = CloningHelper.DeepClone(newLayout.GyroLayout);
                        }

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
    }

    private void cB_Layouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ButtonApplyLayout.IsEnabled = cB_Layouts.SelectedIndex != -1;
    }

    private void CheckBoxDeviceLayouts_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("LayoutFilterOnDevice", CheckBoxDeviceLayouts.IsChecked);
    }

    private void LayoutExportButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutFlyout.Hide();

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
            newLayout.ControllerType = ControllerManager.GetTarget()?.GetType();

        ManagerFactory.layoutManager.SerializeLayoutTemplate(newLayout);

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
}