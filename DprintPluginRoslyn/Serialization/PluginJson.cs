using Dprint.Plugins.Roslyn.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Dprint.Plugins.Roslyn.Serialization;

/// <summary>
/// Reads and writes the JSON exchanged with dprint.
/// </summary>
/// <remarks>
/// This is done by hand because reflection based serialization doesn't work after trimming.
/// </remarks>
public static class PluginJson
{
    public static byte[] SerializePluginInfo(string version)
    {
        return Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("name", "dprint-plugin-roslyn");
            writer.WriteString("version", version);
            writer.WriteString("configKey", "roslyn");
            writer.WriteString("helpUrl", "https://dprint.dev/plugins/roslyn");
            writer.WriteString("configSchemaUrl", "");
            writer.WriteString("updateUrl", "https://plugins.dprint.dev/dprint/dprint-plugin-roslyn/latest.json");
            writer.WriteEndObject();
        });
    }

    public static (GlobalConfiguration Global, Dictionary<string, object> Plugin) DeserializeRegisterConfig(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var globalConfig = new GlobalConfiguration();
        if (root.TryGetProperty("global", out var global) && global.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in global.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Null)
                    continue;
                switch (property.Name)
                {
                    case "lineWidth":
                        globalConfig.LineWidth = property.Value.GetInt32();
                        break;
                    case "useTabs":
                        globalConfig.UseTabs = property.Value.GetBoolean();
                        break;
                    case "indentWidth":
                        globalConfig.IndentWidth = property.Value.GetInt32();
                        break;
                    case "newLineKind":
                        globalConfig.NewLineKind = property.Value.GetString();
                        break;
                }
            }
        }
        var pluginConfig = root.TryGetProperty("plugin", out var plugin) ? ToConfig(plugin) : new();
        return (globalConfig, pluginConfig);
    }

    public static Dictionary<string, object> DeserializeConfig(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        return ToConfig(document.RootElement);
    }

    public static byte[] SerializeDiagnostics(IEnumerable<ConfigurationDiagnostic> diagnostics)
    {
        return Serialize(writer =>
        {
            writer.WriteStartArray();
            foreach (var diagnostic in diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("propertyName", diagnostic.PropertyName);
                writer.WriteString("message", diagnostic.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        });
    }

    public static byte[] SerializeConfig(IReadOnlyDictionary<string, object> config)
    {
        return Serialize(writer =>
        {
            writer.WriteStartObject();
            foreach (var (key, value) in config)
            {
                writer.WritePropertyName(key);
                switch (value)
                {
                    case bool boolValue:
                        writer.WriteBooleanValue(boolValue);
                        break;
                    case int intValue:
                        writer.WriteNumberValue(intValue);
                        break;
                    case Enum enumValue:
                        // serialize the same way it's specified in the configuration
                        var name = enumValue.ToString();
                        writer.WriteStringValue(char.ToLowerInvariant(name[0]) + name.Substring(1));
                        break;
                    default:
                        writer.WriteStringValue(value.ToString());
                        break;
                }
            }
            writer.WriteEndObject();
        });
    }

    private static Dictionary<string, object> ToConfig(JsonElement element)
    {
        var config = new Dictionary<string, object>();
        if (element.ValueKind != JsonValueKind.Object)
            return config;
        foreach (var property in element.EnumerateObject())
        {
            var value = ToConfigValue(property.Value);
            if (value is not null)
                config[property.Name] = value;
        }
        return config;
    }

    private static object? ToConfigValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                return element.GetDouble();
            case JsonValueKind.Null:
                return null;
            default:
                // objects and arrays aren't supported, so let these get a diagnostic
                return element.GetRawText();
        }
    }

    private static byte[] Serialize(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }
        return stream.ToArray();
    }
}
