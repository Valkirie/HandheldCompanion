using craftersmine.SteamGridDBNet;
using HandheldCompanion.Libraries;
using HandheldCompanion.Managers;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views.Pages;
using IGDB.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
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
                foreach(SteamGridDbGrid grid in steamEntry.Grids)
                    LibraryCovers.Add(new(this, grid.Id));
                foreach(SteamGridDbHero hero in steamEntry.Heroes)
                    LibraryArtworks.Add(new(this, hero.Id));
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
