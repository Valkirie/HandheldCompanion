using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Text;
using HandheldCompanion.Platforms;
using HandheldCompanion.Managers;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using File = System.IO.File;

namespace HandheldCompanion.Platforms.Discovery;

public static class EmulatorDiscoveryService
{
    public static IEnumerable<DiscoveredGame> Discover(GamePlatform platform = GamePlatform.All)
    {
        return Discover(EmulatorDefinitions.All.Where(definition => platform.HasFlag(definition.PlatformType)));
    }

    public static IEnumerable<DiscoveredGame> DiscoverBySystem(string system)
    {
        return Discover(EmulatorDefinitions.All.Where(definition =>
            definition.Systems.Contains(system, StringComparer.OrdinalIgnoreCase)));
    }

    private static IEnumerable<DiscoveredGame> Discover(IEnumerable<EmulatorDefinition> definitions)
    {
        foreach (EmulatorDefinition definition in definitions)
        {
            foreach (EmulatorInstallation installation in DiscoverInstallations(definition))
            {
                yield return new DiscoveredGame(definition.Name, installation.ExecutablePath, installation.ExecutablePath, string.Empty, definition.PlatformType, true, installation.ExecutablePaths);

                foreach (string contentPath in installation.ContentPaths)
                foreach (string rom in EnumerateRoms(contentPath, definition))
                {
                    string? arguments = BuildArguments(definition, rom);
                    if (arguments is not null)
                    {
                        string fallbackName = Path.GetFileNameWithoutExtension(rom);
                    yield return new DiscoveredGame(ReadRomName(definition, rom) ?? fallbackName, rom, installation.ExecutablePath, arguments, definition.PlatformType);
                    }
                }
            }
        }
    }

    private static IEnumerable<EmulatorInstallation> DiscoverInstallations(EmulatorDefinition definition)
    {
        HashSet<string> executablePaths = new(StringComparer.OrdinalIgnoreCase);
        List<string> discoveredExecutables = [];
        foreach (string executable in definition.Executables)
        foreach (string candidate in ManagerFactory.profileManager.GetProfiles()
            .Where(profile => !profile.Default &&
                string.Equals(Path.GetFileName(profile.Path), executable, StringComparison.OrdinalIgnoreCase))
            .Select(profile => profile.Path)
            .Concat(FindExecutableCandidates(definition, executable)))
            if (File.Exists(candidate) && executablePaths.Add(candidate))
                discoveredExecutables.Add(candidate);

        if (discoveredExecutables.Count == 0)
            yield break;

        List<string> configFiles = discoveredExecutables.SelectMany(candidate => FindConfigurationFiles(definition, candidate)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        List<string> contentPaths = configFiles.SelectMany(file => ReadContentPaths(file, definition)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        yield return new EmulatorInstallation
        {
            Definition = definition,
            ExecutablePath = discoveredExecutables[0],
            ExecutablePaths = discoveredExecutables,
            ConfigFiles = configFiles,
            ContentPaths = contentPaths
        };
    }

    private static IEnumerable<string> FindExecutableCandidates(EmulatorDefinition definition, string executable)
    {
        foreach (string candidate in FindUserAssistCandidates(executable))
            yield return candidate;

        foreach (string candidate in FindRecentAppCandidates(executable))
            yield return candidate;

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            yield return Path.Combine(directory.Trim(), executable);

        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Default, RegistryView.Registry32, RegistryView.Registry64 }.Distinct())
        {
            List<string> registryCandidates = [];
            RegistryKey? baseKey = null;
            try
            {
                baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? appPath = baseKey.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{executable}");
                string? appPathValue = appPath?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(appPathValue))
                    registryCandidates.Add(UnquoteExecutable(appPathValue));

                using RegistryKey? uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is not null)
                    foreach (string name in uninstall.GetSubKeyNames())
                    {
                        using RegistryKey? entry = uninstall.OpenSubKey(name);
                        if (!MatchesProduct(entry, definition))
                            continue;
                        foreach (string valueName in new[] { "InstallLocation", "DisplayIcon", "UninstallString" })
                        {
                            string? registryValue = entry?.GetValue(valueName) as string;
                            if (!string.IsNullOrWhiteSpace(registryValue))
                                registryCandidates.AddRange(CandidatesFromValue(registryValue, executable));
                        }
                    }
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            finally { baseKey?.Dispose(); }
            foreach (string candidate in registryCandidates)
                yield return candidate;
        }

