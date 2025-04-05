using System.Text.Json.Serialization;
using Serilog.Events;

namespace Bullseye.Util;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true, Converters = [
    typeof(JsonStringEnumConverter<LogEventLevel>)
])]
[JsonSerializable(typeof(Config))]
public partial class JsonContext : JsonSerializerContext;
