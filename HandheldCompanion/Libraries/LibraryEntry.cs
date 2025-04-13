using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class LibraryEntry
    {
        public enum LibraryFamily
        {
            IGDB,
            SteamGrid
        }

        public LibraryFamily Family;
        public long Id;
        public string Name;
        public DateTime ReleaseDate;

        public LibraryEntry(LibraryFamily libraryFamily, long id, string name, DateTime releaseDate)
        {
            this.Family = libraryFamily;
            this.Id = id;
            this.Name = name;
            this.ReleaseDate = releaseDate;
        }

        public virtual long GetCoverId()
        {
            return 0;
        }

        public virtual long GetArtworkId()
        {
            return 0;
        }
    }
}
