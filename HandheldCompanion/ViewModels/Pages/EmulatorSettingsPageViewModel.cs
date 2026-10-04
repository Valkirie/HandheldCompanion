using HandheldCompanion.Platforms;
using HandheldCompanion.Platforms.Discovery;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HandheldCompanion.ViewModels;

public sealed class EmulatorSettingsFileViewModel : BaseViewModel
{
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string Name { get; }

    public EmulatorSettingsFileViewModel(string filePath, string name)
    {
        FilePath = filePath;
        Name = name;
    }
    public override string ToString() => Name;
}
public sealed class StringListViewModel : BaseViewModel
{
    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public StringListViewModel() { }
    public StringListViewModel(IEnumerable<string> values) => Text = string.Join(Environment.NewLine, values);
    public string[] Values => Text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed class ConfigLocationViewModel : BaseViewModel
{
    private ConfigRoot _root;
    private string _relativePath = string.Empty;
    public ConfigRoot Root { get => _root; set => SetProperty(ref _root, value); }
    public string RelativePath { get => _relativePath; set => SetProperty(ref _relativePath, value); }
    public StringListViewModel Files { get; } = new();
    public StringListViewModel ContentKeys { get; } = new();

    public ConfigLocation ToModel() => new() { Root = Root, RelativePath = RelativePath, Files = Files.Values, ContentKeys = ContentKeys.Values };
    public static ConfigLocationViewModel FromModel(ConfigLocation model) => new() { Root = model.Root, RelativePath = model.RelativePath, Files = { Text = string.Join(Environment.NewLine, model.Files) }, ContentKeys = { Text = string.Join(Environment.NewLine, model.ContentKeys) } };
}

public sealed class PortableLocationViewModel : BaseViewModel
{
    private string _marker = string.Empty;
    private string _configPath = string.Empty;
    public string Marker { get => _marker; set => SetProperty(ref _marker, value); }
    public string ConfigPath { get => _configPath; set => SetProperty(ref _configPath, value); }
    public StringListViewModel Files { get; } = new();
    public PortableLocation ToModel() => new() { Marker = Marker, ConfigPath = ConfigPath, Files = Files.Values };
    public static PortableLocationViewModel FromModel(PortableLocation model) => new() { Marker = model.Marker, ConfigPath = model.ConfigPath, Files = { Text = string.Join(Environment.NewLine, model.Files) } };
}

public sealed class RomMetadataViewModel : BaseViewModel
{
    private string _relativePath = string.Empty;
    private string _elementName = string.Empty;
    private bool _searchParentDirectories;
    public string RelativePath { get => _relativePath; set => SetProperty(ref _relativePath, value); }
    public string ElementName { get => _elementName; set => SetProperty(ref _elementName, value); }
    public StringListViewModel RelativePaths { get; } = new();
    public bool SearchParentDirectories { get => _searchParentDirectories; set => SetProperty(ref _searchParentDirectories, value); }
    public RomMetadataRule ToModel() => new() { RelativePath = RelativePath, ElementName = ElementName, RelativePaths = RelativePaths.Values, SearchParentDirectories = SearchParentDirectories };
    public static RomMetadataViewModel FromModel(RomMetadataRule model) => new() { RelativePath = model.RelativePath, ElementName = model.ElementName, RelativePaths = { Text = string.Join(Environment.NewLine, model.RelativePaths) }, SearchParentDirectories = model.SearchParentDirectories };
}

public sealed class EmulatorDefinitionViewModel : BaseViewModel
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private GamePlatform _platformType;
    private string _platformColor = "#666666";
    private string _platformGlyph = "\uF712";
    private string _platformFont = "Simple Icons Fit";
    private double _platformFontSize = 22;
    private string _argumentPrefix = string.Empty;
    private LaunchArgumentMode _launchArgumentMode;
    private string _argumentTemplate = "{rom}";
    private bool _directoryRoms;

