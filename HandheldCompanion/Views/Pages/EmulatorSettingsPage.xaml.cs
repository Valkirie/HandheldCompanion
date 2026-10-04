using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Views.Pages;

public partial class EmulatorSettingsPage : Page
{
    private readonly ViewModels.EmulatorSettingsPageViewModel _viewModel;
    private bool _updatingColor;

    public EmulatorSettingsPage()
    {
        _viewModel = new ViewModels.EmulatorSettingsPageViewModel();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        InitializeComponent();
        UpdateColorPicker();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.EmulatorSettingsPageViewModel.Definition))
            UpdateColorPicker();
    }

    private void UpdateColorPicker()
    {
        if (PlatformColorPicker is null || _viewModel.Definition is null)
            return;

        try
        {
            _updatingColor = true;
            PlatformColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(_viewModel.Definition.PlatformColor);
        }
        catch (FormatException)
        {
        }
        finally
        {
            _updatingColor = false;
        }
    }

    private void PlatformColorPicker_ColorChanged(object sender, RoutedEventArgs e)
    {
        if (!_updatingColor && _viewModel.Definition is not null)
            _viewModel.Definition.PlatformColor = PlatformColorPicker.SelectedColor.ToString();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true)
            NavigationService.GoBack();
    }
}
