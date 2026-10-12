using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Options;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Dprint.Plugins.Roslyn.Configuration;

/// <summary>
/// Generates the JSON schema of the plugin's configuration, which is published with each release.
/// </summary>
[TestFixture]
public class SchemaTests
{
    private const string UpdateEnvVarName = "UPDATE";
    private static readonly string[] _languagePrefixes = ["csharp.", "visualBasic."];

    [Test]
    public void Schema_Generated_SameAsFile()
    {
        var filePath = Path.Combine(GetRepositoryRootDir(), "deployment", "schema.json");
        var expected = GenerateSchema();

        if (Environment.GetEnvironmentVariable(UpdateEnvVarName) == "1")
            File.WriteAllText(filePath, expected);

        var actual = File.Exists(filePath) ? File.ReadAllText(filePath).ReplaceLineEndings("\n") : "";
        Assert.That(actual, Is.EqualTo(expected), $"The schema is out of date. Run the tests with the {UpdateEnvVarName}=1 environment variable to update it.");
    }

    private static string GenerateSchema()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", "http://json-schema.org/draft-07/schema#");
            // the version is filled in when releasing
            writer.WriteString("$id", "https://plugins.dprint.dev/dprint/dprint-plugin-roslyn/0.0.0/schema.json");
            writer.WriteString("type", "object");
            WriteDefinitions(writer);
            WriteProperties(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    private static void WriteDefinitions(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("definitions");

        writer.WriteStartObject("indentWidth");
        writer.WriteString("description", "The number of characters for an indent.");
        writer.WriteString("type", "number");
        writer.WriteNumber("default", 4);
        writer.WriteEndObject();

        writer.WriteStartObject("useTabs");
        writer.WriteString("description", "Whether to use tabs (true) or spaces (false).");
        writer.WriteString("type", "boolean");
        writer.WriteBoolean("default", false);
        writer.WriteEndObject();

        writer.WriteStartObject("newLineKind");
        writer.WriteString("description", "The kind of newline to use.");
        writer.WriteString("type", "string");
        writer.WriteString("default", "auto");
        writer.WriteStartArray("oneOf");
        WriteConst(writer, "auto", "For each file, uses the newline kind found at the end of the last line.");
        WriteConst(writer, "crlf", "Uses carriage return, line feed.");
        WriteConst(writer, "lf", "Uses line feed.");
        WriteConst(writer, "system", "Uses line feed, because the plugin is unable to see the system standard.");
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteProperties(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("properties");

        writer.WriteStartObject("locked");
        writer.WriteString("description", "Whether the configuration is not allowed to be overriden or extended.");
        writer.WriteString("type", "boolean");
        writer.WriteEndObject();

        foreach (var prefix in _languagePrefixes.Prepend(""))
        {
            foreach (var name in new[] { "indentWidth", "useTabs", "newLineKind" })
            {
                writer.WriteStartObject(prefix + name);
                writer.WriteString("$ref", $"#/definitions/{name}");
                writer.WriteEndObject();
            }
        }

        foreach (var (prefix, languageName) in new[] { ("csharp.", "C#"), ("visualBasic.", "Visual Basic") })
        {
            writer.WriteStartObject(prefix + "enabled");
            writer.WriteString("description", $"Whether to format {languageName} files.");
            writer.WriteString("type", "boolean");
            writer.WriteBoolean("default", true);
            writer.WriteEndObject();
        }

        var optionProperties = typeof(CSharpFormattingOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(property => property.Name, StringComparer.Ordinal);
        foreach (var property in optionProperties)
            WriteOption(writer, property);

        writer.WriteEndObject();
    }

    private static void WriteOption(Utf8JsonWriter writer, PropertyInfo property)
    {
        var option = (IOption)property.GetValue(null)!;
        writer.WriteStartObject("csharp." + ToCamelCase(property.Name));
        writer.WriteString("description", $"Sets the {nameof(CSharpFormattingOptions)}.{property.Name} option of Roslyn.");

        if (option.Type == typeof(bool))
        {
            writer.WriteString("type", "boolean");
            writer.WriteBoolean("default", (bool)option.DefaultValue!);
        }
        else if (option.Type == typeof(int))
        {
            writer.WriteString("type", "number");
            writer.WriteNumber("default", (int)option.DefaultValue!);
        }
        else if (option.Type.IsEnum)
        {
            writer.WriteString("type", "string");
            writer.WriteString("default", ToCamelCase(option.DefaultValue!.ToString()!));
            writer.WriteStartArray("enum");
            foreach (var name in Enum.GetNames(option.Type))
                writer.WriteStringValue(ToCamelCase(name));
            writer.WriteEndArray();
        }
        else
        {
            throw new NotSupportedException($"Unhandled type of option {property.Name}: {option.Type}");
        }

        writer.WriteEndObject();
    }

    private static void WriteConst(Utf8JsonWriter writer, string value, string description)
    {
        writer.WriteStartObject();
        writer.WriteString("const", value);
        writer.WriteString("description", description);
        writer.WriteEndObject();
    }

    private static string ToCamelCase(string name) => name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);

    private static string GetRepositoryRootDir()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DprintPluginRoslyn.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Could not find the root directory of the repository.");
    }
}
