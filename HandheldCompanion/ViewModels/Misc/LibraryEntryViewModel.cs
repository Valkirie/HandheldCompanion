using craftersmine.SteamGridDBNet;
using HandheldCompanion.Libraries;
using HandheldCompanion.ViewModels.Misc;
using IGDB.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using static HandheldCompanion.Libraries.LibraryEntry;

namespace HandheldCompanion.ViewModels
{
    public class LibraryEntryViewModel : BaseViewModel
    {
        public ObservableCollection<LibraryVisualViewModel> LibraryCovers { get; } = [];
        public ObservableCollection<LibraryVisualViewModel> LibraryArtworks { get; } = [];

        private LibraryEntry _LibEntry;
        public LibraryEntry LibEntry
        {
            get => _LibEntry;
            set
            {
                _LibEntry = value;

                // refresh all properties
                OnPropertyChanged(string.Empty);
            }
        }

        public long Id => LibEntry.Id;
        public string Name => LibEntry.Name;
        public string Description => LibEntry.Description;
        public int ReleaseDateYear => LibEntry.ReleaseDate.Year;
        public LibraryFamily Family => LibEntry.Family;

        public LibraryEntryViewModel(LibraryEntry libraryEntry)
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(LibraryCovers, new object());
            BindingOperations.EnableCollectionSynchronization(LibraryArtworks, new object());

            LibEntry = libraryEntry;

            if (LibEntry is SteamGridEntry steamEntry)
            {
                foreach (SteamGridDbGrid grid in steamEntry.Grids)
                    LibraryCovers.Add(new(this, grid.Id, Path.GetExtension(grid.FullImageUrl), Path.GetExtension(grid.ThumbnailImageUrl)));
                foreach (SteamGridDbHero hero in steamEntry.Heroes)
                    LibraryArtworks.Add(new(this, hero.Id, Path.GetExtension(hero.FullImageUrl), Path.GetExtension(hero.ThumbnailImageUrl)));
            }
            else if (LibEntry is IGDBEntry IGDB)
            {
                if (IGDB.Cover is not null)
                    LibraryCovers.Add(new(this, IGDB.Cover.Id.HasValue ? IGDB.Cover.Id.Value : 0, Path.GetExtension(IGDB.Cover.Url)));

                foreach (Artwork artwork in IGDB.Artworks)
                    LibraryArtworks.Add(new(this, artwork.Id.Value, Path.GetExtension(artwork.Url)));
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