    public string Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public GamePlatform PlatformType { get => _platformType; set => SetProperty(ref _platformType, value); }
    public string PlatformColor { get => _platformColor; set => SetProperty(ref _platformColor, value); }
    public string PlatformGlyph
    {
        get => _platformGlyph;
        set
        {
            if (SetProperty(ref _platformGlyph, value))
                OnPropertyChanged(nameof(PlatformGlyphText));
        }
    }
    public string PlatformGlyphText
    {
        get
        {
            if (_platformGlyph.Length == 1 && char.IsControl(_platformGlyph[0]) == false && _platformGlyph[0] >= '\uE000')
                return $"\\u{(int)_platformGlyph[0]:X4}";

            return _platformGlyph;
        }
        set
        {
            if (value.StartsWith("\\u", StringComparison.OrdinalIgnoreCase) && value.Length == 6 &&
                int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
            {
                PlatformGlyph = char.ConvertFromUtf32(codePoint);
            }
            else
            {
                PlatformGlyph = value;
            }
        }
    }
    public string PlatformFont { get => _platformFont; set => SetProperty(ref _platformFont, value); }
    public double PlatformFontSize { get => _platformFontSize; set => SetProperty(ref _platformFontSize, value); }
    public StringListViewModel Executables { get; } = new();
    public StringListViewModel ProductNames { get; } = new();
    public ObservableCollection<ConfigLocationViewModel> Configurations { get; } = [];
    public ObservableCollection<PortableLocationViewModel> PortableLocations { get; } = [];
    public StringListViewModel RomExtensions { get; } = new();
    public string ArgumentPrefix { get => _argumentPrefix; set => SetProperty(ref _argumentPrefix, value); }
    public LaunchArgumentMode LaunchArgumentMode { get => _launchArgumentMode; set => SetProperty(ref _launchArgumentMode, value); }
    public string ArgumentTemplate { get => _argumentTemplate; set => SetProperty(ref _argumentTemplate, value); }
    public bool DirectoryRoms { get => _directoryRoms; set => SetProperty(ref _directoryRoms, value); }
    public ObservableCollection<RomMetadataViewModel> RomMetadata { get; } = [];

    public EmulatorDefinition ToModel() => new()
    {
        Id = Id.Trim(),
        Name = Name.Trim(),
        PlatformType = PlatformType,
        PlatformColor = PlatformColor.Trim(),
        PlatformGlyph = PlatformGlyph,
        PlatformFont = PlatformFont,
        PlatformFontSize = PlatformFontSize,
        Executables = Executables.Values,
        ProductNames = ProductNames.Values,
        Configurations = Configurations.Select(x => x.ToModel()).ToArray(),
        PortableLocations = PortableLocations.Select(x => x.ToModel()).ToArray(),
        RomExtensions = RomExtensions.Values,
        ArgumentPrefix = ArgumentPrefix,
        LaunchArgumentMode = LaunchArgumentMode,
        ArgumentTemplate = ArgumentTemplate,
        RomMetadata = RomMetadata.FirstOrDefault()?.ToModel(),
        DirectoryRoms = DirectoryRoms
    };

    public static EmulatorDefinitionViewModel FromModel(EmulatorDefinition model)
    {
        var result = new EmulatorDefinitionViewModel { Id = model.Id, Name = model.Name, PlatformType = model.PlatformType, PlatformColor = model.PlatformColor, PlatformGlyph = model.PlatformGlyph, PlatformFont = model.PlatformFont, PlatformFontSize = model.PlatformFontSize, ArgumentPrefix = model.ArgumentPrefix, LaunchArgumentMode = model.LaunchArgumentMode, ArgumentTemplate = model.ArgumentTemplate, DirectoryRoms = model.DirectoryRoms };
        result.Executables.Text = string.Join(Environment.NewLine, model.Executables); result.ProductNames.Text = string.Join(Environment.NewLine, model.ProductNames); result.RomExtensions.Text = string.Join(Environment.NewLine, model.RomExtensions);
        foreach (ConfigLocation item in model.Configurations) result.Configurations.Add(ConfigLocationViewModel.FromModel(item));
        foreach (PortableLocation item in model.PortableLocations) result.PortableLocations.Add(PortableLocationViewModel.FromModel(item));
        if (model.RomMetadata is not null)
            result.RomMetadata.Add(RomMetadataViewModel.FromModel(model.RomMetadata));
        return result;
    }
}
/// <summary>
/// Provides the data and commands used to manage emulator definition files.
/// </summary>
/// <remarks>
/// The selected definition is exposed as editable view-model data. Changes to
/// the definition and its nested settings are persisted immediately, so the
/// page does not require a separate save operation.
/// </remarks>
public sealed class EmulatorSettingsPageViewModel : BaseViewModel
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private readonly string _directory = EmulatorDefinitions.ConfigsDirectory;
    private EmulatorSettingsFileViewModel? _selectedFile;
    private EmulatorDefinitionViewModel? _definition;
    private string _statusText = string.Empty;

