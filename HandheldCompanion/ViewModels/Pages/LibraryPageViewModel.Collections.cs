using HandheldCompanion.Controls;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Platforms.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.ViewModels
{
    public partial class LibraryPageViewModel
    {
        private void RebuildNavigationItems()
        {
            UIHelper.TryBeginInvoke(RebuildNavigationItemsInternal);
        }

        private void RebuildNavigationItemsInternal()
        {
            string selectedKey = SelectedNavigationItem?.Key ?? AllGamesNavigationKey;
            HashSet<GamePlatform> availablePlatforms = AvailablePlatforms.ToHashSet();

            if (NavigationItems.Count == 0)
            {
                NavigationItems.Add(_navL2);
                NavigationItems.Add(new LibraryNavigationItemViewModel(AllGamesNavigationKey, "All games", LibraryNavigationItemKind.AllGames));
                NavigationItems.Add(new LibraryNavigationItemViewModel(FavoritesNavigationKey, "Favorites", LibraryNavigationItemKind.Collection));

                foreach ((GamePlatform platform, string title) in SupportedLaunchers)
                    NavigationItems.Add(new LibraryNavigationItemViewModel($"platform:{platform}", title, platform));

                foreach (EmulatorDefinition definition in SupportedEmulators.GroupBy(definition => definition.PlatformType).Select(group => group.First()))
                    NavigationItems.Add(new LibraryNavigationItemViewModel($"platform:{definition.PlatformType}", definition.Name, definition.PlatformType));

                NavigationItems.Add(new LibraryNavigationItemViewModel(CollectionsNavigationKey, "Collections", LibraryNavigationItemKind.CollectionsRoot));
                NavigationItems.Add(_navR2);
            }

            var activeCollections = ManagerFactory.profileManager
                .GetCollections()
                .OrderBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            HashSet<Guid> activeCollectionIds = activeCollections.Select(collection => collection.Id).ToHashSet();

            foreach (var item in NavigationItems)
            {
                if (item.Key == FavoritesNavigationKey)
                {
                    item.IsVisible = HasLiked;
                    item.GameCount = Profiles.Count(profile => profile.IsLiked);
                }
                else if (item.Kind == LibraryNavigationItemKind.Platform)
                {
                    item.IsVisible = availablePlatforms.Contains(item.Platform);
                    item.GameCount = Profiles.Count(profile => profile.PlatformType == item.Platform);
                }
                else if (item.Kind == LibraryNavigationItemKind.AllGames)
                {
                    item.GameCount = Profiles.Count;
                }
                else if (item.Kind == LibraryNavigationItemKind.CollectionsRoot)
                {
                    item.GameCount = Profiles.Count(profile => profile.Profile.Collections.Any(activeCollectionIds.Contains));
                }
            }

            collectionNavigationItems.Clear();

            foreach (GameCollection collection in activeCollections)
            {
                string key = $"collection:{collection.Id}";
                bool isVisible = HasProfilesForCollection(collection.Id);

                LibraryNavigationItemViewModel collectionItem = new(key, collection.Name, collection.Id)
                {
                    IsVisible = isVisible
                };

                collectionNavigationItems[key] = collectionItem;
            }

            LibraryNavigationItemViewModel? selectedItem = FindNavigationItemByKey(selectedKey);

            if (selectedItem is null || !selectedItem.IsVisible)
                selectedItem = NavigationItems.FirstOrDefault(item => item.IsVisible && item.Kind != LibraryNavigationItemKind.TriggerGlyph)
                               ?? NavigationItems.FirstOrDefault(item => item.Kind != LibraryNavigationItemKind.TriggerGlyph);

            SelectedNavigationItem = selectedItem;

            OnPropertyChanged(nameof(NavigationItems));
            OnPropertyChanged(nameof(AvailablePlatforms));

            BackAvailabilityChanged?.Invoke(CanGoBack);
        }

        public LibraryNavigationItemViewModel? FindNavigationItemByKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            foreach (LibraryNavigationItemViewModel item in NavigationItems)
            {
                if (item.Key.Equals(key, StringComparison.Ordinal))
                    return item;
            }

            if (collectionNavigationItems.TryGetValue(key, out LibraryNavigationItemViewModel? collectionItem))
                return collectionItem;

            return null;
        }

        private bool HasProfilesForCollection(Guid collectionId)
        {
            return Profiles.Any(profile => profile.Profile.Collections.Contains(collectionId));
        }

        private void ScheduleRebuildCollectionGroups()
        {
            _collectionGroupsDirty = true;

            if (!ShouldShowCollectionGroups || _rebuildCollectionGroupsPending)
                return;

            _rebuildCollectionGroupsPending = true;
            UIHelper.TryBeginInvoke(() =>
            {
                _rebuildCollectionGroupsPending = false;
                RebuildCollectionGroups();
            });
        }

        private void EnsureCollectionGroupsReady()
        {
            if (!ShouldShowCollectionGroups || !_collectionGroupsDirty)
                return;

            ScheduleRebuildCollectionGroups();
        }

        private static ItemsPanelTemplate CreateProfilesCardsItemsPanel()
        {
            FrameworkElementFactory factory = new(typeof(JustifiedWrapPanel));
            factory.SetValue(JustifiedWrapPanel.HorizontalSpacingProperty, 6.0);
            factory.SetValue(JustifiedWrapPanel.VerticalSpacingProperty, 6.0);
            factory.SetValue(JustifiedWrapPanel.TargetRowHeightProperty, 240.0);
            factory.SetValue(JustifiedWrapPanel.ItemAspectRatioProperty, 565.0 / 900.0);

            return new ItemsPanelTemplate(factory);
        }

        private void RefreshProfilesCardsItemsSource()
        {
            UIHelper.TryBeginInvoke(() =>
            {
                ProfilesCardsItemsSource = null;
                ProfilesCardsItemsSource = ProfilesView;
            });
        }

        private void RebuildCollectionGroups()
        {
            _collectionGroupsDirty = false;
            CollectionGroups.Clear();

            List<ProfileViewModel> displayProfiles = ProfilesView.Cast<ProfileViewModel>().ToList();

            if (ShowGroupedProfilesList)
            {
                foreach (IGrouping<string, ProfileViewModel> platformGroup in displayProfiles
                    .GroupBy(profile => profile.HasPlatform ? profile.PlatformName : "Other")
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    CollectionGroupViewModel group = new(platformGroup.Key, OpenCollection);
                    foreach (ProfileViewModel profile in platformGroup)
                        group.Profiles.Add(profile);
                    CollectionGroups.Add(group);
                }

                foreach (CollectionGroupViewModel group in CollectionGroups)
                    group.SetPreviewProfiles(group.Profiles.Take(CollectionPreviewImageCount));

                return;
            }

            var favGroup = new CollectionGroupViewModel("Favorites", OpenCollection);
            foreach (ProfileViewModel pvm in displayProfiles.Where(p => p.IsLiked))
                favGroup.Profiles.Add(pvm);
            if (favGroup.Profiles.Count > 0)
                CollectionGroups.Add(favGroup);

            IReadOnlyList<GameCollection> userCollections = ManagerFactory.profileManager.GetCollections();
            List<CollectionGroupViewModel> pending = [];
            foreach (GameCollection col in userCollections)
            {
                CollectionGroupViewModel group = new(col, OpenCollection);
                foreach (ProfileViewModel pvm in displayProfiles.Where(p => p.Profile.Collections.Contains(col.Id)))
                    group.Profiles.Add(pvm);
                if (group.Profiles.Count > 0)
                    pending.Add(group);
            }
            foreach (CollectionGroupViewModel group in pending.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                CollectionGroups.Add(group);

            HashSet<Guid> allColIds = userCollections.Select(c => c.Id).ToHashSet();
            CollectionGroupViewModel otherGroup = new("Other", OpenCollection);
            foreach (ProfileViewModel pvm in displayProfiles.Where(p => !p.IsLiked && !p.Profile.Collections.Any(id => allColIds.Contains(id))))
                otherGroup.Profiles.Add(pvm);
            if (otherGroup.Profiles.Count > 0)
                CollectionGroups.Add(otherGroup);

            foreach (CollectionGroupViewModel group in CollectionGroups)
            {
                IEnumerable<ProfileViewModel> previewProfiles = group.Collection is not null
                    ? displayProfiles.Where(profile => profile.Profile.Collections.Contains(group.Collection.Id))
                    : group.Name switch
                    {
                        "Favorites" => displayProfiles.Where(profile => profile.IsLiked),
                        "Other" => displayProfiles.Where(profile => !profile.IsLiked && !profile.Profile.Collections.Any(id => allColIds.Contains(id))),
                        _ => Enumerable.Empty<ProfileViewModel>()
                    };

                group.SetPreviewProfiles(previewProfiles.Take(CollectionPreviewImageCount));
            }
        }
    }
}
