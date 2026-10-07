using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dprint.Plugins.Roslyn.Wasm;

/// <summary>
/// Tests that run the plugin compiled to Wasm in dprint.
///
/// These only run when the <c>DPRINT_ROSLYN_WASM_PATH</c> environment variable is set to the
/// path of the built .wasm file. They use the dprint executable at <c>DPRINT_PATH</c> or
/// otherwise the one on the path.
/// </summary>
[TestFixture]
[Category("Wasm")]
public class WasmPluginTests
{
    private const string DefaultConfig = """
        "newLineKind": "lf"
        """;

    private string _wasmPath = "";
    private string _cacheDir = "";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var wasmPath = Environment.GetEnvironmentVariable("DPRINT_ROSLYN_WASM_PATH");
        if (string.IsNullOrEmpty(wasmPath))
            Assert.Ignore("Set the DPRINT_ROSLYN_WASM_PATH environment variable to run the Wasm tests.");
        _wasmPath = Path.GetFullPath(wasmPath);
        Assert.That(File.Exists(_wasmPath), $"Could not find Wasm file: {_wasmPath}");
        // share the cache directory so the plugin is only compiled once
        _cacheDir = Directory.CreateTempSubdirectory("dprint-plugin-roslyn-cache").FullName;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (_cacheDir.Length > 0)
            DprintDirectory.DeleteDirectory(_cacheDir);
    }

    [Test]
    public void Fmt_CSharpAndVisualBasic_Formats()
    {
        using var dir = CreateDirectory(DefaultConfig);
        dir.WriteFile("file.cs", "namespace Test    {   }\n");
        dir.WriteFile("formatted.cs", "namespace Test { }\n");
        dir.WriteFile("file.vb", "Namespace    Test\nClass A\nEnd Class\nEnd  Namespace\n");

        var check = dir.Run("check");
        Assert.That(check.ExitCode, Is.Not.Zero);
        Assert.That(check.Output, Does.Contain("Found 2 not formatted files."));

        Assert.That(dir.Run("fmt").ExitCode, Is.Zero);
        Assert.That(dir.ReadFile("file.cs"), Is.EqualTo("namespace Test { }\n"));
        Assert.That(dir.ReadFile("formatted.cs"), Is.EqualTo("namespace Test { }\n"));
        Assert.That(dir.ReadFile("file.vb"), Is.EqualTo("Namespace Test\n    Class A\n    End Class\nEnd Namespace\n"));
        Assert.That(dir.Run("check").ExitCode, Is.Zero);
    }

    [Test]
    public void Fmt_Config_UsesConfig()
    {
        using var dir = CreateDirectory("""
            "indentWidth": 2,
            "newLineKind": "lf",
            "roslyn": {
              "csharp.newLinesForBracesInTypes": false,
              "csharp.spaceAfterCast": true,
              "visualBasic.indentWidth": 3
            }
            """);
        dir.WriteFile("file.cs", "class Test\n{\nvoid Method()\n{\nvar x=(int)1.0;\n}\n}\n");
        dir.WriteFile("file.vb", "Namespace Test\nClass A\nEnd Class\nEnd Namespace\n");

        Assert.That(dir.Run("fmt").ExitCode, Is.Zero);
        Assert.That(dir.ReadFile("file.cs"), Is.EqualTo("class Test {\n  void Method()\n  {\n    var x = (int) 1.0;\n  }\n}\n"));
        Assert.That(dir.ReadFile("file.vb"), Is.EqualTo("Namespace Test\n   Class A\n   End Class\nEnd Namespace\n"));
    }

    [Test]
    public void Fmt_ConfigDiagnostics_Errors()
    {
        using var dir = CreateDirectory("""
            "roslyn": { "unknownProp": true, "csharp.indentBlock": "test" }
            """);
        dir.WriteFile("file.cs", "namespace Test    {   }\n");

        var result = dir.Run("fmt");
        Assert.That(result.ExitCode, Is.Not.Zero);
        Assert.That(result.Output, Does.Contain("[dprint-plugin-roslyn]: Unknown configuration property name. (unknownProp)"));
        Assert.That(result.Output, Does.Contain("(csharp.indentBlock)"));
        Assert.That(dir.ReadFile("file.cs"), Is.EqualTo("namespace Test    {   }\n"));
    }

    [Test]
    public void OutputResolvedConfig_Outputs()
    {
        using var dir = CreateDirectory("""
            "indentWidth": 2,
            "newLineKind": "crlf",
            "roslyn": { "csharp.labelPositioning": "noIndent", "visualBasic.indentWidth": 3 }
            """);

        var result = dir.Run("output-resolved-config");
        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(result.Output, Does.Contain("""
            "csharp.newLine": "\r\n"
            """.Trim()));
        Assert.That(result.Output, Does.Contain("\"csharp.indentationSize\": 2"));
        Assert.That(result.Output, Does.Contain("\"csharp.labelPositioning\": \"noIndent\""));
        Assert.That(result.Output, Does.Contain("\"visualBasic.indentationSize\": 3"));
    }

    [Test]
    public void License_OutputsLicense()
    {
        using var dir = CreateDirectory(DefaultConfig);

        var result = dir.Run("license");
        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(result.Output, Does.Contain("==== DPRINT-PLUGIN-ROSLYN LICENSE ===="));
        Assert.That(result.Output, Does.Contain(new Plugin().GetLicenseText().Split('\n')[0].Trim()));
    }

    [Test]
    public void Fmt_Encodings_KeepsEncoding()
    {
        using var dir = CreateDirectory(DefaultConfig);
        var text = "class Test    {   }\n// é\n";
        var expectedText = "class Test { }\n// é\n";
        var utf8Bom = Encoding.UTF8.GetPreamble();
        var utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        var utf16BigEndian = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        dir.WriteFile("bom.cs", [.. utf8Bom, .. Encoding.UTF8.GetBytes(text)]);
        dir.WriteFile("utf16.cs", [.. utf16.GetPreamble(), .. utf16.GetBytes(text)]);
        dir.WriteFile("utf16be.cs", [.. utf16BigEndian.GetPreamble(), .. utf16BigEndian.GetBytes(text)]);

        var result = dir.Run("fmt");
        Assert.That(result.ExitCode, Is.Zero, result.Output);
        Assert.That(dir.ReadFileBytes("bom.cs"), Is.EqualTo((byte[])[.. utf8Bom, .. Encoding.UTF8.GetBytes(expectedText)]));
        Assert.That(dir.ReadFileBytes("utf16.cs"), Is.EqualTo((byte[])[.. utf16.GetPreamble(), .. utf16.GetBytes(expectedText)]));
        Assert.That(dir.ReadFileBytes("utf16be.cs"), Is.EqualTo((byte[])[.. utf16BigEndian.GetPreamble(), .. utf16BigEndian.GetBytes(expectedText)]));
        Assert.That(dir.Run("check").ExitCode, Is.Zero);
    }

    [Test]
    public void Fmt_InvalidUtf8_Errors()
    {
        using var dir = CreateDirectory(DefaultConfig);
        dir.WriteFile("file.cs", [.. Encoding.UTF8.GetBytes("class Test    { } // "), 0xFF, (byte)'\n']);

        var result = dir.Run("fmt");
        Assert.That(result.ExitCode, Is.Not.Zero);
        Assert.That(result.Output, Does.Contain("Error formatting"));
    }

    [Test]
    public void EditorService_CanFormat_CanFormat()
    {
        using var dir = CreateDirectory(DefaultConfig);
        using var editorService = dir.StartEditorService();

        Assert.That(editorService.CanFormat(dir.GetPath("file.cs")), Is.True);
        Assert.That(editorService.CanFormat(dir.GetPath("file.vb")), Is.True);
    }

    [Test]
    public void EditorService_Format_Formats()
    {
        using var dir = CreateDirectory(DefaultConfig);
        using var editorService = dir.StartEditorService();

        Assert.That(editorService.FormatText(dir.GetPath("file.cs"), "namespace Test    {   }\n"), Is.EqualTo("namespace Test { }\n"));
        Assert.That(editorService.FormatText(dir.GetPath("file.cs"), "namespace Test { }\n"), Is.Null);
        Assert.That(editorService.FormatText(dir.GetPath("file.vb"), "Namespace    Test\nEnd  Namespace\n"), Is.EqualTo("Namespace Test\nEnd Namespace\n"));
        // not a file the plugin formats
        Assert.That(editorService.FormatText(dir.GetPath("file.txt"), "namespace Test    {   }\n"), Is.Null);
    }

    [Test]
    public void EditorService_FormatRange_OnlyFormatsRange()
    {
        using var dir = CreateDirectory(DefaultConfig);
        using var editorService = dir.StartEditorService();
        // multi-byte character before the range to ensure byte indexes are converted to char indexes
        var prefix = "// é\nnamespace Test    {\n    ";
        var range = "class   Test{}";
        var suffix = "\n    class   Other{}\n   }\n";
        var start = Encoding.UTF8.GetByteCount(prefix);
        var end = start + Encoding.UTF8.GetByteCount(range);

        var result = editorService.FormatText(dir.GetPath("file.cs"), prefix + range + suffix, (start, end));
        Assert.That(result, Is.EqualTo(prefix + "class Test { }" + suffix));

        // a range that's already formatted
        Assert.That(editorService.FormatText(dir.GetPath("file.cs"), result!, (start, start + "class Test { }".Length)), Is.Null);

        // visual basic
        var vbPrefix = "Namespace    Test\n";
        var vbRange = "Class    A";
        var vbSuffix = "\nEnd    Class\nEnd  Namespace\n";
        result = editorService.FormatText(dir.GetPath("file.vb"), vbPrefix + vbRange + vbSuffix, (vbPrefix.Length, vbPrefix.Length + vbRange.Length));
        Assert.That(result, Is.EqualTo(vbPrefix + "Class A" + vbSuffix));
    }

    [Test]
    public void EditorService_FormatRangeWithBom_OnlyFormatsRange()
    {
        using var dir = CreateDirectory(DefaultConfig);
        using var editorService = dir.StartEditorService();
        var bom = Encoding.UTF8.GetPreamble();
        var prefix = "namespace Test    {\n    ";
        var suffix = "\n   }\n";
        var start = bom.Length + Encoding.UTF8.GetByteCount(prefix);
        var end = start + "class   Test{}".Length;

        var result = editorService.Format(
            dir.GetPath("file.cs"),
            [.. bom, .. Encoding.UTF8.GetBytes(prefix + "class   Test{}" + suffix)],
            (start, end)
        );
        Assert.That(result, Is.EqualTo((byte[])[.. bom, .. Encoding.UTF8.GetBytes(prefix + "class Test { }" + suffix)]));
    }

    [Test]
    public void EditorService_OverrideConfig_UsesOverride()
    {
        using var dir = CreateDirectory(DefaultConfig);
        using var editorService = dir.StartEditorService();
        var filePath = dir.GetPath("file.cs");
        var text = "class Test\n{\n    void Method() { var x = (int)1; }\n}\n";

        Assert.That(
            editorService.FormatText(filePath, text, overrideConfigJson: """{ "csharp.indentWidth": 2, "csharp.spaceAfterCast": true }"""),
            Is.EqualTo("class Test\n{\n  void Method() { var x = (int) 1; }\n}\n")
        );
        // the override isn't remembered
        Assert.That(editorService.FormatText(filePath, text), Is.Null);
        // range and override config together
        var start = text.IndexOf("void", StringComparison.Ordinal);
        Assert.That(
            editorService.FormatText(filePath, text, (start, text.IndexOf("\n}", StringComparison.Ordinal)), """{ "csharp.spaceAfterCast": true }"""),
            Is.EqualTo("class Test\n{\n    void Method() { var x = (int) 1; }\n}\n")
        );
    }

    /// <summary>
    /// Ensures the Wasm build gives the same result as running the plugin's code on
    /// the .NET runtime by formatting the code in this repository with both.
    /// </summary>
    [Test]
    public void EditorService_RepositoryCode_SameAsDotNetRuntime()
    {
        using var dir = CreateDirectory(DefaultConfig);
        using var editorService = dir.StartEditorService();
        var plugin = new Plugin();
        plugin.RegisterConfig(1, Encoding.UTF8.GetBytes("""{ "global": { "newLineKind": "lf" }, "plugin": {} }"""));
        var filePaths = GetRepositoryCodeFilePaths().ToList();
        Assert.That(filePaths, Has.Count.GreaterThan(10));

        foreach (var filePath in filePaths)
        {
            var originalBytes = File.ReadAllBytes(filePath);
            foreach (var (description, fileBytes) in GetUnformattedVariations(originalBytes))
            {
                var expected = plugin.Format(1, "file.cs", fileBytes, null, null);
                var actual = editorService.Format(dir.GetPath("file.cs"), fileBytes);
                Assert.That(ToText(actual), Is.EqualTo(ToText(expected)), $"{filePath} ({description})");
            }
        }
    }

    private DprintDirectory CreateDirectory(string config)
    {
        return new DprintDirectory(_wasmPath, _cacheDir, config);
    }

    private static IEnumerable<(string Description, byte[] Bytes)> GetUnformattedVariations(byte[] fileBytes)
    {
        yield return ("original", fileBytes);
        var bom = Encoding.UTF8.GetPreamble();
        var hasBom = fileBytes.AsSpan().StartsWith(bom);
        var text = Encoding.UTF8.GetString(hasBom ? fileBytes[bom.Length..] : fileBytes).ReplaceLineEndings("\n");
        var lines = text.Split('\n');
        yield return ("no indentation", ToBytes(string.Join("\n", lines.Select(line => line.TrimStart()))));
        yield return ("extra indentation", ToBytes(string.Join("\n", lines.Select(line => line.Length == 0 ? line : "   " + line))));
        yield return ("crlf", ToBytes(text.Replace("\n", "\r\n")));

        byte[] ToBytes(string text) => hasBom ? [.. bom, .. Encoding.UTF8.GetBytes(text)] : Encoding.UTF8.GetBytes(text);
    }

    private static IEnumerable<string> GetRepositoryCodeFilePaths()
    {
        var rootDir = GetRepositoryRootDir();
        foreach (var projectDirName in new[] { "DprintPluginRoslyn", "DprintPluginRoslyn.Tests" })
        {
            var projectDir = Path.Combine(rootDir, projectDirName);
            foreach (var filePath in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(projectDir, filePath).Replace('\\', '/');
                if (!relativePath.StartsWith("bin/") && !relativePath.StartsWith("obj/"))
                    yield return filePath;
            }
        }
    }

    private static string GetRepositoryRootDir()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DprintPluginRoslyn.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Could not find the root directory of the repository.");
    }

    private static string? ToText(byte[]? bytes) => bytes is null ? null : Encoding.UTF8.GetString(bytes);
}
