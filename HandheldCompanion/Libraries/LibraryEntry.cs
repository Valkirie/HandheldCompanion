using System;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class LibraryEntry
    {
        [Flags]
        public enum LibraryFamily
        {
            None = 0,
            IGDB = 1,
            SteamGrid = 2,
        }

        public LibraryFamily Family;
        public long Id;
        public string Name;
        public string Description;
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

        public virtual string GetCoverExtension(bool thumbnail)
        {
            return string.Empty;
        }

        public virtual long GetArtworkId()
        {
            return 0;
        }

        public virtual string GetArtworkExtension(bool thumbnail)
        {
            return string.Empty;
        }
    }
}
