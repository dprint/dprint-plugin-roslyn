using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.VisualBasic;
using System;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public class VisualBasicCodeFormatter : ICodeFormatter
    {
        public bool ShouldFormat(string filePath)
        {
            return filePath.EndsWith(".vb", StringComparison.OrdinalIgnoreCase);
        }

        public string FormatText(string text)
        {
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedNode;

            var syntaxTree = VisualBasicSyntaxTree.ParseText(text);
            var root = syntaxTree.GetCompilationUnitRoot();
            formattedNode = Formatter.Format(root, workspace);
            return formattedNode.WriteToString();
        }
    }
}
