using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace SlimA2A;

internal static class ProtoStructJson
{
    private static readonly JsonParser Parser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));
    private static readonly JsonFormatter Formatter = new(JsonFormatter.Settings.Default.WithFormatDefaultValues(false));

    public static Struct? ToStruct(Dictionary<string, JsonElement>? map)
    {
        if (map is null || map.Count == 0)
            return null;
        var json = JsonSerializer.Serialize(map);
        return Parser.Parse<Struct>(json);
    }

    public static Dictionary<string, JsonElement>? FromStruct(Struct? s)
    {
        if (s is null || s.Fields.Count == 0)
            return null;
        var json = Formatter.Format(s);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
    }

    public static Struct JsonElementToStruct(JsonElement element)
    {
        var json = element.GetRawText();
        return Parser.Parse<Struct>(json);
    }

    public static JsonElement StructToJsonElement(Struct s)
    {
        var json = Formatter.Format(s);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