        foreach (string root in GetKnownRoots(definition))
        {
            yield return Path.Combine(root, executable);
            IEnumerable<string> directories;
            try { directories = Directory.EnumerateDirectories(root); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (string directory in directories)
                yield return Path.Combine(directory, executable);
        }

        foreach (string root in GetShortcutRoots())
        {
            IEnumerable<string> shortcuts;
            try { shortcuts = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (string shortcut in shortcuts)
            {
                string? target = null;
                try
                {
                    IWshShortcut link = (IWshShortcut)new WshShell().CreateShortcut(shortcut);
                    if (string.Equals(Path.GetFileName(link.TargetPath), executable, StringComparison.OrdinalIgnoreCase))
                        target = link.TargetPath;
                }
                catch { }
                if (target is not null)
                    yield return target;
            }
        }
    }

    private static IEnumerable<string> FindUserAssistCandidates(string executable)
    {
        using RegistryKey? userAssist = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist");
        if (userAssist is null)
            yield break;

        foreach (string identifier in userAssist.GetSubKeyNames())
        {
            using RegistryKey? count = userAssist.OpenSubKey($@"{identifier}\Count");
            if (count is null)
                continue;

            foreach (string valueName in count.GetValueNames())
            {
                string decoded = DecodeRot13(valueName);
                if (string.Equals(Path.GetFileName(decoded), executable, StringComparison.OrdinalIgnoreCase))
                    yield return decoded;
            }
        }
    }

    private static IEnumerable<string> FindRecentAppCandidates(string executable)
    {
        using RegistryKey? recentApps = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Search\RecentApps");
        if (recentApps is null)
            yield break;

        foreach (string app in recentApps.GetSubKeyNames())
        {
            using RegistryKey? appKey = recentApps.OpenSubKey(app);
            if (appKey is null)
                continue;

            foreach (string valueName in appKey.GetValueNames())
            {
                if (appKey.GetValue(valueName) is not string value)
                    continue;

                if (string.Equals(Path.GetFileName(value), executable, StringComparison.OrdinalIgnoreCase))
                    yield return value;
            }

            if (string.Equals(Path.GetFileName(app), executable, StringComparison.OrdinalIgnoreCase))
                yield return app;
        }
    }

    private static string DecodeRot13(string value)
    {
        return string.Create(value.Length, value, static (buffer, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                buffer[i] = character switch
                {
                    >= 'A' and <= 'Z' => (char)('A' + (character - 'A' + 13) % 26),
                    >= 'a' and <= 'z' => (char)('a' + (character - 'a' + 13) % 26),
                    _ => character
                };
            }
        });
    }

    private static IEnumerable<string> FindConfigurationFiles(EmulatorDefinition definition, string executable)
    {
        string executableDirectory = Path.GetDirectoryName(executable) ?? string.Empty;
        foreach (ConfigLocation location in definition.Configurations)
        {
            string root = ResolveRoot(location.Root, executableDirectory);
            string directory = Path.Combine(root, location.RelativePath);
            foreach (string file in location.Files)
            {
                if (file.Contains('*'))
                {
                    IEnumerable<string> matches;
                    try { matches = Directory.EnumerateFiles(directory, file, SearchOption.TopDirectoryOnly); }
                    catch { continue; }
                    foreach (string match in matches) yield return match;
                }
                else
                {
                    string candidate = Path.Combine(directory, file);
                    if (File.Exists(candidate)) yield return candidate;
                }
            }
        }

        foreach (PortableLocation location in definition.PortableLocations)
        {
            string marker = Path.Combine(executableDirectory, location.Marker);
            if (!File.Exists(marker) && !Directory.Exists(marker)) continue;
            string directory = Path.Combine(executableDirectory, location.ConfigPath);
            foreach (string file in location.Files)
            {
                string candidate = Path.Combine(directory, file);
                if (File.Exists(candidate)) yield return candidate;
            }
        }
    }

