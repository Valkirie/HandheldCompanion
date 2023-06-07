using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ControllerCommon;

public class IniFile // revision 11
{
    private readonly string EXE = Assembly.GetExecutingAssembly().GetName().Name;
    private readonly string Path;

    public IniFile(string IniPath = null)
    {
        Path = new FileInfo(IniPath ?? EXE + ".ini").FullName;
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern bool WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal,
        int Size, string FilePath);

    public string Read(string Key, string Section = null)
    {
        var RetVal = new StringBuilder(255);
        GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
        return RetVal.ToString();
    }

    public bool Write(string Key, string Value, string Section = null)
    {
        return WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
    }

    public bool DeleteKey(string Key, string Section = null)
    {
        return Write(Key, null, Section ?? EXE);
    }

    public bool DeleteSection(string Section = null)
    {
        return Write(null, null, Section ?? EXE);
    }

    public bool KeyExists(string Key, string Section = null)
    {
        return Read(Key, Section).Length > 0;
    }
}