using System;
using System.Windows;
using System.Windows.Controls;
using HandheldCompanion.Properties;

namespace HandheldCompanion.Managers;

public class UpdateFile
{
    public bool debug;
    public string filename;
    public int filesize;
    public short idx;

    // UI vars
    public Border updateBorder;
    public Button updateDownload;
    public TextBlock updateFilename;
    public Grid updateGrid;
    public Button updateInstall;
    public TextBlock updatePercentage;
    public Uri uri;

    public Border Draw()
    {
        updateBorder = new Border
        {
            Padding = new Thickness(20, 12, 12, 12),
            Tag = filename
        };
        updateBorder.SetResourceReference(Control.BackgroundProperty, "ExpanderHeaderBackground");

        // Create Grid
        updateGrid = new Grid();

        // Define the Columns
        var colDef1 = new ColumnDefinition
        {
            Width = new GridLength(5, GridUnitType.Star),
            MinWidth = 200
        };
        updateGrid.ColumnDefinitions.Add(colDef1);

        var colDef2 = new ColumnDefinition
        {
            Width = new GridLength(3, GridUnitType.Star),
            MinWidth = 120
        };
        updateGrid.ColumnDefinitions.Add(colDef2);

        // Create TextBlock
        updateFilename = new TextBlock
        {
            FontSize = 14,
            Text = filename,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(updateFilename, 0);
        updateGrid.Children.Add(updateFilename);

        // Create TextBlock
        updatePercentage = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(updatePercentage, 1);
        updateGrid.Children.Add(updatePercentage);

        // Create Download Button
        updateDownload = new Button
        {
            FontSize = 14,
            Content = Resources.SettingsPage_Download,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        Grid.SetColumn(updateDownload, 1);
        updateGrid.Children.Add(updateDownload);

        // Create Install Button
        updateInstall = new Button
        {
            FontSize = 14,
            Content = Resources.SettingsPage_InstallNow,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Collapsed,
            Style = Application.Current.FindResource("AccentButtonStyle") as Style
        };

        Grid.SetColumn(updateInstall, 1);
        updateGrid.Children.Add(updateInstall);

        updateBorder.Child = updateGrid;

        return updateBorder;
    }
}