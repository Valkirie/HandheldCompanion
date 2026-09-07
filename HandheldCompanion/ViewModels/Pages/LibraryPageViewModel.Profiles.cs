using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace HandheldCompanion.ViewModels
{
    public partial class LibraryPageViewModel
    {
        private void LibraryManager_Initialized()
        {
            QueryLibrary();
        }

        private void LibraryManager_ProfileStatusChanged(Profile profile, ManagerStatus status)
        {
            ProfileViewModel? profileViewModel = Profiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);

            profileViewModel?.IsBusy = status.HasFlag(ManagerStatus.Busy);
        }

        private void QueryProfile()
        {
            // manage events
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;
            ManagerFactory.profileManager.CollectionAdded += ProfileCollectionHelper_CollectionAdded;
            ManagerFactory.profileManager.CollectionRemoved += ProfileCollectionHelper_CollectionRemoved;
            ManagerFactory.profileManager.CollectionUpdated += ProfileCollectionHelper_CollectionUpdated;

            // Bind the repeater to the sorted view BEFORE any profiles arrive so cards can render incrementally rather than all at once after the bulk load completes
            _uiContext.Post(_ => UpdateSorting(), null);

            foreach (Profile profile in ManagerFactory.profileManager.GetProfiles())
            {
                ProfileManager_Updated(profile, UpdateSource.Background, false);

                foreach (Profile subProfile in ManagerFactory.profileManager.GetSubProfilesFromProfile(profile))
                    ProfileManager_Updated(subProfile, UpdateSource.Background, false);
            }

            // Yield to message pump to process Loaded and visibility-tracking events
            Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

            // Hide the spinner once every card has been dispatched to the UI
            IsInitializing = false;

            RebuildNavigationItems();
            ScheduleRebuildCollectionGroups();
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void UpdateSorting()
        {
            ListSortDirection direction = SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            ProfilesView.SortDescriptions.Clear();
            ProfilesView.LiveSortingProperties.Clear();

            // Always sort favorites first (descending IsLiked = favorites on top)
            ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.IsLiked), ListSortDirection.Descending));
            ProfilesView.LiveSortingProperties.Add(nameof(ProfileViewModel.IsLiked));

            // Then apply secondary sort based on user selection
            SortDescription secondary;
            string secondaryProperty;
            switch (SortTarget)
            {
                default:
                case 0:
                    secondary = new SortDescription(nameof(ProfileViewModel.Name), direction);
                    secondaryProperty = nameof(ProfileViewModel.Name);
                    break;
                case 1:
                    secondary = new SortDescription(nameof(ProfileViewModel.PlatformType), direction);
                    secondaryProperty = nameof(ProfileViewModel.PlatformType);
                    break;
                case 2:
                    secondary = new SortDescription(nameof(ProfileViewModel.DateCreated), direction);
                    secondaryProperty = nameof(ProfileViewModel.DateCreated);
                    break;
                case 3:
                    secondary = new SortDescription(nameof(ProfileViewModel.LastUsed), direction);
                    secondaryProperty = nameof(ProfileViewModel.LastUsed);
                    break;
            }
            ProfilesView.SortDescriptions.Add(secondary);
            ProfilesView.LiveSortingProperties.Add(secondaryProperty);

            // Workaround for iNKORE ItemsRepeater not observing ICollectionView changes
            RefreshProfilesCardsItemsSource();

            ScheduleRebuildCollectionGroups();

            OnPropertyChanged(nameof(HasLiked));
        }

        private void ProfileManager_Deleted(Profile profile)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                // ignore me
                if (profile.Default)
                    return;

                if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                    return;

                try
                {
                    ProfileViewModel? foundProfile = Profiles.FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
                    if (foundProfile is not null)
                    {
                        Profiles.Remove(foundProfile);

                        // Remove from recent games.
                        RecentGames.Remove(foundProfile);
                        OnPropertyChanged(nameof(HasRecentGames));

                        // Remove from collection groups.
                        foreach (CollectionGroupViewModel group in CollectionGroups.ToList())
                        {
                            group.Profiles.Remove(foundProfile);
                            if (group.Profiles.Count == 0)
                                CollectionGroups.Remove(group);
                        }

                        foundProfile.Dispose();
                        OnPropertyChanged(nameof(GameCount));
                    }
                }
                finally
                {
                    Monitor.Exit(_collectionLock);
                }

                UpdateNavigationForProfile(profile);
            });
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            UIHelper.TryBeginInvoke(() => UpdateProfile(profile));
        }

        private void UpdateProfile(Profile profile)
        {
            if (profile.Default)
                return;

            bool shouldShow = profile.ShowInLibrary;

            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            ProfileViewModel? updatedViewModel;
            try
            {
                updatedViewModel = UpdateProfiles(profile, shouldShow);
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }

            if (shouldShow && updatedViewModel is not null)
            {
                UpdateRecentGames(updatedViewModel);
                UpdateCollectionGroups(updatedViewModel);
            }

            UpdateNavigationForProfile(profile);
            if (!IsInitializing)
                RefreshProfilesCardsItemsSource();
        }

        private ProfileViewModel? UpdateProfiles(Profile profile, bool shouldShow)
        {
            ProfileViewModel? existingViewModel = Profiles.FirstOrDefault(candidate => candidate.Profile.Guid == profile.Guid);

            if (!shouldShow)
            {
                if (existingViewModel is not null)
                    RemoveProfile(existingViewModel);

                return null;
            }

            if (existingViewModel is not null)
            {
                existingViewModel.Profile = profile;
                return existingViewModel;
            }

            ProfileViewModel addedViewModel = new(profile, false, true);
            Profiles.Add(addedViewModel);
            OnPropertyChanged(nameof(GameCount));
            return addedViewModel;
        }

        private void RemoveProfile(ProfileViewModel profileViewModel)
        {
            Profiles.Remove(profileViewModel);
            RecentGames.Remove(profileViewModel);
            RemoveFromCollectionGroups(profileViewModel);
            profileViewModel.Dispose();

            OnPropertyChanged(nameof(HasRecentGames));
            OnPropertyChanged(nameof(GameCount));
        }

        private void UpdateRecentGames(ProfileViewModel profileViewModel)
        {
            RecentGames.Remove(profileViewModel);
            if (profileViewModel.LastUsed > DateTime.MinValue)
            {
                int index = 0;
                while (index < RecentGames.Count && RecentGames[index].LastUsed >= profileViewModel.LastUsed)
                    index++;

                if (index < RecentGamesCount)
                    RecentGames.Insert(index, profileViewModel);
            }

            while (RecentGames.Count > RecentGamesCount)
                RecentGames.RemoveAt(RecentGames.Count - 1);

            OnPropertyChanged(nameof(HasRecentGames));
        }

        private void UpdateCollectionGroups(ProfileViewModel profileViewModel)
        {
            RemoveFromCollectionGroups(profileViewModel);

            if (!ShouldShowCollectionGroups)
                return;

            if (ShowGroupedProfilesList)
            {
                AddToPlatformGroup(profileViewModel);
                return;
            }

            AddToCustomCollectionGroups(profileViewModel);
        }

        private void RemoveFromCollectionGroups(ProfileViewModel profileViewModel)
        {
            foreach (CollectionGroupViewModel group in CollectionGroups.ToList())
            {
                group.Profiles.Remove(profileViewModel);
                if (group.Profiles.Count == 0)
                    CollectionGroups.Remove(group);
            }
        }

        private void AddToPlatformGroup(ProfileViewModel profileViewModel)
        {
            string groupName = profileViewModel.HasPlatform ? profileViewModel.PlatformName : "Other";
            CollectionGroupViewModel? group = CollectionGroups.FirstOrDefault(candidate => candidate.Name == groupName);
            if (group is null)
            {
                group = new CollectionGroupViewModel(groupName, OpenCollection);
                CollectionGroups.Add(group);
            }

            group.Profiles.Add(profileViewModel);
            group.SetPreviewProfiles(group.Profiles.Take(CollectionPreviewImageCount));
        }

        private void AddToCustomCollectionGroups(ProfileViewModel profileViewModel)
        {
            CollectionGroupViewModel? favoritesGroup = CollectionGroups.FirstOrDefault(candidate => candidate.Name == "Favorites");
            if (profileViewModel.IsLiked && favoritesGroup is not null)
                favoritesGroup.Profiles.Add(profileViewModel);

            IReadOnlyList<GameCollection> collections = ManagerFactory.profileManager.GetCollections();
            foreach (GameCollection collection in collections.Where(collection => profileViewModel.Profile.Collections.Contains(collection.Id)))
            {
                CollectionGroupViewModel? collectionGroup = CollectionGroups.FirstOrDefault(candidate => candidate.Collection?.Id == collection.Id);
                collectionGroup?.Profiles.Add(profileViewModel);
            }

            if (!profileViewModel.IsLiked && !profileViewModel.Profile.Collections.Any(id => collections.Any(collection => collection.Id == id)))
            {
                CollectionGroupViewModel? otherGroup = CollectionGroups.FirstOrDefault(candidate => candidate.Name == "Other");
                otherGroup?.Profiles.Add(profileViewModel);
            }
        }

        private void UpdateNavigationForProfile(Profile profile)
        {
            foreach (LibraryNavigationItemViewModel item in NavigationItems)
            {
                if (item.Key == FavoritesNavigationKey)
                {
                    item.IsVisible = HasLiked;
                    item.GameCount = Profiles.Count(candidate => candidate.IsLiked);
                }
                else if (item.Kind == LibraryNavigationItemKind.Platform && item.Platform == profile.PlatformType)
                {
                    item.IsVisible = Profiles.Any(candidate => candidate.PlatformType == item.Platform);
                    item.GameCount = Profiles.Count(candidate => candidate.PlatformType == item.Platform);
                }
                else if (item.Kind == LibraryNavigationItemKind.CollectionsRoot)
                {
                    item.GameCount = Profiles.Count(candidate => candidate.Profile.Collections.Count > 0);
                }
            }

            foreach (KeyValuePair<string, LibraryNavigationItemViewModel> pair in collectionNavigationItems)
            {
                if (profile.Collections.Any(id => pair.Value.CollectionId == id))
                    pair.Value.IsVisible = HasProfilesForCollection(pair.Value.CollectionId!.Value);
            }

            OnPropertyChanged(nameof(AvailablePlatforms));
            OnPropertyChanged(nameof(HasLiked));
        }

        public void MarkLibraryChecked()
        {
            ManagerFactory.settingsManager.SetProperty(
                "LibraryLastChecked",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            OnPropertyChanged(nameof(LastLibraryCheckText));
        }

        private static string GetLastLibraryCheckText()
        {
            string value = ManagerFactory.settingsManager.GetString("LibraryLastChecked");
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp))
                return Properties.Resources.SettingsPage_LastChecked;

            return Properties.Resources.SettingsPage_LastChecked +
                   CommonUtils.GetTime(DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime);
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // manage events
                ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
                ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;
                ManagerFactory.libraryManager.ProfileStatusChanged -= LibraryManager_ProfileStatusChanged;
                ManagerFactory.libraryManager.NetworkAvailabilityChanged -= LibraryManager_NetworkAvailabilityChanged;
                ManagerFactory.profileManager.CollectionAdded -= ProfileCollectionHelper_CollectionAdded;
                ManagerFactory.profileManager.CollectionRemoved -= ProfileCollectionHelper_CollectionRemoved;
                ManagerFactory.profileManager.CollectionUpdated -= ProfileCollectionHelper_CollectionUpdated;
            }

            base.Dispose(disposing);
        }


        private void UpdateFiltering()
        {
            UIHelper.TryBeginInvoke(() =>
            {
                ProfilesView.Filter = o => o is ProfileViewModel vm && MatchesFilters(vm);

                // Workaround for iNKORE ItemsRepeater not observing ICollectionView changes
                RefreshProfilesCardsItemsSource();

                ScheduleRebuildCollectionGroups();
            });
        }

        private bool MatchesFilters(ProfileViewModel profile)
        {
            return MatchesSearchFilter(profile) && MatchesNavigationFilter(profile);
        }

        private bool MatchesSearchFilter(ProfileViewModel profile)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return profile.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   profile.Profile.Executable.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesNavigationFilter(ProfileViewModel profile)
        {
            return SelectedNavigationItem?.Kind switch
            {
                null => true,
                LibraryNavigationItemKind.AllGames => true,
                LibraryNavigationItemKind.CollectionsRoot => true,
                _ when string.Equals(SelectedNavigationItem.Key, FavoritesNavigationKey, StringComparison.Ordinal) => profile.IsLiked,
                LibraryNavigationItemKind.Platform => profile.PlatformType == SelectedNavigationItem.Platform,
                LibraryNavigationItemKind.Collection => SelectedNavigationItem.CollectionId.HasValue &&
                                                        profile.Profile.Collections.Contains(SelectedNavigationItem.CollectionId.Value),
                _ => true
            };
        }
    }
}
