using System;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public partial class LibraryPageViewModel
    {
        private LibraryNavigationItemViewModel? _selectedNavigationItem;

        public LibraryNavigationItemViewModel? SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value))
                {
                    OnPropertyChanged(nameof(NavigationViewSelectedItem));
                    OnPropertyChanged(nameof(IsCollectionsOverviewSelected));
                    OnPropertyChanged(nameof(IsSingleCollectionSelection));
                    OnPropertyChanged(nameof(ShowProfilesCards));
                    OnPropertyChanged(nameof(ShowProfilesList));
                    OnPropertyChanged(nameof(ShowGroupedProfilesList));
                    OnPropertyChanged(nameof(ShowCollectionsOverview));
                    UpdateFiltering();
                    EnsureCollectionGroupsReady();
                    BackAvailabilityChanged?.Invoke(CanGoBack);
                }
            }
        }

        public LibraryNavigationItemViewModel? NavigationViewSelectedItem
        {
            get => SelectedNavigationItem;
            set
            {
                // Prevent auto-selection of disabled trigger glyphs by the NavigationView
                if (value?.Kind == LibraryNavigationItemKind.TriggerGlyph)
                {
                    OnPropertyChanged(nameof(NavigationViewSelectedItem));
                    return;
                }

                // The getter returns the "Collections" parent item when a specific collection is active,
                // so the navView's TwoWay binding can back-write the parent item when the page re-enters
                // the frame (e.g. after ContentFrame.GoBack()). Guard against that: if the navView
                // reports the collections root as selected but a specific collection is already active,
                // do not override it. Explicit user navigation goes through navView_ItemInvoked →
                // SelectNavigationItemByKey which sets SelectedNavigationItem directly.
                if (value?.Kind == LibraryNavigationItemKind.CollectionsRoot
                    && SelectedNavigationItem?.Kind == LibraryNavigationItemKind.Collection)
                    return;

                SelectedNavigationItem = value;
            }
        }

        public bool IsCollectionsOverviewSelected => SelectedNavigationItem?.Kind == LibraryNavigationItemKind.CollectionsRoot;

        public event Action<bool>? BackAvailabilityChanged;
        public event Action? CollectionOpened;
        public event Action? NavigatedBackToCollections;

        public bool CanGoBack => IsSingleCollectionSelection
            && !string.Equals(SelectedNavigationItem?.Key, FavoritesNavigationKey, StringComparison.Ordinal);

        public bool IsCollectionsOverviewNavigationKey(string? key)
        {
            return string.Equals(key, CollectionsNavigationKey, StringComparison.Ordinal);
        }

        public string? GetCollectionsOverviewItemKey(CollectionGroupViewModel? group)
        {
            if (group is null)
                return null;

            if (group.Collection is not null)
                return $"collection:{group.Collection.Id}";

            return string.Equals(group.Name, "Favorites", StringComparison.Ordinal)
                ? FavoritesNavigationKey
                : null;
        }

        public void RememberCollectionsOverviewItem(CollectionGroupViewModel? group)
        {
            string? key = GetCollectionsOverviewItemKey(group);
            if (!string.IsNullOrWhiteSpace(key))
                _lastCollectionsOverviewItemKey = key;
        }

        public string? GetLastCollectionsOverviewItemKey()
        {
            if (string.IsNullOrWhiteSpace(_lastCollectionsOverviewItemKey))
            {
                _lastCollectionsOverviewItemKey = CollectionGroups
                    .Select(GetCollectionsOverviewItemKey)
                    .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key));
            }

            return _lastCollectionsOverviewItemKey;
        }

        private void OpenCollection(CollectionGroupViewModel group)
        {
            RememberCollectionsOverviewItem(group);

            string? collectionKey = GetCollectionsOverviewItemKey(group);
            LibraryNavigationItemViewModel? collectionItem = FindNavigationItemByKey(collectionKey);

            if (collectionItem is not null)
            {
                SelectedNavigationItem = collectionItem;
                CollectionOpened?.Invoke();
            }
        }

        public bool SelectNavigationItemByKey(string? key)
        {
            LibraryNavigationItemViewModel? selectedItem = FindNavigationItemByKey(key);
            if (selectedItem is null || !selectedItem.IsVisible)
                return false;

            SelectedNavigationItem = selectedItem;
            return true;
        }

        public bool TryGoBack()
        {
            if (!CanGoBack)
                return false;

            LibraryNavigationItemViewModel? collectionsItem = FindNavigationItemByKey(CollectionsNavigationKey);
            if (collectionsItem is null)
                return false;

            SelectedNavigationItem = collectionsItem;
            NavigatedBackToCollections?.Invoke();
            return true;
        }
    }
}
