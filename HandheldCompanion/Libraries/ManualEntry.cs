using System;
using System.IO;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class ManualEntry : LibraryEntry
    {
        // Fixed magic IDs used to locate manually-placed files in the library cache
        public const long ManualCoverId = -1L;
        public const long ManualArtworkId = -2L;
        public const long ManualLogoId = -3L;

        public ManualEntry(long id, string name) : base(LibraryFamily.Manual, id, name, DateTime.Now)
        { }

        public override long GetCoverId() => string.IsNullOrEmpty(ManualCoverPath) ? 0 : ManualCoverId;
        public override string GetCoverExtension(bool thumbnail)
        {
            if (string.IsNullOrEmpty(ManualCoverPath)) return string.Empty;
            return Path.GetExtension(ManualCoverPath);
        }

        public override long GetArtworkId() => string.IsNullOrEmpty(ManualArtworkPath) ? 0 : ManualArtworkId;
        public override string GetArtworkExtension(bool thumbnail)
        {
            if (string.IsNullOrEmpty(ManualArtworkPath)) return string.Empty;
            return Path.GetExtension(ManualArtworkPath);
        }

        public override long GetLogoId() => string.IsNullOrEmpty(ManualLogoPath) ? 0 : ManualLogoId;
        public override string GetLogoExtension(bool thumbnail)
        {
            if (string.IsNullOrEmpty(ManualLogoPath)) return string.Empty;
            return Path.GetExtension(ManualLogoPath);
        }
    }
}
