using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adnd.Server.Services;

public class SafeJsonElementConverter : JsonConverter<JsonElement>
{
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
            writer.WriteNullValue();
        else
            value.WriteTo(writer);
    }
}
