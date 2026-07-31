using IGDB.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class IGDBEntry : LibraryEntry
    {
        public string Storyline = string.Empty;

        public Cover? Cover;
        public Artwork? Artwork;

        [JsonIgnore] public List<Artwork> Artworks = new();

        public IGDBEntry(long id, string name, DateTime releaseDate) : base(LibraryFamily.IGDB, id, name, releaseDate)
        { }

        public override long GetCoverId()
        {
            if (!string.IsNullOrEmpty(ManualCoverPath))
                return base.GetCoverId();
            if (Cover?.Id is long coverId)
                return coverId;
            return 0;
        }

        public override string GetCoverExtension(bool thumbnail)
        {
            if (!string.IsNullOrEmpty(ManualCoverPath))
                return base.GetCoverExtension(thumbnail);
            if (Cover is not null)
                return Path.GetExtension(Cover.Url);

            return base.GetCoverExtension(thumbnail);
        }

        public override long GetArtworkId()
        {
            if (!string.IsNullOrEmpty(ManualArtworkPath))
                return base.GetArtworkId();
            if (Artwork?.Id is long artworkId)
                return artworkId;
            return 0;
        }

        public override string GetArtworkExtension(bool thumbnail)
        {
            if (!string.IsNullOrEmpty(ManualArtworkPath))
                return base.GetArtworkExtension(thumbnail);
            if (Artwork is not null)
                return Path.GetExtension(Artwork.Url);

            return base.GetArtworkExtension(thumbnail);
        }
    }
}
