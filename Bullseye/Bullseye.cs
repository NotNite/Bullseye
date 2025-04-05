using System.Reflection;
using Bullseye.Util;
using Serilog;

namespace Bullseye;

public class Bullseye : IDisposable {
    public const string GitHubRepository = "https://github.com/NotNite/Bullseye";
    public static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
    public static readonly Version? Version = Assembly.GetName().Version;
    public static readonly string GameDirectory = Path.GetDirectoryName(Environment.ProcessPath!)!;
    public static readonly string BullseyeDirectory =
        Environment.GetEnvironmentVariable("BULLSEYE_FOLDER_OVERRIDE") ??
        Path.Combine(GameDirectory, "Bullseye");

    private readonly Config config;
    private readonly Interop interop;
    private readonly Hooks hooks;

    public Bullseye(Config config) {
        this.EnsureFasm().Wait();

        this.config = config;
        this.interop = new Interop();
        this.hooks = new Hooks(this.interop);
    }

    // Reloaded needs FASM loaded to function, but we just ship the single d3d9.dll, so download it if we need
    private async Task EnsureFasm() {
        const string url =
            "https://github.com/Reloaded-Project/Reloaded.Assembler/raw/181896f3ddc8a2cf4c916de9e05566e59f74d26b/Source/Reloaded.Assembler/FASM.DLL";

        // a little sad we can't move it into the Bullseye folder :(
        var filePath = Path.Combine(GameDirectory, "FASM.DLL");
        if (!File.Exists(filePath)) {
            Log.Debug("FASM not present, downloading...");

            using var client = new HttpClient();
            var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            await using var outputStream = File.OpenWrite(filePath);
            await resp.Content.CopyToAsync(outputStream);

            Log.Debug("Downloaded FASM!");
        }
    }

    public void Dispose() {
        this.hooks.Dispose();
        this.interop.Dispose();
        // We don't technically own this config instance (it was passed to us by Entrypoint) but w/e
        this.config.Dispose();
    }
}
