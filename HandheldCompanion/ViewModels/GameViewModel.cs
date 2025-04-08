using IGDB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.ViewModels
{
    public class GameViewModel : BaseViewModel
    {
        private Game _Game;
        public Game Game
        {
            get => _Game;
            set
            {
                _Game = value;

                // refresh all properties
                OnPropertyChanged(string.Empty);
            }
        }

        public string Name => _Game.Name ?? string.Empty;
        public string Summary => _Game.Summary ?? string.Empty;
        public string Storyline => _Game.Storyline ?? string.Empty;
        public long Id => _Game.Id ?? 0;

        public bool HasCover => _Game.Cover.Value is not null;
        public bool HasArtworks => _Game.Artworks is not null && _Game.Artworks.Values.Count() != 0;
        public bool HasScreenshots => _Game.Screenshots is not null && _Game.Screenshots.Values.Count() != 0;

        public GameViewModel(Game game)
        {
            Game = game;
        }

        public override string ToString()
        {
            return Name;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
