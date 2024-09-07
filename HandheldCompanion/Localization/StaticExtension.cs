namespace HandheldCompanion.Localization;

using System.Windows.Data;

public class StaticExtension : Binding
{
    public StaticExtension(string name) : base("[" + name + "]")
    {
        Mode = BindingMode.Default;
        Source = TranslationSource.Instance;
    }
}

