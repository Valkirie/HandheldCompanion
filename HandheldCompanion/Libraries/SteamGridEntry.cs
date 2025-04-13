using craftersmine.SteamGridDBNet;
using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Libraries
{
    [Serializable]
    public class SteamGridEntry : LibraryEntry
    {
        public SteamGridDbGrid Grid;
        public SteamGridDbHero Hero;

        [JsonIgnore] public SteamGridDbGrid[] Grids;
        [JsonIgnore] public SteamGridDbHero[] Heroes;

        public SteamGridEntry(long id, string name, DateTime releaseDate) : base(LibraryFamily.SteamGrid, id, name, releaseDate)
        { }

        public override long GetCoverId()
        {
            if (Grid is not null)
                return Grid.Id;
            return 0;
        }

        public override long GetArtworkId()
        {
            if (Hero is not null)
                return Hero.Id;
            return 0;
        }
    }
}
