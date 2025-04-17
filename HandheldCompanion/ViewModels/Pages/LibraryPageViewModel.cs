using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Managers.LibraryManager;

namespace HandheldCompanion.ViewModels
{
    public class LibraryPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProfileViewModel> Profiles { get; set; } = [];

        private Color _highlightColor = Colors.Red;
        public Color HighlightColor
        {
            get => _highlightColor;
            set
            {
                if (_highlightColor != value)
                {
                    _highlightColor = value;
                    OnPropertyChanged(nameof(HighlightColor));
                }
            }
        }

        private BitmapImage _Artwork;
        public BitmapImage Artwork
        {
            get => _Artwork;
            set
            {
                if (_Artwork != value)
                {
                    _Artwork = value;
                    OnPropertyChanged(nameof(Artwork));
                }
            }
        }

        public LibraryPageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Profiles, new object());

            // Load only the ones that should be shown
            foreach (var profile in ManagerFactory.profileManager
                                                  .GetProfiles()
                                                  .Where(p => !p.Default && p.ShowInLibrary))
            {
                Profiles.Add(new ProfileViewModel(profile, false));
            }

            // manage events
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;
        }

        private void ProfileManager_Deleted(Profile profile)
        {
            // ignore me
            if (profile.Default)
                return;

            ProfileViewModel? foundProfile = Profiles.ToList().FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
            if (foundProfile is not null)
            {
                Profiles.SafeRemove(foundProfile);
                foundProfile.Dispose();
            }
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // ignore me
            if (profile.Default)
                return;

            bool shouldShow = profile.ShowInLibrary;
            // find based on guid
            var existingVm = Profiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);

            if (shouldShow)
            {
                if (existingVm is null)
                {
                    // Not yet in list, add
                    Profiles.SafeAdd(new ProfileViewModel(profile, false));
                }
                else
                {
                    // Already in list, only update
                    existingVm.Profile = profile;
                }
            }
            else
            {
                if (existingVm is not null)
                {
                    // Remove from list and dispose
                    Profiles.SafeRemove(existingVm);
                    existingVm.Dispose();
                }
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;

            base.Dispose();
        }
    }
}
