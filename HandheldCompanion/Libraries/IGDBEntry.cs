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
        public string Storyline;

        public Cover? Cover;
        public Artwork? Artwork;

        [JsonIgnore] public List<Artwork> Artworks = new();

        public IGDBEntry(long id, string name, DateTime releaseDate) : base(LibraryFamily.IGDB, id, name, releaseDate)
        { }

        public override long GetCoverId()
        {
            if (Cover != null)
                return Cover.Id.Value;
            return 0;
        }

        public override string GetCoverExtension(bool thumbnail)
        {
            if (Cover is not null)
                return Path.GetExtension(Cover.Url);

            return base.GetCoverExtension(thumbnail);
        }

        public override long GetArtworkId()
        {
            if (Artwork != null)
                return Artwork.Id.Value;
            return 0;
        }

        public override string GetArtworkExtension(bool thumbnail)
        {
            if (Artwork is not null)
                return Path.GetExtension(Artwork.Url);

            return base.GetArtworkExtension(thumbnail);
        }
    }
}
