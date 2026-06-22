using System;
using HandheldCompanion.Platforms;

namespace HandheldCompanion.Views.Pages.Library;

public static class LibraryNavigationKeys
{
    public const string AllGames = "all-games";
    public const string Favorites = "favorites";
    public const string Collections = "collections";

    public static string Platform(GamePlatform platform) => $"platform:{platform}";
    public static string Collection(global::System.Guid collectionId) => $"collection:{collectionId}";
}
