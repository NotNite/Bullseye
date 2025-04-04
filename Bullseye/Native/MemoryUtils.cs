using System.Runtime.InteropServices;

namespace Bullseye.Native;

public static class MemoryUtils {
    public static void Unprotect(nint memoryAddress, int size, Action action) {
        WinApi.VirtualProtect(memoryAddress, size, 0x40, out var oldProtect);
        action();
        WinApi.VirtualProtect(memoryAddress, size, oldProtect, out _);
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
