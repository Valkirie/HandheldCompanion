using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace HandheldCompanion.Localization;
public class TranslationSource : INotifyPropertyChanged
{
    public static readonly CultureInfo[] ValidCultures = [
        new CultureInfo("en-US"),
        new CultureInfo("fr-FR"),
        new CultureInfo("de-DE"),
        new CultureInfo("it-IT"),
        new CultureInfo("ja-JP"),
        new CultureInfo("pt-BR"),
        new CultureInfo("es-ES"),
        new CultureInfo("zh-Hans"),
        new CultureInfo("zh-Hant"),
        new CultureInfo("ru-RU"),
        new CultureInfo("ko-KR")];

    private static readonly TranslationSource instance = new TranslationSource();

    public static TranslationSource Instance
    {
        get { return instance; }
    }

    private readonly ResourceManager resManager = Properties.Resources.ResourceManager;
    private CultureInfo currentCulture = CultureInfo.CurrentCulture;

    public string this[string key]
    {
        get { return resManager.GetString(key.Replace("resx:Resources.", ""), currentCulture); }
    }

    public CultureInfo CurrentCulture
    {
        get { return currentCulture; }
        set
        {
            if (!currentCulture.Equals(value))
            {
                currentCulture = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
                Thread.CurrentThread.CurrentCulture = value;
                Thread.CurrentThread.CurrentUICulture = value;
                CultureInfo.DefaultThreadCurrentCulture = value;
                CultureInfo.DefaultThreadCurrentUICulture = value;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}