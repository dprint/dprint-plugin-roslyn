using System;

namespace Dprint.Plugins.Roslyn.Serialization;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class JsonSerializer
{
    public T Deserialize<T>(byte[] jsonData)
    {
        var jsonText = Encoding.UTF8.GetString(jsonData);
        return System.Text.Json.JsonSerializer.Deserialize<T>(jsonText, GetSettings()) ?? throw new Exception("Error deserializing JSON.");
    }

    public byte[] Serialize<T>(T obj)
    {
        var jsonText = System.Text.Json.JsonSerializer.Serialize(obj, GetSettings()) ?? throw new Exception("Error serializing to JSON.");
        return Encoding.UTF8.GetBytes(jsonText);
    }

    private JsonSerializerOptions GetSettings()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new ObjectToInferredTypesConverter(),
            }
        };
    }
    
    // Match Newtonsoft Behaviour
    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft?pivots=dotnet-8-0#deserialization-of-object-properties
    private class ObjectToInferredTypesConverter : JsonConverter<object>
    {
        public override object Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
            JsonTokenType.String => reader.GetString()!,
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };

        public override void Write(
            Utf8JsonWriter writer,
            object objectToWrite,
            JsonSerializerOptions options) =>
            System.Text.Json.JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
    }
}
