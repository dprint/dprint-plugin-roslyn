using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public class CSharpCodeFormatter : ICodeFormatter
    {
        private readonly AdhocWorkspace _workspace;

        public CSharpCodeFormatter(AdhocWorkspace workspace)
        {
            _workspace = workspace;
        }

        public bool ShouldFormat(string filePath)
        {
            return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        public string FormatText(string text, OptionSet options)
        {
            SyntaxNode formattedNode;

            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var root = syntaxTree.GetCompilationUnitRoot();
            formattedNode = Formatter.Format(root, _workspace, options);
            return formattedNode.WriteToString();
        }

        public void ResolveConfiguration(ConfigurationResolutionContext context)
        {
            var languageKeyPrefix = "csharp.";

            // global config
            ConfigurationHelpers.HandleGlobalConfig(context, languageKeyPrefix, LanguageNames.CSharp);

            // plugin config
            ConfigurationHelpers.HandlePluginConfig(context, typeof(CSharpFormattingOptions), languageKeyPrefix);
        }
    }
}
