using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public interface ICodeFormatter
{
    string RoslynLanguageName { get; }
    string FileExtension { get; }
    /// <summary>
    /// Whether the files of the language are formatted, which is decided by the language's "enabled" configuration.
    /// </summary>
    bool IsEnabled { get; }
    bool ShouldFormat(string filePath);
    SourceText FormatText(SourceText text, TextSpan? range, OptionSet options, CancellationToken token);
    void ResolveConfiguration(ConfigurationResolutionContext context);
    IEnumerable<(string, object)> GetResolvedConfig(OptionSet options);
}
