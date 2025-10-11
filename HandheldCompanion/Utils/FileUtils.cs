using HandheldCompanion.Shared;
using IWshRuntimeLibrary;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml;
using File = System.IO.File;

namespace HandheldCompanion.Utils
{
    static class FileUtils
    {
        public static CommonFileDialogResult CommonFileDialog(out string path, out string arguments, out string name, string initialPath = null)
        {
            path = string.Empty;
            arguments = string.Empty;
            name = string.Empty;

            CommonOpenFileDialog openFileDialog = new CommonOpenFileDialog();
            openFileDialog.Filters.Add(new CommonFileDialogFilter("Executable", "*.exe"));
            openFileDialog.Filters.Add(new CommonFileDialogFilter("Shortcuts", "*.lnk"));
            openFileDialog.Filters.Add(new CommonFileDialogFilter("UWP manifest", "*AppxManifest.xml"));
            openFileDialog.NavigateToShortcut = false;

            string? dir = Path.GetDirectoryName(initialPath);
            if (File.Exists(initialPath) && !string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                openFileDialog.InitialDirectory = dir;

            CommonFileDialogResult errorCode = openFileDialog.ShowDialog();
            if (errorCode != CommonFileDialogResult.Ok)
                return errorCode;

            path = openFileDialog.FileName;
            if (string.IsNullOrEmpty(path))
                return errorCode;

            string folder = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);
            string ext = Path.GetExtension(file);
            name = file.Replace(ext, string.Empty);

            if (ext.Equals(".lnk"))
            {
                WshShell wsh = new WshShell();
                IWshShortcut link = (IWshShortcut)wsh.CreateShortcut(path);

                // get real path
                path = link.TargetPath;

                // get arguments
                arguments = link.Arguments;

                folder = Path.GetDirectoryName(path);
                file = Path.GetFileName(link.TargetPath);
                ext = Path.GetExtension(file);
            }

            switch (ext)
            {
                default:
                case ".exe":
                    break;
                case ".xml":
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        string UWPpath = string.Empty;
                        string UWPfile = string.Empty;

                        // check if MicrosoftGame.config exists
                        string configPath = Path.Combine(folder, "MicrosoftGame.config");
                        if (File.Exists(configPath))
                        {
                            doc.Load(configPath);

                            XmlNodeList ExecutableList = doc.GetElementsByTagName("ExecutableList");
                            foreach (XmlNode node in ExecutableList)
                                foreach (XmlNode child in node.ChildNodes)
                                    if (child.Name.Equals("Executable"))
                                        if (child.Attributes is not null)
                                            foreach (XmlAttribute attribute in child.Attributes)
                                                switch (attribute.Name)
                                                {
                                                    case "Name":
                                                        UWPpath = Path.Combine(folder, attribute.InnerText);
                                                        UWPfile = Path.GetFileName(path);
                                                        break;
                                                }
                        }

                        // either there was no config file, either we couldn't find an executable within it
                        if (!File.Exists(UWPpath))
                        {
                            doc.Load(path);

                            XmlNodeList Applications = doc.GetElementsByTagName("Applications");
                            foreach (XmlNode node in Applications)
                                foreach (XmlNode child in node.ChildNodes)
                                    if (child.Name.Equals("Application"))
                                        if (child.Attributes is not null)
                                            foreach (XmlAttribute attribute in child.Attributes)
                                                switch (attribute.Name)
                                                {
                                                    case "Executable":
                                                        UWPpath = Path.Combine(folder, attribute.InnerText);
                                                        UWPfile = Path.GetFileName(path);
                                                        break;
                                                }
                        }

                        // we're good to go
                        if (File.Exists(UWPpath))
                        {
                            path = UWPpath;
                            file = UWPfile;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError(ex.Message, true);
                    }

                    break;
            }

            return errorCode;
        }

        public static bool IsFileWritable(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    using (var fs = new FileStream(fileName, FileMode.Open))
                    {
                        return fs.CanWrite;
                    }
                }

                string dirPath = Path.GetDirectoryName(fileName);
                return IsDirectoryWritable(dirPath);
            }
            catch { }

            return false;
        }

        public static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                if (Directory.Exists(dirPath))
                {
                    using (var fs = File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool IsFileUsed(string fileName)
        {
            // Check if the file exists
            if (File.Exists(fileName))
            {
                // Try to open the file for reading
                try
                {
                    using (StreamReader sr = new StreamReader(fileName))
                    {
                        // If no exception is thrown, the file is being used
                        return false;
                    }
                }
                catch (IOException)
                {
                    // If an exception is thrown, the file is not being used
                    return true;
                }
            }

            return false;
        }

        public static void SetDirectoryWritable(string dirPath)
        {
            var rootDirectory = new DirectoryInfo(dirPath);
            var directorySecurity = rootDirectory.GetAccessControl();

            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var adminitrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            directorySecurity.AddAccessRule(
                new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.FullControl,
                    InheritanceFlags.None,
                    PropagationFlags.NoPropagateInherit,
                    AccessControlType.Allow));

            directorySecurity.AddAccessRule(
                new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().Name,
                    FileSystemRights.FullControl,
                    InheritanceFlags.None,
                    PropagationFlags.NoPropagateInherit,
                    AccessControlType.Allow));

            directorySecurity.SetAccessRuleProtection(true, false);

            rootDirectory.SetAccessControl(directorySecurity);
        }

        public static void SetFileWritable(string fileName)
        {
            var rootFile = new FileInfo(fileName);
            var fileSecurity = rootFile.GetAccessControl();

            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var adminitrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            fileSecurity.AddAccessRule(
                new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.FullControl,
                    InheritanceFlags.None,
                    PropagationFlags.NoPropagateInherit,
                    AccessControlType.Allow));

            fileSecurity.AddAccessRule(
                new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().Name,
                    FileSystemRights.FullControl,
                    InheritanceFlags.None,
                    PropagationFlags.NoPropagateInherit,
                    AccessControlType.Allow));

            fileSecurity.SetAccessRuleProtection(true, false);

            rootFile.SetAccessControl(fileSecurity);
        }

        public static bool FileDelete(string fileName)
        {
            if (IsFileUsed(fileName))
                return false;

            try
            {
                File.Delete(fileName);
                return true;
            }
            catch { return false; }
        }

        public static string MakeValidFileName(this string name)
        {
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(name, invalidRegStr, "_");
        }
    }
}
