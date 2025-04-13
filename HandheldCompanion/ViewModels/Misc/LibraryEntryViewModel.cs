using HandheldCompanion.Libraries;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages;
using IGDB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;

namespace HandheldCompanion.ViewModels
{
    public class LibraryEntryViewModel : BaseViewModel
    {
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
            LibEntry = libraryEntry;
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
