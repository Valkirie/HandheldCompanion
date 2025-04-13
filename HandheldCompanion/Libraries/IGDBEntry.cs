using IGDB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Libraries
{
    public class IGDBEntry : LibraryEntry
    {
        public string Summary;
        public string Storyline;
        public Category Category;

        public Cover Cover;
        public Artwork Artwork;
        public Screenshot Screenshot;

        public IGDBEntry(long id, string name, DateTime releaseDate) : base(LibraryFamily.IGDB, id, name, releaseDate)
        {
        }
    }
}
