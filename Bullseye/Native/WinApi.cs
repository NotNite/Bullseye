using System.Runtime.InteropServices;

namespace Bullseye.Native;

public partial class WinApi {
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllocConsole();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(int pid);

    // Specify the exact path here to not infinite loop import ourselves, lol
    [LibraryImport("C:/Windows/System32/d3d9.dll")]
    public static partial nint Direct3DCreate9(uint version);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int MessageBoxW(nint hwnd, string text, string caption, uint type);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualProtect(nint address, nint size, uint newProtect, out uint oldProtect);
}
