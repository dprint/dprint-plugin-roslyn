using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.VisualBasic;
using System;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public class VisualBasicCodeFormatter : ICodeFormatter
    {
        private readonly AdhocWorkspace _workspace;

        public VisualBasicCodeFormatter(AdhocWorkspace workspace)
        {
            _workspace = workspace;
        }

        public bool ShouldFormat(string filePath)
        {
            return filePath.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
        }

        public string FormatText(string text, OptionSet options)
        {
            SyntaxNode formattedNode;

            var syntaxTree = VisualBasicSyntaxTree.ParseText(text);
            var root = syntaxTree.GetCompilationUnitRoot();
            formattedNode = Formatter.Format(root, _workspace, options);
            return formattedNode.WriteToString();
        }

        public void ResolveConfiguration(ConfigurationResolutionContext context)
        {
            // seems like there's no language specific formatting options for visual basic
            var languageKeyPrefix = "vb.";

            // global config
            ConfigurationHelpers.HandleGlobalConfig(context, languageKeyPrefix, LanguageNames.VisualBasic);
        }
    }
}
