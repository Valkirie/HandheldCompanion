using System.Threading;

namespace HandheldCompanion.Localization;

using System.ComponentModel;
using System.Globalization;
using System.Resources;

public class TranslationSource : INotifyPropertyChanged
{
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