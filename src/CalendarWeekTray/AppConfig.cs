using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalendarWeekTray;

internal enum Language { Auto, De, En }

internal enum Theme { Auto, Light, Dark }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AppConfig
{
    [JsonConverter(typeof(JsonStringEnumConverter<Language>))]
    public Language Language { get; init; } = Language.Auto;

    [JsonConverter(typeof(JsonStringEnumConverter<Theme>))]
    public Theme Theme { get; init; } = Theme.Auto;
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,
                             ReadCommentHandling = JsonCommentHandling.Skip,
                             AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
