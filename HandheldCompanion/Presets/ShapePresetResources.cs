using System;
using System.Windows;

namespace HandheldCompanion.Presets;

public class ShapePresetResources : ResourceDictionary
{
    public ShapePresetResources()
    {
        WeakEventManager<PresetManager, EventArgs>.AddHandler(
            PresetManager.Current,
            nameof(PresetManager.ShapePresetChanged),
            OnCurrentPresetChanged);

        ApplyCurrentPreset();
    }

    private void OnCurrentPresetChanged(object sender, EventArgs e)
    {
        ApplyCurrentPreset();
    }

    private void ApplyCurrentPreset()
    {
        if (MergedDictionaries.Count > 0) MergedDictionaries.Clear();

        var currentPreset = PresetManager.Current.ShapePreset;
        if (currentPreset != PresetManager.DefaultPreset)
        {
            var assemblyName = GetType().Assembly.GetName().Name;
            var source = new Uri($"/{assemblyName};component/Presets/{currentPreset}.xaml", UriKind.Relative);
            MergedDictionaries.Add(new ResourceDictionary { Source = source });
        }
    }
}