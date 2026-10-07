using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public static class SyntaxFormatter
{
    /// <summary>
    /// Formats the node the same way as Roslyn's <c>Formatter.Format</c>, but without
    /// requiring a workspace.
    /// </summary>
    public static SyntaxNode Format(ISyntaxFormatting formatting, SyntaxNode root, TextSpan? range, OptionSet options, CancellationToken token)
    {
        var result = formatting.GetFormattingResult(
            root,
            [range ?? root.FullSpan],
            formatting.GetFormattingOptions(options),
            formatting.GetDefaultFormattingRules(),
            token
        );
        return result.GetFormattedRoot(token);
    }
}
