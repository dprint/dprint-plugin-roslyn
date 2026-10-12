using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public class VisualBasicCodeFormatter : ICodeFormatter
{
    private readonly string _languageKeyPrefix = "visualBasic.";

    public string RoslynLanguageName => LanguageNames.VisualBasic;

    public string FileExtension => "vb";

    public bool IsEnabled { get; private set; } = true;

    public bool ShouldFormat(string filePath)
    {
        return filePath.EndsWith($".{FileExtension}", StringComparison.OrdinalIgnoreCase);
    }

    public SourceText FormatText(SourceText text, TextSpan? range, OptionSet options, CancellationToken token)
    {
        var syntaxTree = VisualBasicSyntaxTree.ParseText(text);
        var root = syntaxTree.GetCompilationUnitRoot();
        var formattedNode = SyntaxFormatter.Format(VisualBasicSyntaxFormatting.Instance, root, range, options, token);
        return formattedNode.GetText(text.Encoding, text.ChecksumAlgorithm);
    }

    public void ResolveConfiguration(ConfigurationResolutionContext context)
    {
        IsEnabled = ConfigurationHelpers.HandleEnabledConfig(context, _languageKeyPrefix);

        // seems like there's no language specific formatting options for visual basic

        // global config
        ConfigurationHelpers.HandleGlobalConfig(context, _languageKeyPrefix, LanguageNames.VisualBasic);
    }

    public IEnumerable<(string, object)> GetResolvedConfig(OptionSet options)
    {
        return ConfigurationHelpers.GetResolvedGlobalConfig(options, _languageKeyPrefix, LanguageNames.VisualBasic)
            .Prepend(($"{_languageKeyPrefix}enabled", IsEnabled));
    }
}
