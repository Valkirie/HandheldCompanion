using IGDB.Models;
using Newtonsoft.Json;
using System;
using System.Windows.Controls;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class IGDBEntry : LibraryEntry
    {
        public string Summary;
        public string Storyline;
        public Category Category;

        public Cover Cover;
        public Artwork Artwork;
        public Screenshot Screenshot;

        [JsonIgnore] public Artwork[] Artworks;
        [JsonIgnore] public Screenshot[] Screenshots;

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
            else if (Screenshot != null)
                return Screenshot.Id.Value;
            return 0;
        }
    }
}
