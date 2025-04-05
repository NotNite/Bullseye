using System.Diagnostics;
using System.Reflection;
using Windows.Win32.Graphics.Direct3D9;
using Bullseye.Hooks;
using Bullseye.Util;
using Serilog;

namespace Bullseye;

public class Bullseye : IDisposable {
    public const string GitHubRepository = "https://github.com/NotNite/Bullseye";
    public static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
    public static readonly Version? Version = Assembly.GetName().Version;
    public static readonly ProcessModule Module = GetModule();

    public static readonly string GameDirectory = Path.GetDirectoryName(Environment.ProcessPath!)!;
    public static readonly string BullseyeDirectory =
        Environment.GetEnvironmentVariable("BULLSEYE_FOLDER_OVERRIDE") ??
        Path.Combine(GameDirectory, "Bullseye");

    private readonly Config config;
    private readonly Interop interop;

    private readonly ImGuiHooks? imgui;

    public unsafe Bullseye(Config config, IDirect3D9* d3d) {
        this.EnsureFasm();
        this.config = config;
        this.interop = new Interop();

        try {
            this.imgui = new ImGuiHooks(this.interop, this.config, d3d);
        } catch (Exception e) {
            Utils.ErrorWithMessageBox(e, "Failed to create ImGui. Things will break!");
        }
    }

    public void Dispose() {
        this.imgui?.Dispose();

        // We don't technically own this config instance (it was passed to us by Entrypoint) but w/e
        this.interop.Dispose();
        this.config.Dispose();

        GC.SuppressFinalize(this);
    }

    // Reloaded needs FASM loaded to function, but we just ship the single d3d9.dll, so extract it if we need
    private void EnsureFasm() {
        // a little sad we can't move it into the Bullseye folder :(
        const string filename = "FASM.DLL";
        var filePath = Path.Combine(GameDirectory, filename);
        if (!File.Exists(filePath)) {
            Log.Debug("Extracting FASM...");
            using var stream = Assembly.GetManifestResourceStream(filename);
            if (stream == null) throw new InvalidOperationException("Couldn't extract FASM");
            using var outputStream = File.OpenWrite(filePath);
            stream.CopyTo(outputStream);
        }
    }

    private static ProcessModule GetModule() {
        var process = Process.GetCurrentProcess();
        var gameDir = Path.GetFullPath(Path.GetDirectoryName(Environment.ProcessPath)!);
        const string filename = "d3d9.dll";

        for (var i = 0; i < process.Modules.Count; i++) {
            var module = process.Modules[i];
            var moduleDir = Path.GetFullPath(Path.GetDirectoryName(module.FileName)!);
            var moduleFile = Path.GetFileName(module.FileName);
            if (moduleDir == gameDir && moduleFile == filename) return module;
        }

        throw new Exception("Couldn't find own module?");
    }
}
