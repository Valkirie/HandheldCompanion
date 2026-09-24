using HandheldCompanion.Shared;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HandheldCompanion.Platforms.Discovery;

public static class EmulatorDefinitions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ConfigsDirectory { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Emulators");

    public static GamePlatform AllPlatforms => All.Aggregate(GamePlatform.Generic, (platform, definition) => platform | definition.PlatformType);

    public static EmulatorDefinition[] All = Array.Empty<EmulatorDefinition>();
    public static event Action<EmulatorDefinition>? DefinitionAdded;
    public static event Action<EmulatorDefinition>? DefinitionRemoved;
    public static event Action<EmulatorDefinition>? DefinitionUpdated;

    static EmulatorDefinitions()
    {
        Reload();
    }

    public static void Reload()
    {
        EmulatorDefinition[] previous = All;
        All = LoadDefinitions();

        foreach (EmulatorDefinition definition in previous)
        {
            if (!All.Any(current => current.Id == definition.Id))
                DefinitionRemoved?.Invoke(definition);
        }

        foreach (EmulatorDefinition definition in All)
        {
            EmulatorDefinition? previousDefinition = previous.FirstOrDefault(current => current.Id == definition.Id);
            if (previousDefinition is null)
                DefinitionAdded?.Invoke(definition);
            else
                DefinitionUpdated?.Invoke(definition);
        }
    }

    private static EmulatorDefinition[] LoadDefinitions()
    {
        if (!Directory.Exists(ConfigsDirectory))
            throw new DirectoryNotFoundException($"Emulator definition directory was not found: {ConfigsDirectory}");

        EmulatorDefinition[] definitions = Directory
            .EnumerateFiles(ConfigsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(LoadDefinition)
            .Where(definition => definition is not null)
            .Select(definition => definition!)
            .ToArray();

        if (definitions.Length == 0)
            throw new InvalidOperationException($"No emulator definitions were found in: {ConfigsDirectory}");

        return definitions;
    }

    private static EmulatorDefinition? LoadDefinition(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            EmulatorDefinition? definition = JsonSerializer.Deserialize<EmulatorDefinition>(json, SerializerOptions);
            if (definition is null || string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Name))
                throw new JsonException("The definition must contain non-empty Id and Name values.");

            return definition;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Failed to load emulator definition '{0}': {1}", filePath, ex.Message);
            return null;
        }
    }
}
