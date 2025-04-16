using IGDB.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class IGDBEntry : LibraryEntry
    {
        public string Storyline;
        public Category Category;

        public Cover Cover;
        public Artwork Artwork;

        [JsonIgnore] public List<Artwork> Artworks = new();

        public IGDBEntry(long id, string name, DateTime releaseDate) : base(LibraryFamily.IGDB, id, name, releaseDate)
        { }

        public override long GetCoverId()
        {
            if (Cover != null)
                return Cover.Id.Value;
            return 0;
        }

        public override long GetArtworkId()
        {
            if (Artwork != null)
                return Artwork.Id.Value;
            return 0;
        }
    }
}
