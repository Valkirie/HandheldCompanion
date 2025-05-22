using System.Globalization;
using System.Resources;

namespace HandheldCompanion.Localization
{
    public class ResilientResourceManager : ResourceManager
    {
        private static readonly CultureInfo fallbackCulture = new CultureInfo("en-US");

        public ResilientResourceManager(string baseName, System.Reflection.Assembly assembly) : base(baseName, assembly)
        {
        }

        public override string GetString(string name, CultureInfo? culture)
        {
            string? result = base.GetString(name, culture);
            if (string.IsNullOrEmpty(result))
                result = base.GetString(name, fallbackCulture);
            return result ?? name;
        }

        public override string GetString(string name)
        {
            return GetString(name, CultureInfo.CurrentUICulture);
        }
    }
}
