using craftersmine.SteamGridDBNet;
using Newtonsoft.Json;
using System;
using System.IO;

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

        public override string GetCoverExtension(bool thumbnail)
        {
            if (Grid is not null)
                return Path.GetExtension(thumbnail ? Grid.ThumbnailImageUrl : Grid.FullImageUrl);

            return base.GetCoverExtension(thumbnail);
        }

        public override long GetArtworkId()
        {
            if (Hero is not null)
                return Hero.Id;
            return 0;
        }

        public override string GetArtworkExtension(bool thumbnail)
        {
            if (Hero is not null)
                return Path.GetExtension(thumbnail ? Hero.ThumbnailImageUrl : Hero.FullImageUrl);

            return base.GetArtworkExtension(thumbnail);
        }
    }
}
