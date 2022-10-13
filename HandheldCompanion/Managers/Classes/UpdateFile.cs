using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Managers.Classes
{
    public class UpdateFile
    {
        public short idx;
        public Uri uri;
        public int filesize;
        public string filename;
        public bool debug;

        // UI vars
        public Border updateBorder;
        public Grid updateGrid;
        public TextBlock updateFilename;
        public TextBlock updatePercentage;
        public Button updateDownload;
        public Button updateInstall;

        public Border Draw()
        {
            updateBorder = new Border()
            {
                Padding = new Thickness(20, 12, 12, 12),
                Background = (Brush)Application.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"],
                Tag = filename
            };

            // Create Grid
            updateGrid = new();

            // Define the Columns
            ColumnDefinition colDef1 = new ColumnDefinition()
            {
                Width = new GridLength(5, GridUnitType.Star),
                MinWidth = 200
            };
            updateGrid.ColumnDefinitions.Add(colDef1);

            ColumnDefinition colDef2 = new ColumnDefinition()
            {
                Width = new GridLength(3, GridUnitType.Star),
                MinWidth = 120
            };
            updateGrid.ColumnDefinitions.Add(colDef2);

            // Create TextBlock
            updateFilename = new TextBlock()
            {
                FontSize = 14,
                Text = filename,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(updateFilename, 0);
            updateGrid.Children.Add(updateFilename);

            // Create TextBlock
            updatePercentage = new TextBlock()
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(updatePercentage, 1);
            updateGrid.Children.Add(updatePercentage);

            // Create Download Button
            updateDownload = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_Download,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Grid.SetColumn(updateDownload, 1);
            updateGrid.Children.Add(updateDownload);

            // Create Install Button
            updateInstall = new Button()
            {
                FontSize = 14,
                Content = Properties.Resources.SettingsPage_InstallNow,
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
}
