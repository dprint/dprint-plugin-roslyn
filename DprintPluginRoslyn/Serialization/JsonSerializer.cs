using System;

namespace Dprint.Plugins.Roslyn.Serialization;

using System.Text;
using System.Text.Json;

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
        };
    }
}
