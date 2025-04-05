using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Memory;

namespace Bullseye.Util;

public static unsafe class MemoryUtils {
    public static void Unprotect(nint memoryAddress, int size, Action action) {
        PInvoke.VirtualProtect((void*) memoryAddress, (nuint) size,
            PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE,
            out var oldProtect);
        action();
        PInvoke.VirtualProtect((void*) memoryAddress, (nuint) size, oldProtect, out _);
    }

    public static byte[] ReadRaw(nint memoryAddress, int length) {
        var value = new byte[length];
        Marshal.Copy(memoryAddress, value, 0, value.Length);
        return value;
    }

    public static void WriteRaw(nint memoryAddress, byte[] value) {
        Marshal.Copy(value, 0, memoryAddress, value.Length);
    }
}
