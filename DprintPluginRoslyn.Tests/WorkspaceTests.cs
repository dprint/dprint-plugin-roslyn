using Dprint.Plugins.Roslyn.Configuration;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dprint.Plugins.Roslyn;

[TestFixture]
public class WorkspaceTests
{
    [Test]
    public void GetDiagnostics_UnhandledPropertyNames_Diagnostics()
    {
        var workspace = new Workspace();
        var pluginConfig = new Dictionary<string, object>()
        {
            { "csharp.indentBlock", true },
            { "csharp.labelPositioning", "noIndent" },
            { "unknownProp", false }
        };
        workspace.SetConfig(1, new(), pluginConfig);
        var diagnostics = workspace.GetDiagnostics(1);
        Assert.That(diagnostics.Count, Is.EqualTo(1));
        Assert.That(diagnostics[0].PropertyName, Is.EqualTo("unknownProp"));
        Assert.That(diagnostics[0].Message, Is.EqualTo("Unknown configuration property name."));
    }

    [Test]
    public void GetResolvedConfig_Default_Gets()
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new());
        var resolvedConfig = workspace.GetFormatters(1).GetResolvedConfig();
        Assert.That(resolvedConfig["csharp.indentBlock"], Is.EqualTo(true));
        Assert.That(resolvedConfig["visualBasic.indentationSize"], Is.EqualTo(4));
        Assert.That(resolvedConfig["csharp.newLine"], Is.EqualTo("auto"));
    }

    [TestCase("\r\n")]
    [TestCase("\n")]
    public void FormatCode_DefaultNewLineKind_UsesLineEndingsOfFile(string newLine)
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new());

        var result = FormatIfStatement(workspace, newLine);

        Assert.That(result, Is.EqualTo(GetExpectedIfStatement(newLine, newLine)));
    }

    [TestCase("lf", "\n")]
    [TestCase("crlf", "\r\n")]
    public void FormatCode_NewLineKind_UsesItForAllLines(string newLineKind, string expectedNewLine)
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new() { { "newLineKind", newLineKind } });

        foreach (var fileNewLine in new[] { "\n", "\r\n" })
        {
            var result = FormatIfStatement(workspace, fileNewLine);

            Assert.That(result, Is.EqualTo(GetExpectedIfStatement(expectedNewLine, expectedNewLine)));
        }
    }

    [TestCase("class A\n{\r\n}\r\n", "class A\r\n{\r\n}\r\n")]
    [TestCase("class A\r\n{\r\n}\n", "class A\n{\n}\n")]
    public void FormatCode_MixedLineEndings_UsesLastLineEndingOfFile(string input, string expected)
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new());

        var result = workspace.GetFormatters(1).FormatCode("file.cs", Encoding.UTF8.GetBytes(input), null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(Encoding.UTF8.GetString(result!), Is.EqualTo(expected));
    }

    [Test]
    public void FormatCode_Range_KeepsLineEndingsOutsideRange()
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new() { { "newLineKind", "crlf" } });
        var input = "class A\n{\n    void M()\n    {\n        if (true) {\n        }\n    }\n}\n";
        var range = (input.IndexOf("if"), input.IndexOf("if") + "if (true) {".Length);

        var result = workspace.GetFormatters(1).FormatCode("file.cs", Encoding.UTF8.GetBytes(input), range, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(Encoding.UTF8.GetString(result!), Is.EqualTo("class A\n{\n    void M()\n    {\n        if (true)\r\n        {\n        }\n    }\n}\n"));
    }

    [Test]
    public void FormatCode_AutoNewLineKind_OverridesGlobalConfig()
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new() { NewLineKind = "lf" }, new() { { "newLineKind", "auto" } });

        var result = FormatIfStatement(workspace, "\r\n");

        Assert.That(result, Is.EqualTo(GetExpectedIfStatement("\r\n", "\r\n")));
    }

    [Test]
    public void Issue3_SetIndentWidth()
    {
        var workspace = new Workspace();
        var textBytes = Encoding.UTF8.GetBytes("{\"indentWidth\": 2 }");
        var pluginConfig = Serialization.PluginJson.DeserializeConfig(textBytes);
        workspace.SetConfig(2, new(), pluginConfig);
        var diagnostics = workspace.GetDiagnostics(2);
        Assert.That(diagnostics.Count, Is.EqualTo(0));
    }

    [Test]
    public void FormatCode_WithBom_PreservesBomWhenContentChanges()
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new());
        var bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes("namespace Test    {   }\n");
        var input = bom.Concat(content).ToArray();

        var result = workspace.GetFormatters(1).FormatCode("file.cs", input, null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Take(3).ToArray(), Is.EqualTo(bom));
    }

    [Test]
    public void FormatCode_WithBom_ReturnsNullWhenUnchanged()
    {
        var workspace = new Workspace();
        workspace.SetConfig(1, new(), new());
        var bom = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes("namespace Test { }\n");
        var input = bom.Concat(content).ToArray();

        var result = workspace.GetFormatters(1).FormatCode("file.cs", input, null, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    /// <summary>
    /// Formats code where Roslyn moves the brace of an if statement to a new line.
    /// </summary>
    private static string FormatIfStatement(Workspace workspace, string fileNewLine)
    {
        var lines = new[] { "class A", "{", "    void M()", "    {", "        if (true) {", "        }", "    }", "}", "" };
        var input = Encoding.UTF8.GetBytes(string.Join(fileNewLine, lines));
        var result = workspace.GetFormatters(1).FormatCode("file.cs", input, null, CancellationToken.None);
        Assert.That(result, Is.Not.Null);
        return Encoding.UTF8.GetString(result!);
    }

    private static string GetExpectedIfStatement(string fileNewLine, string insertedNewLine)
    {
        var lines = new[] { "class A", "{", "    void M()", "    {", $"        if (true){insertedNewLine}        {{", "        }", "    }", "}", "" };
        return string.Join(fileNewLine, lines);
    }
}
