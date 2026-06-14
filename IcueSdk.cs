using System.Runtime.InteropServices;

namespace ForzaRGB;

public static class CorsairConst
{
    public const int STRING_SIZE_M = 128;
}

[StructLayout(LayoutKind.Sequential)]
public struct CorsairLedColor
{
    public uint id;
    public byte r, g, b, a;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CorsairDeviceInfo
{
    public int type;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CorsairConst.STRING_SIZE_M)]
    public string id;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CorsairConst.STRING_SIZE_M)]
    public string serial;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CorsairConst.STRING_SIZE_M)]
    public string model;
    public int ledCount;
    public int channelCount;
}

[StructLayout(LayoutKind.Sequential)]
public struct CorsairLedPosition
{
    public uint   id;
    public double cx;
    public double cy;
}

[StructLayout(LayoutKind.Sequential)]
public struct CorsairDeviceFilter
{
    public int deviceTypeMask;
}

public enum CorsairDeviceType : int
{
    Unknown         = 0x0000,
    Mouse           = 0x0001,
    Keyboard        = 0x0002,
    Headset         = 0x0004,
    MouseMat        = 0x0008,
    HeadsetStand    = 0x0010,
    CommanderPro    = 0x0020,
    LightingNodePro = 0x0040,
    MemoryModule    = 0x0080,
    Cooler          = 0x0100,
    Motherboard     = 0x0200,
    GraphicsCard    = 0x0400,
    Touchbar        = 0x0800,
    GameController  = 0x1000,
    All             = 0xFFFF,
}

// Delegate types matching iCUE SDK v4 function signatures
public delegate int  CorsairConnectDelegate(IntPtr onStateChanged, IntPtr context);
public delegate int  CorsairGetDevicesDelegate(ref CorsairDeviceFilter filter, int sizeMax, IntPtr devices, out int size);
public delegate int  CorsairGetLedPositionsDelegate([MarshalAs(UnmanagedType.LPStr)] string deviceId, int sizeMax, IntPtr ledPositions, out int size);
public delegate int  CorsairSetLedColorsDelegate([MarshalAs(UnmanagedType.LPStr)] string deviceId, int size, [In] CorsairLedColor[] ledColors);
public delegate int  CorsairDisconnectDelegate();

public static class CorsairApi
{
    public static CorsairConnectDelegate        CorsairConnect        = null!;
    public static CorsairGetDevicesDelegate     CorsairGetDevices     = null!;
    public static CorsairGetLedPositionsDelegate CorsairGetLedPositions = null!;
    public static CorsairSetLedColorsDelegate   CorsairSetLedColors   = null!;
    public static CorsairDisconnectDelegate     CorsairDisconnect     = null!;

    /// <summary>
    /// Searches for iCUE SDK DLL in the exe directory.
    /// Accepts any filename containing "iCUESDK" (case-insensitive).
    /// </summary>
    public static string? FindAndLoad()
    {
        string exeDir = AppContext.BaseDirectory;

        // Accept any DLL with "iCUESDK" in the name
        string? dllPath = Directory.GetFiles(exeDir, "*.dll")
            .FirstOrDefault(f => Path.GetFileName(f).Contains("iCUESDK", StringComparison.OrdinalIgnoreCase));

        if (dllPath == null) return null;

        try
        {
            IntPtr handle = NativeLibrary.Load(dllPath);

            CorsairConnect         = Marshal.GetDelegateForFunctionPointer<CorsairConnectDelegate>(NativeLibrary.GetExport(handle, "CorsairConnect"));
            CorsairGetDevices      = Marshal.GetDelegateForFunctionPointer<CorsairGetDevicesDelegate>(NativeLibrary.GetExport(handle, "CorsairGetDevices"));
            CorsairGetLedPositions = Marshal.GetDelegateForFunctionPointer<CorsairGetLedPositionsDelegate>(NativeLibrary.GetExport(handle, "CorsairGetLedPositions"));
            CorsairSetLedColors    = Marshal.GetDelegateForFunctionPointer<CorsairSetLedColorsDelegate>(NativeLibrary.GetExport(handle, "CorsairSetLedColors"));
            CorsairDisconnect      = Marshal.GetDelegateForFunctionPointer<CorsairDisconnectDelegate>(NativeLibrary.GetExport(handle, "CorsairDisconnect"));

            return Path.GetFileName(dllPath);
        }
        catch
        {
            return null;
        }
    }
}
