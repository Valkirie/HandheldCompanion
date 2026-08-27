using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class CollectionGroupViewModel : BaseViewModel, IComparable<CollectionGroupViewModel>, IComparable
    {
        private readonly Action<CollectionGroupViewModel>? _openAction;

        public GameCollection? Collection { get; }
        private readonly string _builtInName;

        public string Name => Collection?.Name ?? _builtInName;

        public bool IsBuiltIn => Collection is null || Collection.IsBuiltIn;
        public bool CanDelete => !IsBuiltIn;

        public ObservableCollection<ProfileViewModel> Profiles { get; } = [];
        public ObservableCollection<BitmapImage> PreviewArtworks { get; } = [];
        public ObservableCollection<BitmapImage> PreviewCovers { get; } = [];
        public ICommand? OpenCommand { get; }

        public bool HasPreviewArtworks => PreviewArtworks.Count > 0;
        public bool HasPreviewCovers => PreviewCovers.Count > 0;
        public int ProfileCount => Profiles.Count;
        public int PreviewArtworkCount => PreviewArtworks.Count;
        public int PreviewCoverCount => PreviewCovers.Count;
        public bool ShowInCollectionsOverview => Collection is not null || Name == "Favorites";
        public BitmapImage? PreviewArtwork1 => GetPreviewArtworkAt(0);
        public BitmapImage? PreviewArtwork2 => GetPreviewArtworkAt(1);
        public BitmapImage? PreviewArtwork3 => GetPreviewArtworkAt(2);
        public BitmapImage? PreviewArtwork4 => GetPreviewArtworkAt(3);
        public BitmapImage? PreviewCover1 => GetPreviewCoverAt(0);
        public BitmapImage? PreviewCover2 => GetPreviewCoverAt(1);
        public BitmapImage? PreviewCover3 => GetPreviewCoverAt(2);
        public BitmapImage? PreviewCover4 => GetPreviewCoverAt(3);
        public BitmapImage? PreviewHorizontal1 => GetHorizontalPreviewAt(0);
        public BitmapImage? PreviewHorizontal2 => GetHorizontalPreviewAt(1);
        public BitmapImage? PreviewHorizontal3 => GetHorizontalPreviewAt(2);
        public BitmapImage? PreviewHorizontal4 => GetHorizontalPreviewAt(3);
        public BitmapImage? PreviewVertical1 => GetVerticalPreviewAt(0);
        public BitmapImage? PreviewVertical2 => GetVerticalPreviewAt(1);
        public BitmapImage? PreviewVertical3 => GetVerticalPreviewAt(2);
        public BitmapImage? PreviewVertical4 => GetVerticalPreviewAt(3);

        public ICommand? DeleteCommand { get; }

        // For built-in groups (Favorites, Other)
        public CollectionGroupViewModel(string builtInName, Action<CollectionGroupViewModel>? openAction = null)
        {
            _builtInName = builtInName;
            _openAction = openAction;

            OpenCommand = new DelegateCommand(() =>
            {
                _openAction?.Invoke(this);
            });
        }

        // For user-created collections
        public CollectionGroupViewModel(GameCollection collection, Action<CollectionGroupViewModel>? openAction = null)
        {
            Collection = collection;
            _builtInName = string.Empty;
            _openAction = openAction;

            DeleteCommand = new DelegateCommand(async () =>
            {
                Dialog dialog = new Dialog(MainWindow.GetCurrent())
                {
                    Title = string.Format(Resources.ProfilesPage_AreYouSureDelete1, collection.Name),
                    Content = Resources.ProfilesPage_AreYouSureDelete2,
                    CloseButtonText = Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Resources.ProfilesPage_Delete
                };

                ContentDialogResult result = await dialog.ShowAsync();
                switch (result)
                {
                    case ContentDialogResult.None:
                        dialog.Hide();
                        break;
                    case ContentDialogResult.Primary:
                        ManagerFactory.profileManager.DeleteCollection(collection.Id);
                        break;
                }
            });

            OpenCommand = new DelegateCommand(() =>
            {
                _openAction?.Invoke(this);
            });
        }

        public void SetPreviewProfiles(IEnumerable<ProfileViewModel> profiles)
        {
            // Clear immediately so the UI shows empty/placeholder state without blocking
            PreviewArtworks.Clear();
            PreviewCovers.Clear();
            NotifyPreviewProperties();

            // Snapshot the profiles list so the lambda captures a stable collection
            List<ProfileViewModel> snapshot = profiles.ToList();

            // Capture the UI scheduler before leaving the UI thread
            TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            // Load bitmaps (file I/O + decode) on a background thread, then update collections on the UI thread
            Task.Run(() =>
            {
                List<BitmapImage> artworks = snapshot
                    .Select(GetPreviewArtwork)
                    .Cast<BitmapImage>()
                    .ToList();

                List<BitmapImage> covers = snapshot
                    .Select(GetPreviewCover)
                    .Cast<BitmapImage>()
                    .ToList();

                return (artworks, covers);
            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                    return;

                (List<BitmapImage> artworks, List<BitmapImage> covers) = t.Result;

                PreviewArtworks.Clear();
                PreviewCovers.Clear();

                foreach (BitmapImage artwork in artworks)
                    PreviewArtworks.Add(artwork);

                foreach (BitmapImage cover in covers)
                    PreviewCovers.Add(cover);

                NotifyPreviewProperties();
            }, uiScheduler);
        }

        private void NotifyPreviewProperties()
        {
            OnPropertyChanged(nameof(HasPreviewArtworks));
            OnPropertyChanged(nameof(HasPreviewCovers));
            OnPropertyChanged(nameof(ProfileCount));
            OnPropertyChanged(nameof(PreviewArtworkCount));
            OnPropertyChanged(nameof(PreviewCoverCount));
            OnPropertyChanged(nameof(PreviewArtwork1));
            OnPropertyChanged(nameof(PreviewArtwork2));
            OnPropertyChanged(nameof(PreviewArtwork3));
            OnPropertyChanged(nameof(PreviewArtwork4));
            OnPropertyChanged(nameof(PreviewCover1));
            OnPropertyChanged(nameof(PreviewCover2));
            OnPropertyChanged(nameof(PreviewCover3));
            OnPropertyChanged(nameof(PreviewCover4));
            OnPropertyChanged(nameof(PreviewHorizontal1));
            OnPropertyChanged(nameof(PreviewHorizontal2));
            OnPropertyChanged(nameof(PreviewHorizontal3));
            OnPropertyChanged(nameof(PreviewHorizontal4));
            OnPropertyChanged(nameof(PreviewVertical1));
            OnPropertyChanged(nameof(PreviewVertical2));
            OnPropertyChanged(nameof(PreviewVertical3));
            OnPropertyChanged(nameof(PreviewVertical4));
            OnPropertyChanged(nameof(ShowInCollectionsOverview));
        }

        private static BitmapImage GetPreviewArtwork(ProfileViewModel profile)
        {
            if (profile.Profile.LibraryEntry is not null)
            {
                BitmapImage? artwork = ManagerFactory.libraryManager.GetGameArt(
                    profile.Profile.LibraryEntry.Id,
                    LibraryManager.LibraryType.artwork | LibraryManager.LibraryType.thumbnails,
                    profile.Profile.LibraryEntry.GetArtworkId(),
                    profile.Profile.LibraryEntry.GetArtworkExtension(true));

                if (artwork is not null && artwork != LibraryResources.MissingArtwork)
                    return artwork;
            }

            return profile.Artwork ?? LibraryResources.MissingArtwork;
        }

        private static BitmapImage GetPreviewCover(ProfileViewModel profile)
        {
            if (profile.Profile.LibraryEntry is not null)
            {
                BitmapImage? cover = ManagerFactory.libraryManager.GetGameArt(
                    profile.Profile.LibraryEntry.Id,
                    LibraryManager.LibraryType.cover | LibraryManager.LibraryType.thumbnails,
                    profile.Profile.LibraryEntry.GetCoverId(),
                    profile.Profile.LibraryEntry.GetCoverExtension(true));

                if (cover is not null && cover != LibraryResources.MissingCover)
                    return cover;
            }

            return profile.Cover ?? LibraryResources.MissingCover;
        }

        private static bool IsValidArtwork(BitmapImage? artwork)
        {
            return artwork is not null && artwork != LibraryResources.MissingArtwork;
        }

        private static bool IsValidCover(BitmapImage? cover)
        {
            return cover is not null && cover != LibraryResources.MissingCover;
        }

        private BitmapImage? GetPreviewArtworkAt(int index)
        {
            return index >= 0 && index < PreviewArtworks.Count ? PreviewArtworks[index] : null;
        }

        private BitmapImage? GetPreviewCoverAt(int index)
        {
            return index >= 0 && index < PreviewCovers.Count ? PreviewCovers[index] : null;
        }

        private BitmapImage? GetHorizontalPreviewAt(int index)
        {
            BitmapImage? artwork = GetPreviewArtworkAt(index);
            if (IsValidArtwork(artwork))
                return artwork;

            BitmapImage? cover = GetPreviewCoverAt(index);
            return IsValidCover(cover) ? cover : artwork ?? cover;
        }

        private BitmapImage? GetVerticalPreviewAt(int index)
        {
            BitmapImage? cover = GetPreviewCoverAt(index);
            if (IsValidCover(cover))
                return cover;

            BitmapImage? artwork = GetPreviewArtworkAt(index);
            return IsValidArtwork(artwork) ? artwork : cover ?? artwork;
        }

        public void RefreshName()
        {
            OnPropertyChanged(nameof(Name));
        }

        public override string ToString() => Name;

        // Favorites first, user collections alphabetically, Other last
        public int CompareTo(CollectionGroupViewModel? other)
        {
            if (other is null) return 1;
            return SortKey().CompareTo(other.SortKey());
        }

        public int CompareTo(object? obj) => CompareTo(obj as CollectionGroupViewModel);

        private string SortKey()
        {
            if (IsBuiltIn && Name == "Favorites") return "0";
            if (IsBuiltIn) return "2_" + Name;
            return "1_" + Name;
        }
    }
}
