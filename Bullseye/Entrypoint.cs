using System.Reflection;
using System.Runtime.InteropServices;
using Bullseye.Native;

namespace Bullseye;

public static class Entrypoint {
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    private const uint DllProcessDetach = 0;
    private const uint DllProcessAttach = 1;

    private static Bullseye? Instance;
    private static bool InitCalled;

    [UnmanagedCallersOnly(EntryPoint = "Direct3DCreate9")]
    private static nint Direct3DCreate9(uint version) {
        // Game calls Direct3DCreate9 twice for some reason?
        // Use a separate bool in case the instance fails to load
        if (!InitCalled) {
            InitCalled = true;

            try {
                Instance = new Bullseye();
            } catch (Exception e) {
                Console.WriteLine(e);
                WinApi.MessageBoxW(nint.Zero, $"Failed to load:\n{e}", "Bullseye", 0);
            }
        }

        return WinApi.Direct3DCreate9(version);
    }

    [UnmanagedCallersOnly(EntryPoint = "DllMain")]
    public static bool DllMain(nint module, uint reasonForCall, nint reserved) {
        switch (reasonForCall) {
            case DllProcessAttach: {
                try {
                    DllMainInit();
                    Console.WriteLine($"This is Bullseye {Version}");
                } catch (Exception e) {
                    Console.WriteLine(e);
                    WinApi.MessageBoxW(nint.Zero, $"Failed to early load\n:{e}", "Bullseye", 0);
                }
                break;
            }

            case DllProcessDetach when reserved == 0 && Instance != null: {
                try {
                    Instance.Dispose();
                    Instance = null;
                } catch (Exception e) {
                    Console.WriteLine(e);
                    WinApi.MessageBoxW(nint.Zero, $"Failed to unload\n:{e}", "Bullseye", 0);
                }
                break;
            }
        }

        return true;
    }

    // Init inside DllMain, before the main instance is created
    // Be careful about loader lock
    private static void DllMainInit() {
        WinApi.AllocConsole();
        WinApi.AttachConsole(-1);

        // Fix Console
        var stdout = new StreamWriter(Console.OpenStandardOutput()) {AutoFlush = true};
        Console.SetOut(stdout);
        var stderr = new StreamWriter(Console.OpenStandardError()) {AutoFlush = true};
        Console.SetError(stderr);
    }
}
