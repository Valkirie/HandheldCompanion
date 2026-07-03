using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
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
    public new event UpdatedLayoutHandler? LayoutUpdated;
    public delegate void UpdatedLayoutHandler(Layout layout);

    // Getter to update layout in ViewModels
    public Layout CurrentLayout => currentTemplate.Layout;
    public LayoutTemplate currentTemplate = new();
    protected object updateLock = new();

    // page vars
    private Dictionary<string, (ILayoutPage, NavigationViewItem)>? pages;
    private ButtonsPage? buttonsPage;
    private DpadPage? dpadPage;
    private GyroPage? gyroPage;
    private JoysticksPage? joysticksPage;
    private TrackpadsPage? trackpadsPage;
    private TriggersPage? triggersPage;

    private NavigationView? parentNavView;
    private string preNavItemTag = string.Empty;
    private string _prevNavItemTag = string.Empty;

    public LayoutPage()
    {
        DataContext = new LayoutPageViewModel(this);
        InitializeComponent();
    }

    public LayoutPage(string Tag, NavigationView parent) : this()
    {
        this.Tag = Tag;
        this.parentNavView = parent;

        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        // raise events
        switch (ManagerFactory.profileManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryProfile();
                break;
        }

        // manage events
        ControllerManager.Initialized += ControllerManager_Initialized;

        // raise events
        if (ControllerManager.IsInitialized)
            ControllerManager_Initialized();
    }

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

        foreach (ILayoutPage page in pages.Values.Select(p => p.Item1))
        {
            if (page.DataContext is BaseViewModel baseViewModel)
                baseViewModel.PropertyChanged += (sender, e) => BaseViewModel_PropertyChanged(page, e);

            // force raise event, in case page is already loaded
            BaseViewModel_PropertyChanged(page, new PropertyChangedEventArgs("IsEnabled"));
        }
    }

    private void QueryProfile()
    {
        // manage events
        ManagerFactory.profileManager.Updated += ProfileManager_Updated;

        // do something ?
    }

    private void ProfileManager_Initialized()
    {
        QueryProfile();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("LayoutFilterOnDevice", ManagerFactory.settingsManager.GetString("LayoutFilterOnDevice"), false, false);
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void ControllerManager_Initialized()
    {
        // manage events
        ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

        // raise events
        if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
            ControllerManager_ControllerSelected(controller);
    }

    private void ControllerManager_ControllerSelected(IController? Controller)
    {
        if (Controller is null)
            return;

        // UI thread (async to prevent blocking event callers)
        UIHelper.TryBeginInvoke(() =>
        {
            L2.Glyph = Controller.GetGlyph(AxisFlags.L2);
            R2.Glyph = Controller.GetGlyph(AxisFlags.R2);
        });
    }

    private void BaseViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
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
                if (pages is null)
                    return;

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
                        if (ProfilesPage.selectedProfile != null &&
                            ProfilesPage.selectedProfile.Name.Equals(profile.Name))
                            UpdateLayout(profile.Layout);
                    }
                    break;
            }
        });
    }

    private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
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
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
        ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
    }

    public void UpdateLayout(Layout layout)
    {
        currentTemplate.Layout = layout;
        UpdatePages();
    }

    public void UpdateLayoutTemplate(LayoutTemplate layoutTemplate)
    {
        currentTemplate = layoutTemplate;

        // Update ViewModel properties to reflect current template
        var viewModel = DataContext as LayoutPageViewModel;
        if (viewModel is not null)
        {
            viewModel.LayoutName = layoutTemplate.Name;
            viewModel.LayoutDescription = layoutTemplate.Description;
            viewModel.LayoutAuthor = layoutTemplate.Author;
        }

        UpdatePages();
    }

    private void UpdatePages()
    {
        // This is a very important lock, it blocks backward events to the layout when
        // this is actually the backend that triggered the update. Notifications on higher
        // levels (pages and mappings) could potentially be blocked for optimization.
        UIHelper.TryBeginInvoke(() =>
        {
            lock (updateLock)
            {
                // Invoke Layout Updated to trigger ViewModel updates
                LayoutUpdated?.Invoke(currentTemplate.Layout);

                // clear layout selection
                cB_Layouts.SelectedValue = null;
            }
        });
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
                        using (Layout newLayout = (Layout)layoutTemplate.Layout.Clone())
                        {
                            currentTemplate.Layout.AxisLayout = CloningHelper.DeepClone(newLayout.AxisLayout);
                            currentTemplate.Layout.ButtonLayout = CloningHelper.DeepClone(newLayout.ButtonLayout);
                            currentTemplate.Layout.GyroLayout = CloningHelper.DeepClone(newLayout.GyroLayout);
                        }

                        currentTemplate.Name = layoutTemplate.Name;
                        currentTemplate.Description = layoutTemplate.Description;
                        currentTemplate.Guid = layoutTemplate.Guid; // not needed

                        // Update ViewModel properties to reflect newly applied template
                        var viewModel = DataContext as LayoutPageViewModel;
                        if (viewModel is not null)
                        {
                            viewModel.LayoutName = layoutTemplate.Name;
                            viewModel.LayoutDescription = layoutTemplate.Description;
                            viewModel.LayoutAuthor = layoutTemplate.Author;
                        }

                        // the whole layout has been updated without notification, trigger one
                        currentTemplate.Layout.UpdateLayout();

                        UpdatePages();
                    }
                    break;
            }
        }
    }

    private async void ButtonResetLayout_Click(object sender, RoutedEventArgs e)
    {
        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = "Reset layout",
            Content = "Are you sure you want to reset the layout to its default configuration?",
            DefaultButton = ContentDialogButton.Close,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                {
                    // Get the current profile to determine if it's a default profile
                    Profile currentProfile = ProfilesPage.selectedProfile;

                    // Clear the layout
                    currentTemplate.Layout.ButtonLayout.Clear();
                    currentTemplate.Layout.AxisLayout.Clear();
                    currentTemplate.Layout.GyroLayout.Clear();

                    // Fill with appropriate defaults
                    if (currentProfile.Default)
                        currentTemplate.Layout.FillDefault();
                    else
                        currentTemplate.Layout.FillInherit();

                    currentTemplate.Name = LayoutTemplate.DefaultLayout.Name;

                    // Update ViewModel properties
                    var viewModel = DataContext as LayoutPageViewModel;
                    if (viewModel is not null)
                    {
                        viewModel.LayoutName = LayoutTemplate.DefaultLayout.Name;
                        viewModel.LayoutDescription = LayoutTemplate.DefaultLayout.Description;
                        viewModel.LayoutAuthor = LayoutTemplate.DefaultLayout.Author;
                    }

                    // Trigger layout update notification
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

        ManagerFactory.settingsManager.SetProperty("LayoutFilterOnDevice", CheckBoxDeviceLayouts.IsChecked);
    }

    private void LayoutExportButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutExportFlyout.Hide();

        var viewModel = DataContext as LayoutPageViewModel;
        if (viewModel is null)
            return;

        LayoutTemplate newLayout = new()
        {
            Layout = currentTemplate.Layout,
            Name = viewModel.LayoutName,
            Description = viewModel.LayoutDescription,
            Author = viewModel.LayoutAuthor,
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
        {
            IController? target = ControllerManager.GetTarget();
            if (target is not null)
                newLayout.DeviceName = target.GetType().Name;
        }

        ManagerFactory.layoutManager.SerializeLayoutTemplate(newLayout);

        // todo: translate me
        _ = new Dialog(MainWindow.GetCurrent())
        {
            Title = "Layout template exported",
            Content = $"{newLayout.Name} was exported.",
            PrimaryButtonText = Properties.Resources.ProfilesPage_OK
        }.ShowAsync();
    }

    private void ExportFlyout_Opening(object sender, object e)
    {
        if (currentTemplate.Executable == string.Empty && currentTemplate.Product == string.Empty)
            SaveGameInfo.IsChecked = SaveGameInfo.IsEnabled = false;
        else
            SaveGameInfo.IsChecked = SaveGameInfo.IsEnabled = true;
    }

    private void SaveGameInfo_Toggled(object sender, RoutedEventArgs e)
    {
        // Checkboxes updated dynamically
    }

    private void LayoutSettingsFlyout_Opening(object sender, object e)
    {
        // Load current profile values into ViewModel properties
        if (ProfilesPage.selectedProfile != null)
        {
            var viewModel = DataContext as LayoutPageViewModel;
            if (viewModel is not null)
            {
                viewModel.LayoutName = ProfilesPage.selectedProfile.LayoutTitle;
                viewModel.LayoutDescription = ProfilesPage.selectedProfile.LayoutDescription;
                viewModel.LayoutAuthor = ProfilesPage.selectedProfile.LayoutAuthor;
            }
        }
    }

    private void LayoutSettingsConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        // Save edited values to profile and close the flyout
        if (ProfilesPage.selectedProfile != null)
        {
            var viewModel = DataContext as LayoutPageViewModel;
            if (viewModel is not null)
            {
                ProfilesPage.selectedProfile.LayoutTitle = viewModel.LayoutName;
                ProfilesPage.selectedProfile.LayoutDescription = viewModel.LayoutDescription;
                ProfilesPage.selectedProfile.LayoutAuthor = viewModel.LayoutAuthor;

                // Save the profile with changes
                ManagerFactory.profileManager.UpdateOrCreateProfile(ProfilesPage.selectedProfile, UpdateSource.LayoutPage);
            }
        }

        LayoutSettingsFlyout.Hide();
    }

    private void LayoutSettingsCancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Discard edits by reloading from profile
        if (ProfilesPage.selectedProfile != null)
        {
            var viewModel = DataContext as LayoutPageViewModel;
            if (viewModel is not null)
            {
                viewModel.LayoutName = ProfilesPage.selectedProfile.LayoutTitle;
                viewModel.LayoutDescription = ProfilesPage.selectedProfile.LayoutDescription;
                viewModel.LayoutAuthor = ProfilesPage.selectedProfile.LayoutAuthor;
            }
        }

        LayoutSettingsFlyout.Hide();
    }

    private void LayoutExportCancelButton_Click(object sender, RoutedEventArgs e)
    {
        LayoutExportFlyout.Hide();
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is null)
            return;

        NavigationViewItem navItem = (NavigationViewItem)args.InvokedItemContainer;
        _prevNavItemTag = preNavItemTag;
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
        ContentFrame.Navigated -= On_Navigated;
        ContentFrame.Navigated += On_Navigated;

        if (ContentFrame.Content is not null)
        {
            if (ContentFrame.SourcePageType is not null)
            {
                string currentPageName = ContentFrame.CurrentSourcePageType.Name;
                var currentNavViewItem = navView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(n => n.Tag is not null && n.Tag.Equals(currentPageName));
                if (currentNavViewItem is not null)
                    navView.SelectedItem = currentNavViewItem;
            }

            return;
        }

        // NavView doesn't load any page by default, so load home page.
        navView.SelectedItem = navView.MenuItems[0];

        // If navigation occurs on SelectionChanged, this isn't needed.
        // Because we use ItemInvoked to navigate, we need to call Navigate
        // here to load the home page.
        preNavItemTag = "ButtonsPage";
        NavView_Navigate(preNavItemTag);
    }

    public bool TryGoBack()
    {
        if (!navView.IsBackEnabled)
            return false;

        if (!ContentFrame.CanGoBack)
            return false;

        ContentFrame.GoBack();
        return true;
    }

    private void On_Navigated(object sender, NavigationEventArgs e)
    {
        if (ContentFrame.SourcePageType is not null)
        {
            var preNavPageType = ContentFrame.CurrentSourcePageType;
            var preNavPageName = preNavPageType.Name;
            preNavItemTag = preNavPageName;

            var NavViewItem = navView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(n => n.Tag is not null && n.Tag.Equals(preNavPageName));

            if (!(NavViewItem is null))
                navView.SelectedItem = NavViewItem;

            string header = currentTemplate.Product.Length > 0 ? $"{Properties.Resources.LayoutPage_Profile}: " + currentTemplate.Product : $"{Properties.Resources.LayoutPage_LaytouDesktop}";
            parentNavView?.Header = new TextBlock() { Text = header };
        }
    }
}