
using System.Windows.Data;

namespace HandheldCompanion.Localization;
public class StaticExtension : Binding
{
    public StaticExtension(string name) : base("[" + name + "]")
    {
        Mode = BindingMode.Default;
        Source = TranslationSource.Instance;
    }
}

