using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Direct3D9;
using Bullseye.Util;
using Serilog;

namespace Bullseye;

public static class Entrypoint {
    private const uint DllProcessDetach = 0;
    private const uint DllProcessAttach = 1;

    private static readonly Config Config = Config.Load();
    private static Bullseye? Instance;
    private static bool InitCalled;
    private static bool AttemptedInit;

    [UnmanagedCallersOnly(EntryPoint = "Direct3DCreate9")]
    private static unsafe IDirect3D9* Direct3DCreate9(uint version) {
        var d3d = PInvoke.Direct3DCreate9(version);

        // Game calls Direct3DCreate9 twice for some reason?
        if (!InitCalled) {
            InitCalled = true;
        } else if (!AttemptedInit) {
            // Use a separate bool in case the instance fails to load
            AttemptedInit = true;

            try {
                Log.Information("Initializing...");
                Instance = new Bullseye(Config, d3d);
            } catch (Exception e) {
                Utils.ErrorWithMessageBox(e, "Failed to load (Direct3DCreate9)");
            }
        }

        return d3d;
    }

    [UnmanagedCallersOnly(EntryPoint = "DllMain")]
    public static bool DllMain(nint module, uint reasonForCall, nint reserved) {
        switch (reasonForCall) {
            case DllProcessAttach: {
                try {
                    DllMainInit();
                } catch (Exception e) {
                    Utils.ErrorWithMessageBox(e, "Failed to load (DllMain)");
                }
                break;
            }

            case DllProcessDetach when reserved == 0 && Instance != null: {
                try {
                    Log.Information("Shutting down, goodbye!");
                    Instance.Dispose();
                    Instance = null;
                } catch (Exception e) {
                    Utils.ErrorWithMessageBox(e, "Failed to unload");
                }
                break;
            }
        }

        return true;
    }

    // Init inside DllMain, before the main instance is created
    // Be careful about loader lock
    private static void DllMainInit() {
        if (!Directory.Exists(Bullseye.BullseyeDirectory)) Directory.CreateDirectory(Bullseye.BullseyeDirectory);

        // DLL proxying
        NativeLibrary.SetDllImportResolver(Bullseye.Assembly, DllImportResolver);

        // Setup logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(Config.LogLevel)
            .WriteTo.File(Path.Combine(Bullseye.BullseyeDirectory, "Bullseye.log"))
            .WriteTo.Console()
            .CreateLogger();

        // Setup console
        if (Config.CreateConsoleWindow) {
            PInvoke.AllocConsole();
            PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);
        }

        var stdout = new StreamWriter(Console.OpenStandardOutput()) {AutoFlush = true};
        Console.SetOut(stdout);
        var stderr = new StreamWriter(Console.OpenStandardError()) {AutoFlush = true};
        Console.SetError(stderr);

        Log.Information("This is Bullseye {Version}, heya! - {GitHub}",
            Bullseye.Version, Bullseye.GitHubRepository);
    }

    private static nint DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
        // Refuse to load our own DLL when importing DirectX 9 functions, preventing a stack overflow
        if (libraryName == "d3d9.dll") libraryName = "C:/Windows/System32/d3d9.dll";
        return NativeLibrary.Load(libraryName, assembly, searchPath);
    }
}
