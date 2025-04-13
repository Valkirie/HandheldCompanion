using HandheldCompanion.Managers;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;
using static HandheldCompanion.Managers.LibraryManager;

namespace HandheldCompanion.ViewModels.Misc
{
    public class LibraryVisualViewModel
    {
        private LibraryEntryViewModel LibraryEntry { get; set; }
        public long Id { get; set; }

        public BitmapImage Image
        {
            get
            {
                long entryId = LibraryEntry.Id;
                LibraryFamily libraryFamily = LibraryEntry.Family;

                return ManagerFactory.libraryManager.GetGameArt(entryId, LibraryType.thumbnails, Id);
            }
        }

        public LibraryVisualViewModel(LibraryEntryViewModel libraryEntry, long id)
        {
            this.LibraryEntry = libraryEntry;
            this.Id = id;
        }
    }
}