    public ObservableCollection<EmulatorSettingsFileViewModel> Files { get; } = [];
    public Array PlatformTypes => Enum.GetValues<GamePlatform>();
    public Array ConfigRoots => Enum.GetValues<ConfigRoot>();
    public Array LaunchArgumentModes => Enum.GetValues<LaunchArgumentMode>();
    public EmulatorDefinitionViewModel? Definition { get => _definition; private set => SetProperty(ref _definition, value); }
    public EmulatorSettingsFileViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (!SetProperty(ref _selectedFile, value))
                return;

            OnPropertyChanged(nameof(IsFileManagementEnabled));
            if (value is not null)
                LoadDefinition(value.FilePath);
        }
    }
    public bool IsFileManagementEnabled => SelectedFile is not null;
    public DelegateCommand NewCommand { get; }
    public DelegateCommand DeleteCommand { get; }
    public DelegateCommand SaveCommand { get; }
    public DelegateCommand AddConfigurationCommand { get; }
    public DelegateCommand<ConfigLocationViewModel> RemoveConfigurationCommand { get; }
    public DelegateCommand AddPortableLocationCommand { get; }
    public DelegateCommand<PortableLocationViewModel> RemovePortableLocationCommand { get; }
    public DelegateCommand AddRomMetadataCommand { get; }
    public DelegateCommand<RomMetadataViewModel> RemoveRomMetadataCommand { get; }

    /// <summary>
    /// Initializes the emulator settings view model and loads the available definitions.
    /// </summary>
    public EmulatorSettingsPageViewModel()
    {
        NewCommand = new DelegateCommand(NewFile);
        DeleteCommand = new DelegateCommand(DeleteFile);
        SaveCommand = new DelegateCommand(SaveFile);

        AddConfigurationCommand = new DelegateCommand(() => Definition?.Configurations.Add(new ConfigLocationViewModel()));
        RemoveConfigurationCommand = new DelegateCommand<ConfigLocationViewModel>(item => Definition?.Configurations.Remove(item));
        AddPortableLocationCommand = new DelegateCommand(() => Definition?.PortableLocations.Add(new PortableLocationViewModel()));
        RemovePortableLocationCommand = new DelegateCommand<PortableLocationViewModel>(item => Definition?.PortableLocations.Remove(item));
        AddRomMetadataCommand = new DelegateCommand(() => { if (Definition is not null && Definition.RomMetadata.Count == 0) Definition.RomMetadata.Add(new RomMetadataViewModel()); });
        RemoveRomMetadataCommand = new DelegateCommand<RomMetadataViewModel>(item => Definition?.RomMetadata.Remove(item));

        LoadFiles();
    }

    private void LoadFiles()
    {
        Files.Clear();
        Directory.CreateDirectory(_directory);

        foreach (string filePath in Directory.EnumerateFiles(_directory, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath));
                if (document.RootElement.TryGetProperty("Name", out JsonElement nameElement))
                    name = nameElement.GetString() ?? name;
            }
            catch (JsonException) { }
            Files.Add(new(filePath, name));
        }

        SelectedFile = Files.FirstOrDefault();
    }

    private void LoadDefinition(string filePath)
    {
        try
        {
            UnsubscribeDefinition();
            Definition = EmulatorDefinitionViewModel.FromModel(JsonSerializer.Deserialize<EmulatorDefinition>(File.ReadAllText(filePath), SerializerOptions) ?? throw new JsonException("The file is empty."));
            SubscribeDefinition();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            UnsubscribeDefinition();
            Definition = null;
        }
    }

    private void SubscribeDefinition()
    {
        if (Definition is null)
            return;

        Definition.PropertyChanged += Definition_PropertyChanged;
        Definition.Executables.PropertyChanged += Definition_PropertyChanged;
        Definition.ProductNames.PropertyChanged += Definition_PropertyChanged;
        Definition.RomExtensions.PropertyChanged += Definition_PropertyChanged;
        SubscribeCollection(Definition.Configurations);
        SubscribeCollection(Definition.PortableLocations);
        SubscribeCollection(Definition.RomMetadata);
    }

    private void UnsubscribeDefinition()
    {
        if (Definition is null)
            return;

        Definition.PropertyChanged -= Definition_PropertyChanged;
        Definition.Executables.PropertyChanged -= Definition_PropertyChanged;
        Definition.ProductNames.PropertyChanged -= Definition_PropertyChanged;
        Definition.RomExtensions.PropertyChanged -= Definition_PropertyChanged;
        UnsubscribeCollection(Definition.Configurations);
        UnsubscribeCollection(Definition.PortableLocations);
        UnsubscribeCollection(Definition.RomMetadata);
    }

    private void SubscribeCollection<T>(ObservableCollection<T> collection) where T : BaseViewModel
    {
        collection.CollectionChanged += Collection_CollectionChanged;
        foreach (T item in collection)
            SubscribeItem(item);
    }

    private void UnsubscribeCollection<T>(ObservableCollection<T> collection) where T : BaseViewModel
    {
        collection.CollectionChanged -= Collection_CollectionChanged;
        foreach (T item in collection)
            UnsubscribeItem(item);
    }

    private void SubscribeItem(BaseViewModel item)
    {
        item.PropertyChanged += Definition_PropertyChanged;
        if (item is ConfigLocationViewModel config)
        {
            config.Files.PropertyChanged += Definition_PropertyChanged;
            config.ContentKeys.PropertyChanged += Definition_PropertyChanged;
        }
        else if (item is PortableLocationViewModel portable)
        {
            portable.Files.PropertyChanged += Definition_PropertyChanged;
        }
        else if (item is RomMetadataViewModel metadata)
        {
            metadata.RelativePaths.PropertyChanged += Definition_PropertyChanged;
        }
    }

    private void UnsubscribeItem(BaseViewModel item)
    {
        item.PropertyChanged -= Definition_PropertyChanged;
        if (item is ConfigLocationViewModel config)
        {
            config.Files.PropertyChanged -= Definition_PropertyChanged;
            config.ContentKeys.PropertyChanged -= Definition_PropertyChanged;
        }
        else if (item is PortableLocationViewModel portable)
        {
            portable.Files.PropertyChanged -= Definition_PropertyChanged;
        }
        else if (item is RomMetadataViewModel metadata)
        {
            metadata.RelativePaths.PropertyChanged -= Definition_PropertyChanged;
        }
    }

    private void Collection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (BaseViewModel item in e.OldItems)
                UnsubscribeItem(item);

        if (e.NewItems is not null)
            foreach (BaseViewModel item in e.NewItems)
                SubscribeItem(item);

        SaveFile();
    }

    private void Definition_PropertyChanged(object? sender, PropertyChangedEventArgs e) => SaveFile();

    private void NewFile()
    {
        string filePath = Path.Combine(_directory, "new-emulator.json");
        int suffix = 1;

        while (File.Exists(filePath))
            filePath = Path.Combine(_directory, $"new-emulator-{suffix++}.json");

        File.WriteAllText(filePath, JsonSerializer.Serialize(new EmulatorDefinition { Id = "new-emulator", Name = "New Emulator", PlatformType = GamePlatform.Generic }, SerializerOptions));
        EmulatorDefinitions.Reload(); LoadFiles(); SelectedFile = Files.FirstOrDefault(x => x.FilePath == filePath);
    }

    private void SaveFile()
    {
        if (SelectedFile is null || Definition is null)
            return;

        try
        {
            EmulatorDefinition model = Definition.ToModel();
            if (string.IsNullOrWhiteSpace(model.Id) || string.IsNullOrWhiteSpace(model.Name))
            {
                // Id and Name are required
                return;
            }

            string temporaryPath = SelectedFile.FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(model, SerializerOptions));
            File.Move(temporaryPath, SelectedFile.FilePath, true);

            EmulatorDefinitions.Reload();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogManager.LogError("Could not save: {0}", ex.Message);
        }
    }

    private void DeleteFile()
    {
        if (SelectedFile is null)
            return;

        try
        {
            File.Delete(SelectedFile.FilePath);
            EmulatorDefinitions.Reload();
            LoadFiles();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogManager.LogError("Could not delete: {0}", ex.Message);
        }
    }
}
