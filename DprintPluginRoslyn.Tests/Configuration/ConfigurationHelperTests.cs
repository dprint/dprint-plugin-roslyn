using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Dprint.Plugins.Roslyn.Configuration;

[TestFixture]
public class ConfigurationHelperTests
{
    [Test]
    public void HandlePluginConfig_VariousCorrectInput_Handles()
    {
        var pluginConfig = new Dictionary<string, object>()
        {
            { "csharp.indentBlock", true },
            { "csharp.labelPositioning", "noIndent" },
        };
        var context = new ConfigurationResolutionContext(pluginConfig, new AdhocWorkspace().Options);
        ConfigurationHelpers.HandlePluginConfig(context, typeof(CSharpFormattingOptions), "csharp.");
        var options = context.GetOptions();

        Assert.That(options.GetOption(CSharpFormattingOptions.IndentBlock), Is.True);
        Assert.That(options.GetOption(CSharpFormattingOptions.LabelPositioning), Is.EqualTo(LabelPositionOptions.NoIndent));
        Assert.That(pluginConfig.Count, Is.EqualTo(0)); // should have cleared the plugin config
    }

    [Test]
    public void HandlePluginConfig_IncorrectInput_Diagnostics()
    {
        var pluginConfig = new Dictionary<string, object>()
        {
            { "csharp.indentBlock", "asdf" },
            { "csharp.labelPositioning", "testing" },
            { "csharp.unknown", true }
        };
        var context = new ConfigurationResolutionContext(pluginConfig, new AdhocWorkspace().Options);
        ConfigurationHelpers.HandlePluginConfig(context, typeof(CSharpFormattingOptions), "csharp.");
        var diagnostics = context.GetDiagnostics().ToList();
        Assert.That(diagnostics.Count, Is.EqualTo(3));
        Assert.That(diagnostics[0].PropertyName, Is.EqualTo("csharp.indentBlock"));
        Assert.That(diagnostics[0].Message, Is.EqualTo("Property value was expected to be Boolean, but was String."));
        Assert.That(diagnostics[1].PropertyName, Is.EqualTo("csharp.labelPositioning"));
        Assert.That(diagnostics[1].Message, Is.EqualTo("Could not parse string 'testing' to enum: Microsoft.CodeAnalysis.CSharp.Formatting.LabelPositionOptions"));
        Assert.That(diagnostics[2].PropertyName, Is.EqualTo("csharp.unknown"));
        Assert.That(diagnostics[2].Message, Is.EqualTo("Could not find property on CSharpFormattingOptions with name 'Unknown'"));
        Assert.That(pluginConfig.Count, Is.EqualTo(0)); // should have cleared the plugin config
    }
}
