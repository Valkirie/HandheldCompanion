using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Xml;

namespace HandheldCompanion
{
    public class CustomSettingsProvider : SettingsProvider
    {
        private const string RootNodeName = "configuration";
        private const string SettingsNodeName = "settings";
        private const string SettingNodeName = "setting";
        private const string NameAttribute = "name";
        private const string SerializeAsAttribute = "serializeAs";
        private const string ValueAttribute = "value";
        private const string UserConfigFileName = "user.config";

        private readonly object _sync = new();

        public string UserConfigPath { get; set; } = string.Empty;

        public override string ApplicationName
        {
            get => Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);
            set { /* noop */ }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(ApplicationName, config);

            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string settingsPath = Path.Combine(myDocumentsPath, "HandheldCompanion");
            UserConfigPath = Path.Combine(settingsPath, UserConfigFileName);

            if (!Directory.Exists(settingsPath))
                Directory.CreateDirectory(settingsPath);

            // one-time migration
            string previousPath = Path.Combine(applicationDataPath, ApplicationName, UserConfigFileName);
            if (File.Exists(previousPath))
            {
                if (!File.Exists(UserConfigPath))
                    File.Move(previousPath, UserConfigPath);
                else
                    File.Delete(previousPath);
            }
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            var values = new SettingsPropertyValueCollection();

            XmlDocument doc = LoadOrCreateDocument(quarantineOnFailure: true);

            foreach (SettingsProperty setting in collection)
            {
                var spv = new SettingsPropertyValue(setting);

                XmlNode? node = doc.SelectSingleNode($"/{RootNodeName}/{SettingsNodeName}/{SettingNodeName}[@{NameAttribute}='{setting.Name}']");
                if (node is not null && node.Attributes?[ValueAttribute] is XmlAttribute valAttr)
                {
                    spv.SerializedValue = valAttr.Value;
                    // If you have non-string settings, deserialize here based on setting.SerializeAs
                    // and assign spv.PropertyValue. For plain strings, SerializedValue is fine.
                }
                else
                {
                    spv.SerializedValue = setting.DefaultValue;
                }

                // Mark clean: they just came from persisted storage (or defaults)
                spv.IsDirty = false;
                values.Add(spv);
            }

            return values;
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            lock (_sync)
            {
                XmlDocument doc = LoadOrCreateDocument(quarantineOnFailure: false);
                EnsureSkeleton(doc);

                XmlNode settingsNode = doc.DocumentElement!.SelectSingleNode(SettingsNodeName)!;

                foreach (SettingsPropertyValue spv in collection)
                {
                    // Optionally skip unchanged props:
                    // if (!spv.IsDirty) continue;

                    XmlNode? node = doc.SelectSingleNode($"/{RootNodeName}/{SettingsNodeName}/{SettingNodeName}[@{NameAttribute}='{spv.Name}']");
                    if (node is null)
                    {
                        node = doc.CreateElement(SettingNodeName);

                        var nameAttr = doc.CreateAttribute(NameAttribute);
                        nameAttr.Value = spv.Name;
                        node.Attributes!.Append(nameAttr);

                        var serializeAsAttr = doc.CreateAttribute(SerializeAsAttribute);
                        serializeAsAttr.Value = spv.Property.SerializeAs.ToString();
                        node.Attributes!.Append(serializeAsAttr);

                        settingsNode.AppendChild(node);
                    }

                    var valueAttr = node.Attributes![ValueAttribute] ?? doc.CreateAttribute(ValueAttribute);
                    valueAttr.Value = spv.SerializedValue?.ToString() ?? string.Empty;
                    if (node.Attributes![ValueAttribute] is null)
                        node.Attributes!.Append(valueAttr);
                }

                AtomicSave(doc, UserConfigPath);
            }
        }

        private XmlDocument LoadOrCreateDocument(bool quarantineOnFailure)
        {
            var doc = new XmlDocument();
            try
            {
                if (File.Exists(UserConfigPath))
                {
                    doc.Load(UserConfigPath);
                }
                else
                {
                    // brand-new file
                    CreateSkeleton(doc);
                }
            }
            catch
            {
                if (quarantineOnFailure)
                {
                    // Rename bad file one time so future loads work
                    TryQuarantine(UserConfigPath);
                    CreateSkeleton(doc);
                }
                else
                {
                    CreateSkeleton(doc);
                }
            }

            EnsureSkeleton(doc);
            return doc;
        }

        private static void CreateSkeleton(XmlDocument doc)
        {
            doc.RemoveAll();
            doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
            var root = doc.CreateElement(RootNodeName);
            doc.AppendChild(root);
            root.AppendChild(doc.CreateElement(SettingsNodeName));
        }

        private static void EnsureSkeleton(XmlDocument doc)
        {
            if (doc.DocumentElement?.Name != RootNodeName)
            {
                CreateSkeleton(doc);
                return;
            }

            var settings = doc.DocumentElement.SelectSingleNode(SettingsNodeName);
            if (settings is null)
            {
                doc.DocumentElement.AppendChild(doc.CreateElement(SettingsNodeName));
            }
        }

        private static void TryQuarantine(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                string bak = path + ".bak";
                // Avoid overwriting an older backup
                if (File.Exists(bak))
                {
                    string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    bak = path + "." + ts + ".bak";
                }
                File.Move(path, bak);
            }
            catch
            {
                // Swallow: if we can't move it, we'll just overwrite later.
            }
        }

        private static void AtomicSave(XmlDocument doc, string destinationPath)
        {
            string dir = Path.GetDirectoryName(destinationPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tempPath = destinationPath + ".tmp";

            // Write to temp
            using (var writer = XmlWriter.Create(tempPath, new XmlWriterSettings { Indent = true }))
            {
                doc.Save(writer);
            }

            try
            {
                if (File.Exists(destinationPath))
                {
                    // Replace existing file, keep a short backup
                    string backup = destinationPath + ".bak_last";
                    File.Replace(tempPath, destinationPath, backup, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, destinationPath);
                }
            }
            catch
            {
                // Fallback: best effort
                try
                {
                    File.Copy(tempPath, destinationPath, overwrite: true);
                    File.Delete(tempPath);
                }
                catch { /* ignore */ }
            }
        }
    }
}