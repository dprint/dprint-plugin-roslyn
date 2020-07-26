using Microsoft.CodeAnalysis;
using System.IO;
using System.Text;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public static class SyntaxNodeExtensions
    {
        public static string WriteToString(this SyntaxNode syntaxNode)
        {
            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);
            syntaxNode.WriteTo(writer);
            return sb.ToString();
        }
    }
}