    private static IEnumerable<string> ReadContentPaths(string file, EmulatorDefinition definition)
    {
        string content;
        try { content = File.ReadAllText(file); }
        catch { yield break; }

        IEnumerable<string> configuredPaths;
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".json":
                configuredPaths = ReadJsonContentPaths(content, definition);
                break;
            case ".xml":
                configuredPaths = ReadXmlContentPaths(content, definition);
                break;
            case ".ini":
                configuredPaths = ReadIniContentPaths(content, definition);
                break;
            default:
                configuredPaths = [];
                break;
        }

        foreach (string path in configuredPaths)
            if (Directory.Exists(path))
                yield return path;
    }

    private static IEnumerable<string> ReadTextContentPaths(string content, EmulatorDefinition definition)
    {
        string[] keys = definition.Configurations
            .SelectMany(configuration => configuration.ContentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length > 0)
        {
            foreach (string key in keys)
            {
                string pattern = "(?im)(?:^[\"'<>]|\\s|[=:])"
                    + Regex.Escape(key)
                    + "(?:\"|'|\\s|=|>)*[:=]?\\s*[\"']?(?<value>[A-Za-z]:[^\"'<>\\r\\n,;]*|\\\\\\\\[^\"'<>\\r\\n,;]+)";
                foreach (Match match in Regex.Matches(content, pattern, RegexOptions.CultureInvariant))
                {
                    yield return match.Groups["value"].Value
                        .Trim()
                        .Trim('"', '\'')
                        .TrimEnd('>', ']', '}', ';')
                        .Replace("\\\\", "\\", StringComparison.Ordinal);
                }
            }
        }
        else
        {
            const string pathPattern = "(?<value>[A-Za-z]:[\\\\/][^\\\"'<>\\r\\n,;]+|\\\\\\\\[^\\\"'<>\\r\\n,;]+)";
            foreach (Match match in Regex.Matches(content, pathPattern, RegexOptions.CultureInvariant))
            {
                yield return match.Groups["value"].Value
                    .Trim()
                    .Trim('"', '\'')
                    .TrimEnd('>', ']', '}', ';')
                    .Replace("\\\\", "\\", StringComparison.Ordinal);
            }
        }
    }

    private static IEnumerable<string> ReadXmlContentPaths(string content, EmulatorDefinition definition)
    {
        XDocument document;
        try { document = XDocument.Parse(content, LoadOptions.None); }
        catch { yield break; }

        string[] keys = definition.Configurations.SelectMany(configuration => configuration.ContentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (XElement element in EnumerateXmlElements(document.Root))
            if (keys.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
                foreach (string value in EnumerateXmlText(element))
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return value.Trim();
    }

    private static IEnumerable<string> EnumerateXmlText(XElement element)
    {
        if (!element.Elements().Any())
        {
            yield return element.Value;
            yield break;
        }

        foreach (XElement child in element.Elements())
        foreach (string value in EnumerateXmlText(child))
            yield return value;
    }

    private static IEnumerable<XElement> EnumerateXmlElements(XElement? element)
    {
        if (element is null)
            yield break;

        yield return element;
        foreach (XElement child in element.Elements())
        foreach (XElement descendant in EnumerateXmlElements(child))
            yield return descendant;
    }

    private static IEnumerable<string> ReadIniContentPaths(string content, EmulatorDefinition definition)
    {
        HashSet<string> keys = definition.Configurations.SelectMany(configuration => configuration.ContentKeys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string entry = line.Trim();
            if (entry.StartsWith(';') || entry.StartsWith('#'))
                continue;

            int separator = entry.IndexOf('=');
            if (separator <= 0 || !keys.Contains(entry[..separator].Trim()))
                continue;

            string value = entry[(separator + 1)..].Trim().Trim('"', '\'');
            foreach (string path in value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return path.Trim().Trim('"', '\'');
        }
    }

    private static IEnumerable<string> ReadJsonContentPaths(string content, EmulatorDefinition definition)
    {
        JObject document;
        try { document = JObject.Parse(content); }
        catch { yield break; }

        foreach (string key in definition.Configurations.SelectMany(configuration => configuration.ContentKeys).Distinct(StringComparer.OrdinalIgnoreCase))
        foreach (JProperty property in EnumerateJsonProperties(document)
            .Where(property => string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)))
        foreach (JValue value in EnumerateJsonValues(property.Value))
            if (value.Type == JTokenType.String && !string.IsNullOrWhiteSpace(value.Value<string>()))
                yield return value.Value<string>()!;
    }

    private static IEnumerable<JValue> EnumerateJsonValues(JToken token)
    {
        if (token is JValue value)
        {
            yield return value;
            yield break;
        }

        foreach (JToken child in token.Children())
        foreach (JValue childValue in EnumerateJsonValues(child))
            yield return childValue;
    }

    private static IEnumerable<JProperty> EnumerateJsonProperties(JToken token)
    {
        if (token is not JObject objectToken)
            yield break;

        foreach (JProperty property in objectToken.Properties())
        {
            yield return property;

            foreach (JProperty child in EnumerateJsonProperties(property.Value))
                yield return child;
        }
    }

    private static IEnumerable<string> EnumerateRoms(string root, EmulatorDefinition definition)
    {
        if (definition.DirectoryRoms)
        {
            IEnumerable<string> directories;
            try { directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories); }
            catch { yield break; }

            foreach (string directory in directories)
                if (HasRomMetadata(directory, definition.RomMetadata))
                    yield return directory;
            yield break;
        }

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
        catch { yield break; }
        foreach (string file in files)
            if (definition.RomExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) yield return file;
    }

    private static bool HasRomMetadata(string directory, RomMetadataRule? rule)
    {
        if (rule is null)
            return false;

        string[] relativePaths = rule.RelativePaths.Length > 0 ? rule.RelativePaths : [rule.RelativePath];
        return relativePaths.Any(path => File.Exists(Path.Combine(directory, path)));
    }

    private static string ResolveRoot(ConfigRoot root, string executableDirectory) => root switch
    {
        ConfigRoot.ExecutableDirectory => executableDirectory,
        ConfigRoot.AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ConfigRoot.LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ConfigRoot.Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        ConfigRoot.UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        _ => string.Empty
    };

    private static IEnumerable<string> GetKnownRoots(EmulatorDefinition definition) => definition.Configurations.Select(configuration => ResolveRoot(configuration.Root, string.Empty)).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> GetShortcutRoots() => new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }.Where(Directory.Exists);

    private static bool MatchesProduct(RegistryKey? key, EmulatorDefinition definition) => definition.ProductNames.Length == 0 || definition.ProductNames.Any(name => (key?.GetValue("DisplayName") as string)?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);

    private static IEnumerable<string> CandidatesFromValue(string value, string executable) { string candidate = UnquoteExecutable(value); if (File.Exists(candidate) && Path.GetFileName(candidate).Equals(executable, StringComparison.OrdinalIgnoreCase)) yield return candidate; if (Directory.Exists(candidate)) yield return Path.Combine(candidate, executable); }

    private static string UnquoteExecutable(string value) { value = value.Trim(); if (value.StartsWith('"')) { int end = value.IndexOf('"', 1); return end > 0 ? value[1..end] : value.Trim('"'); } int space = value.IndexOf(' '); return space > 0 ? value[..space] : value; }

    private static string Quote(string path, string prefix) => string.IsNullOrEmpty(prefix) ? $"\"{path}\"" : $"{prefix} \"{path}\"";

    private static string? BuildArguments(EmulatorDefinition definition, string rom) => definition.LaunchArgumentMode switch
    {
        LaunchArgumentMode.Template => definition.ArgumentTemplate.Replace("{rom}", Quote(rom, string.Empty), StringComparison.Ordinal),
        LaunchArgumentMode.Unsupported => null,
        _ => Quote(rom, definition.ArgumentPrefix)
    };

    private static string? ReadRomName(EmulatorDefinition definition, string rom)
    {
        RomMetadataRule? rule = definition.RomMetadata;
        if (rule is null)
            return null;

        string? romDirectory = Directory.Exists(rom) ? rom : Path.GetDirectoryName(rom);
        if (string.IsNullOrWhiteSpace(romDirectory) || !Directory.Exists(romDirectory))
            return null;

        string[] relativePaths = rule.RelativePaths.Length > 0 ? rule.RelativePaths : [rule.RelativePath];
        IEnumerable<string> roots = rule.SearchParentDirectories
            ? EnumerateParentDirectories(romDirectory)
            : [romDirectory];
        IEnumerable<string> metadataPaths = roots.SelectMany(root => relativePaths.Select(path => Path.Combine(root, path)));
        foreach (string metadataPath in metadataPaths)
        {
            string? value = rule.Provider switch
            {
                RomMetadataProvider.ParamSfo => ReadParamSfoName(metadataPath),
                _ => ReadXmlElementName(metadataPath, rule.ElementName)
            };
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateParentDirectories(string directory)
    {
        DirectoryInfo? current = new(directory);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static string? ReadXmlElementName(string path, string elementName)
    {
        try
        {
            return XDocument.Load(path, LoadOptions.None).Descendants(elementName).FirstOrDefault()?.Value.Trim();
        }
        catch { return null; }
    }

    private static string? ReadParamSfoName(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);
            if (reader.ReadUInt32() != 0x46535000 || reader.ReadUInt32() != 0x00000101)
                return null;

            uint keyOffset = reader.ReadUInt32();
            uint valueOffset = reader.ReadUInt32();
            ushort entryCount = reader.ReadUInt16();
            stream.Position = 20;
            for (int i = 0; i < entryCount; i++)
            {
                stream.Position = 20 + i * 16;
                uint keyIndex = reader.ReadUInt16();
                reader.ReadByte();
                reader.ReadByte();
                uint valueLength = reader.ReadUInt32();
                reader.ReadUInt32();
                uint dataOffset = reader.ReadUInt32();
                long keyPosition = keyOffset + keyIndex;
                if (keyPosition < 0 || keyPosition >= stream.Length)
                    continue;
                stream.Position = keyPosition;
                string key = ReadNullTerminated(reader, 256);
                if (!string.Equals(key, "TITLE", StringComparison.OrdinalIgnoreCase))
                    continue;
                long valuePosition = valueOffset + dataOffset;
                if (valuePosition < 0 || valuePosition >= stream.Length || valueLength > stream.Length - valuePosition)
                    return null;
                stream.Position = valuePosition;
                string value = Encoding.UTF8.GetString(reader.ReadBytes((int)valueLength)).TrimEnd('\0', ' ', '\r', '\n');
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }
        catch { }

        return null;
    }

    private static string ReadNullTerminated(BinaryReader reader, int maxLength)
    {
        StringBuilder value = new();
        for (int i = 0; i < maxLength && reader.BaseStream.Position < reader.BaseStream.Length; i++)
        {
            byte character = reader.ReadByte();
            if (character == 0)
                break;
            value.Append((char)character);
        }
        return value.ToString();
    }

}
