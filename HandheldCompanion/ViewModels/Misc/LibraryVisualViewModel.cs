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
        public string Extension { get; set; }
        public string ExtensionThumbnail { get; set; }

        public BitmapImage Image
        {
            get
            {
                long entryId = LibraryEntry.Id;
                LibraryFamily libraryFamily = LibraryEntry.Family;

                return ManagerFactory.libraryManager.GetGameArt(entryId, LibraryType.thumbnails, Id, ExtensionThumbnail);
            }
        }

        public LibraryVisualViewModel(LibraryEntryViewModel libraryEntry, long id, string extFull, string extThumb = "")
        {
            this.LibraryEntry = libraryEntry;
            this.Id = id;
            this.Extension = extFull;
            if (string.IsNullOrEmpty(extThumb))
                this.ExtensionThumbnail = this.Extension;
            else
                this.ExtensionThumbnail = extThumb;
        }
    }
}
