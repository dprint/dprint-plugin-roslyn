using NUnit.Framework;
using System.Linq;
using System.Text;

namespace Dprint.Plugins.Roslyn;

[TestFixture]
public class PluginTests
{
    [Test]
    public void GetPluginInfo_Gets()
    {
        var json = Encoding.UTF8.GetString(new Plugin().GetPluginInfo());
        Assert.That(json, Does.StartWith("""{"name":"dprint-plugin-roslyn","version":"""));
        Assert.That(json, Does.Match("""
            "version":"\d+\.\d+\.\d+"
            """.Trim()));
        Assert.That(json, Does.Contain("""
            "configKey":"roslyn"
            """.Trim()));
        Assert.That(json, Does.Match("""
            "configSchemaUrl":"https://plugins\.dprint\.dev/dprint/dprint-plugin-roslyn/\d+\.\d+\.\d+/schema\.json"
            """.Trim()));
    }

    [Test]
    public void GetLicenseText_Gets()
    {
        Assert.That(new Plugin().GetLicenseText(), Does.StartWith("The MIT License (MIT)"));
    }

    [Test]
    public void RegisterConfig_GlobalAndPluginConfig_Resolves()
    {
        var plugin = new Plugin();
        plugin.RegisterConfig(1, ToBytes("""
            {
              "global": { "lineWidth": 80, "indentWidth": 2, "useTabs": null, "newLineKind": "lf" },
              "plugin": { "csharp.indentBlock": false, "csharp.labelPositioning": "noIndent", "visualBasic.indentWidth": 3 }
            }
            """));

        Assert.That(ToString(plugin.GetConfigDiagnostics(1)), Is.EqualTo("[]"));
        var resolvedConfig = ToString(plugin.GetResolvedConfig(1));
        Assert.That(resolvedConfig, Does.Contain("""
            "csharp.indentationSize":2
            """.Trim()));
        Assert.That(resolvedConfig, Does.Contain("""
            "csharp.newLine":"\n"
            """.Trim()));
        Assert.That(resolvedConfig, Does.Contain("""
            "csharp.indentBlock":false
            """.Trim()));
        Assert.That(resolvedConfig, Does.Contain("""
            "csharp.labelPositioning":"noIndent"
            """.Trim()));
        Assert.That(resolvedConfig, Does.Contain("""
            "visualBasic.indentationSize":3
            """.Trim()));
    }

    [Test]
    public void GetConfigDiagnostics_UnknownProperty_Diagnostic()
    {
        var plugin = new Plugin();
        plugin.RegisterConfig(1, ToBytes("""{ "global": {}, "plugin": { "unknownProp": [1] } }"""));

        Assert.That(
            ToString(plugin.GetConfigDiagnostics(1)),
            Is.EqualTo("""[{"propertyName":"unknownProp","message":"Unknown configuration property name."}]""")
        );
    }

    [Test]
    public void GetConfigDiagnostics_ReleasedConfig_Throws()
    {
        var plugin = new Plugin();
        plugin.RegisterConfig(1, ToBytes("""{ "global": {}, "plugin": {} }"""));
        plugin.ReleaseConfig(1);

        Assert.That(() => plugin.GetConfigDiagnostics(1), Throws.Exception.With.Message.StartsWith("Could not find configuration id: 1"));
    }

    [Test]
    public void Format_CSharpAndVisualBasic_Formats()
    {
        var plugin = CreatePluginWithDefaultConfig();

        Assert.That(Format(plugin, "file.cs", "namespace Test    {   }\n"), Is.EqualTo("namespace Test { }\n"));
        Assert.That(Format(plugin, "file.cs", "namespace Test { }\n"), Is.Null);
        Assert.That(Format(plugin, "file.vb", "Namespace    Test\nEnd  Namespace\n"), Is.EqualTo("Namespace Test\nEnd Namespace\n"));
    }

    [Test]
    public void Format_OverrideConfig_UsesOverride()
    {
        var plugin = CreatePluginWithDefaultConfig();
        var text = "class Test\n{\n    void Method() { }\n}\n";

        var result = plugin.Format(1, "file.cs", ToBytes(text), null, ToBytes("""{ "csharp.indentWidth": 2 }"""));

        Assert.That(ToString(result!), Is.EqualTo("class Test\n{\n  void Method() { }\n}\n"));
        // the override shouldn't be remembered
        Assert.That(Format(plugin, "file.cs", text), Is.Null);
    }

    [Test]
    public void Format_Range_OnlyFormatsRange()
    {
        var plugin = CreatePluginWithDefaultConfig();
        // multi-byte character before the range to ensure byte indexes are converted to char indexes
        var text = "// é\nnamespace Test    {\n    class   Test{}\n   }\n";
        var start = Encoding.UTF8.GetByteCount("// é\nnamespace Test    {\n    ");
        var end = start + Encoding.UTF8.GetByteCount("class   Test{}");

        var result = plugin.Format(1, "file.cs", ToBytes(text), (start, end), null);

        Assert.That(ToString(result!), Is.EqualTo("// é\nnamespace Test    {\n    class Test { }\n   }\n"));
    }

    [Test]
    public void Format_WithBom_PreservesBom()
    {
        var plugin = CreatePluginWithDefaultConfig();
        var bom = Encoding.UTF8.GetPreamble();

        var result = plugin.Format(1, "file.cs", bom.Concat(ToBytes("namespace Test    {   }\n")).ToArray(), null, null);

        Assert.That(result, Is.EqualTo(bom.Concat(ToBytes("namespace Test { }\n")).ToArray()));
        Assert.That(plugin.Format(1, "file.cs", result!, null, null), Is.Null);
    }

    [Test]
    public void Format_RangeWithBom_OnlyFormatsRange()
    {
        var plugin = CreatePluginWithDefaultConfig();
        var bom = Encoding.UTF8.GetPreamble();
        var prefix = "namespace Test    {\n    ";
        var start = bom.Length + Encoding.UTF8.GetByteCount(prefix);
        var end = start + Encoding.UTF8.GetByteCount("class   Test{}");

        var result = plugin.Format(1, "file.cs", bom.Concat(ToBytes(prefix + "class   Test{}\n   }\n")).ToArray(), (start, end), null);

        Assert.That(result, Is.EqualTo(bom.Concat(ToBytes(prefix + "class Test { }\n   }\n")).ToArray()));
    }

    [Test]
    public void Format_Utf16_KeepsEncoding()
    {
        var plugin = CreatePluginWithDefaultConfig();
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);

        var result = plugin.Format(1, "file.cs", encoding.GetPreamble().Concat(encoding.GetBytes("namespace Test    {   }\n")).ToArray(), null, null);

        Assert.That(result, Is.EqualTo(encoding.GetPreamble().Concat(encoding.GetBytes("namespace Test { }\n")).ToArray()));
    }

    [Test]
    public void Format_UnknownExtension_Throws()
    {
        var plugin = CreatePluginWithDefaultConfig();

        Assert.That(() => Format(plugin, "file.txt", "text"), Throws.Exception.With.Message.EqualTo("Could not find formatter for file path: file.txt"));
    }

    private static Plugin CreatePluginWithDefaultConfig()
    {
        var plugin = new Plugin();
        plugin.RegisterConfig(1, ToBytes("""{ "global": {}, "plugin": { "newLineKind": "lf" } }"""));
        return plugin;
    }

    private static string? Format(Plugin plugin, string filePath, string text)
    {
        var result = plugin.Format(1, filePath, ToBytes(text), null, null);
        return result is null ? null : ToString(result);
    }

    private static byte[] ToBytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string ToString(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
