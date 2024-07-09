using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Xml;

namespace HandheldCompanion
{
    public class CustomSettingsProvider : SettingsProvider
    {
        // Define some constants for the XML elements and attributes
        private const string RootNodeName = "configuration";
        private const string SettingsNodeName = "settings";
        private const string SettingNodeName = "setting";
        private const string NameAttribute = "name";
        private const string SerializeAsAttribute = "serializeAs";
        private const string ValueAttribute = "value";
        private const string UserConfigFileName = "user.config";

        // Define a property to store the location of the user.config file
        public string UserConfigPath { get; set; }

        // Override the ApplicationName property to return the name of the current assembly
        public override string ApplicationName
        {
            get { return Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location); }
            set { }
        }

        // Override the Initialize method to set the UserConfigPath property
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(ApplicationName, config);

            // Get the path from the config parameter, or use a default value
            string myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string SettingsPath = Path.Combine(myDocumentsPath, "HandheldCompanion");
            UserConfigPath = Path.Combine(myDocumentsPath, "HandheldCompanion", UserConfigFileName);

            if (!Directory.Exists(SettingsPath))
                Directory.CreateDirectory(SettingsPath);

            // temporary, pull previous settings, if any
            string previousPath = Path.Combine(applicationDataPath, ApplicationName, UserConfigFileName);

            if (File.Exists(previousPath))
            {
                if (!File.Exists(UserConfigPath))
                    File.Move(previousPath, UserConfigPath);
                else
                    File.Delete(previousPath);
            }
        }

        // Override the GetPropertyValues method to read the settings from the user.config file
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            // Create a collection to store the values
            SettingsPropertyValueCollection values = [];

            // Load the user.config file into an XmlDocument
            XmlDocument document = new XmlDocument();
            try
            {
                document.Load(UserConfigPath);
            }
            catch (Exception)
            {
                document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", string.Empty));
                document.AppendChild(document.CreateElement(RootNodeName));
                document.DocumentElement.AppendChild(document.CreateElement(SettingsNodeName));
            }

            // Loop through each setting in the collection
            foreach (SettingsProperty setting in collection)
            {
                // Create a value object for this setting
                SettingsPropertyValue value = new SettingsPropertyValue(setting);

                // Try to find a matching node in the user.config file
                XmlNode node = document.SelectSingleNode(string.Format("/{0}/{1}/{2}[@{3}='{4}']", RootNodeName, SettingsNodeName, SettingNodeName, NameAttribute, setting.Name));

                // If a node is found, get the value from it
                if (node != null)
                {
                    value.SerializedValue = node.Attributes[ValueAttribute].Value;
                }
                else
                {
                    // Otherwise, use the default value
                    value.SerializedValue = setting.DefaultValue;
                }

                // Add the value object to the collection
                values.Add(value);
            }

            // Return the collection
            return values;
        }

        // Override the SetPropertyValues method to write the settings to the user.config file
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            // Load or create the user.config file into an XmlDocument
            XmlDocument document = new XmlDocument();
            try
            {
                document.Load(UserConfigPath);
            }
            catch (Exception)
            {
                document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", string.Empty));
                document.AppendChild(document.CreateElement(RootNodeName));
                document.DocumentElement.AppendChild(document.CreateElement(SettingsNodeName));
            }

            // Loop through each setting in the collection
            foreach (SettingsPropertyValue value in collection)
            {
                try
                {
                    // Try to find a matching node in the user.config file
                    XmlNode node = document.SelectSingleNode(string.Format("/{0}/{1}/{2}[@{3}='{4}']", RootNodeName, SettingsNodeName, SettingNodeName, NameAttribute, value.Name));

                    // If a node is not found, create a new one and append it to the settings node
                    if (node == null)
                    {
                        node = document.CreateElement(SettingNodeName);
                        XmlAttribute nameAttribute = document.CreateAttribute(NameAttribute);
                        nameAttribute.Value = value.Name;
                        node.Attributes.Append(nameAttribute);
                        XmlAttribute serializeAsAttribute = document.CreateAttribute(SerializeAsAttribute);
                        serializeAsAttribute.Value = value.Property.SerializeAs.ToString();
                        node.Attributes.Append(serializeAsAttribute);
                        document.DocumentElement.SelectSingleNode(SettingsNodeName).AppendChild(node);
                    }

                    // Set or update the value attribute of the node
                    XmlAttribute valueAttribute = node.Attributes[ValueAttribute];
                    if (valueAttribute == null)
                    {
                        valueAttribute = document.CreateAttribute(ValueAttribute);
                        node.Attributes.Append(valueAttribute);
                    }

                    if (value.SerializedValue is not null)
                        valueAttribute.Value = value.SerializedValue.ToString();
                }
                catch { }
            }

            // Save the user.config file
            document.Save(UserConfigPath);
        }
    }
}
