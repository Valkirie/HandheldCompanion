using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HandheldCompanion;

public class InpOut : IDisposable
{
    public delegate byte DlPortReadPortUcharDelegate(ushort port);

    public delegate byte DlPortWritePortUcharDelegate(ushort port, byte value);

    public delegate IntPtr MapPhysToLinDelegate(IntPtr pbPhysAddr, uint dwPhysSize, out IntPtr pPhysicalMemoryHandle);

    public delegate bool UnmapPhysicalMemoryDelegate(IntPtr PhysicalMemoryHandle, IntPtr pbLinAddr);
    // Author: Kamil Trzciński, 2022 (https://github.com/ayufan/steam-deck-tools/)

    public const string LibraryName = "inpoutx64.dll";
    public DlPortReadPortUcharDelegate DlPortReadPortUchar;
    public DlPortWritePortUcharDelegate DlPortWritePortUchar;

    private IntPtr libraryHandle;
    public MapPhysToLinDelegate MapPhysToLin;
    public UnmapPhysicalMemoryDelegate UnmapPhysicalMemory;

    public InpOut()
    {
        var fileName = Path.GetFullPath(Path.Combine("Resources", LibraryName));
        libraryHandle = LoadLibrary(fileName);

        try
        {
            var addr = GetProcAddress(libraryHandle, "MapPhysToLin");
            if (addr == IntPtr.Zero)
                throw new ArgumentException("Missing MapPhysToLin");
            MapPhysToLin = Marshal.GetDelegateForFunctionPointer<MapPhysToLinDelegate>(addr);

            addr = GetProcAddress(libraryHandle, "UnmapPhysicalMemory");
            if (addr == IntPtr.Zero)
                throw new ArgumentException("Missing UnmapPhysicalMemory");
            UnmapPhysicalMemory = Marshal.GetDelegateForFunctionPointer<UnmapPhysicalMemoryDelegate>(addr);

            addr = GetProcAddress(libraryHandle, "UnmapPhysicalMemory");
            if (addr == IntPtr.Zero)
                throw new ArgumentException("Missing UnmapPhysicalMemory");
            UnmapPhysicalMemory = Marshal.GetDelegateForFunctionPointer<UnmapPhysicalMemoryDelegate>(addr);

            addr = GetProcAddress(libraryHandle, "DlPortReadPortUchar");
            if (addr == IntPtr.Zero)
                throw new ArgumentException("Missing DlPortReadPortUchar");
            DlPortReadPortUchar = Marshal.GetDelegateForFunctionPointer<DlPortReadPortUcharDelegate>(addr);

            addr = GetProcAddress(libraryHandle, "DlPortWritePortUchar");
            if (addr == IntPtr.Zero)
                throw new ArgumentException("Missing DlPortWritePortUchar");
            DlPortWritePortUchar = Marshal.GetDelegateForFunctionPointer<DlPortWritePortUcharDelegate>(addr);
        }
        catch
        {
            FreeLibrary(libraryHandle);
            libraryHandle = IntPtr.Zero;
            throw;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FreeLibrary(libraryHandle);
        libraryHandle = IntPtr.Zero;
    }

    ~InpOut()
    {
        Dispose();
    }

    public byte[]? ReadMemory(IntPtr baseAddress, uint size)
    {
        var pdwLinAddr = MapPhysToLin(baseAddress, size, out var pPhysicalMemoryHandle);
        if (pdwLinAddr != IntPtr.Zero)
        {
            var bytes = new byte[size];
            Marshal.Copy(pdwLinAddr, bytes, 0, bytes.Length);
            UnmapPhysicalMemory(pPhysicalMemoryHandle, pdwLinAddr);
            return bytes;
        }

        return null;
    }

    public bool WriteMemory(IntPtr baseAddress, byte[] data)
    {
        var pdwLinAddr = MapPhysToLin(baseAddress, (uint)data.Length, out var pPhysicalMemoryHandle);
        if (pdwLinAddr != IntPtr.Zero)
        {
            Marshal.Copy(data, 0, pdwLinAddr, data.Length);
            UnmapPhysicalMemory(pPhysicalMemoryHandle, pdwLinAddr);
            return true;
        }

        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string methodName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);
}