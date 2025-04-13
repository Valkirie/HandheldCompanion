using craftersmine.SteamGridDBNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Libraries
{
    public class SteamGridEntry : LibraryEntry
    {
        public SteamGridDbGrid Grid;
        public SteamGridDbHero Hero;

        public SteamGridEntry(long id, string name, DateTime releaseDate) : base(LibraryFamily.SteamGrid, id, name, releaseDate)
        {
        }
    }
}
