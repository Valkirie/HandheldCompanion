using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HandheldCompanion.Utils
{
    static class FileUtils
    {
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

                using (var fs = new FileStream(fileName, FileMode.Create))
                {
                    return fs.CanWrite;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                using (var fs = File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
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
                        return true;
                    }
                }
                catch (IOException)
                {
                    // If an exception is thrown, the file is not being used
                    return false;
                }
            }
            else
            {
                // If the file does not exist, it is not being used
                return false;
            }
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
    }
}
