using System;
using System.IO;

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
            Manual = 3,
        }

        public LibraryFamily Family;
        public long Id;
        public string Name;
        public string Description = string.Empty;
        public DateTime ReleaseDate;

        public string ManualCoverPath = string.Empty;
        public string ManualArtworkPath = string.Empty;
        public string ManualLogoPath = string.Empty;

        public LibraryEntry(LibraryFamily libraryFamily, long id, string name, DateTime releaseDate)
        {
            this.Family = libraryFamily;
            this.Id = id;
            this.Name = name;
            this.ReleaseDate = releaseDate;
        }

        public virtual long GetCoverId()
        {
            return string.IsNullOrEmpty(ManualCoverPath) ? 0 : ManualEntry.ManualCoverId;
        }

        public virtual string GetCoverExtension(bool thumbnail)
        {
            return string.IsNullOrEmpty(ManualCoverPath) ? string.Empty : Path.GetExtension(ManualCoverPath);
        }

        public virtual long GetArtworkId()
        {
            return string.IsNullOrEmpty(ManualArtworkPath) ? 0 : ManualEntry.ManualArtworkId;
        }

        public virtual string GetArtworkExtension(bool thumbnail)
        {
            return string.IsNullOrEmpty(ManualArtworkPath) ? string.Empty : Path.GetExtension(ManualArtworkPath);
        }

        public virtual long GetLogoId()
        {
            return string.IsNullOrEmpty(ManualLogoPath) ? 0 : ManualEntry.ManualLogoId;
        }

        public virtual string GetLogoExtension(bool thumbnail)
        {
            return string.IsNullOrEmpty(ManualLogoPath) ? string.Empty : Path.GetExtension(ManualLogoPath);
        }
    }
}
