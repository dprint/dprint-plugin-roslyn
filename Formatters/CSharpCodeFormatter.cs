using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using System;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public class CSharpCodeFormatter : ICodeFormatter
    {
        public bool ShouldFormat(string filePath)
        {
            return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        }

        public string FormatText(string text)
        {
            var workspace = new AdhocWorkspace();
            SyntaxNode formattedNode;

            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var root = syntaxTree.GetCompilationUnitRoot();
            formattedNode = Formatter.Format(root, workspace);
            return formattedNode.WriteToString();
        }
    }
}
