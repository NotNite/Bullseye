using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bullseye.Util;
using Serilog;
using Serilog.Events;

namespace Bullseye;

public class Config : IDisposable {
    [JsonIgnore]
    private static string ConfigPath => Path.Combine(
        Bullseye.BullseyeDirectory,
        "config.json"
    );

    public LogEventLevel LogLevel = LogEventLevel.Information;
    public bool CreateConsoleWindow;

    public static Config Load() {
        Config config;
        if (!File.Exists(ConfigPath)) {
            config = new Config();
        } else {
            try {
                config = JsonSerializer.Deserialize<Config>(
                    File.ReadAllText(ConfigPath),
                    JsonContext.Default.Config)!;
            } catch (Exception e) {
                Log.Warning(e, "Failed to load config file - creating a new one");
                config = new Config();
            }
        }

        config.Fixup();
        config.Save();

        return config;
    }

    public void Save() {
        Log.Debug("Saving config");
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonContext.Default.Config));
    }

    private void Fixup() {
        // here if we need it
    }

    public void Dispose() {
        // If we edited the config file on disk, skip saving it so we don't overwrite it
        try {
            var newConfig = JsonNode.Parse(File.ReadAllText(ConfigPath));
            var oldConfig = JsonNode.Parse(JsonSerializer.Serialize(this, JsonContext.Default.Config));
            if (!JsonNode.DeepEquals(newConfig, oldConfig)) return;
        } catch {
            // ignored
        }

        this.Save();
    }
}
