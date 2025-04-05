using Bullseye.Util;
using Reloaded.Hooks.Definitions.X86;

namespace Bullseye;

public class Hooks : IDisposable {
    private const string WndProcSignature = "56 8B 74 24 ?? 57 8B 7C 24 ?? 83 FE 01";

    [Function(CallingConventions.Stdcall)]
    private delegate nint WndProcDelegate(uint hwnd, uint msg, nint param1, nint param2);

    private readonly ITrackedHook<WndProcDelegate> wndProcHook;

    public Hooks(Interop interop) {
        var wndProcAddr = interop.ScanText([WndProcSignature]);
        this.wndProcHook = interop.CreateHook<WndProcDelegate>(wndProcAddr, this.WndProcDetour);
        this.wndProcHook.Enable();
    }

    public void Dispose() {
        this.wndProcHook.Dispose();
    }

    private nint WndProcDetour(uint hwnd, uint msg, nint param1, nint param2) {
        Console.WriteLine($"WndProc {hwnd:X8} {msg:X8} {param1:X8} {param2:X8}");
        return this.wndProcHook.Original(hwnd, msg, param1, param2);
    }
}
