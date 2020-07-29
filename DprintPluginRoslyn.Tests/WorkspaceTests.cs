using NUnit.Framework;
using System.Collections.Generic;

namespace Dprint.Plugins.Roslyn
{
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
            workspace.SetPluginConfig(pluginConfig);
            var diagnostics = workspace.GetDiagnostics();
            Assert.That(diagnostics.Count, Is.EqualTo(1));
            Assert.That(diagnostics[0].PropertyName, Is.EqualTo("unknownProp"));
            Assert.That(diagnostics[0].Message, Is.EqualTo("Unknown configuration property name."));
        }

        [Test]
        public void GetResolvedConfig_Default_Gets()
        {
            var workspace = new Workspace();
            var resolvedConfig = workspace.GetResolvedConfig();
            Assert.That(resolvedConfig["csharp.indentBlock"], Is.EqualTo(true));
            Assert.That(resolvedConfig["visualBasic.indentationSize"], Is.EqualTo(4));
        }
    }
}
