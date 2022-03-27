using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public interface ICodeFormatter
{
  string RoslynLanguageName { get; }
  bool ShouldFormat(string filePath);
  string FormatText(string text, TextSpan? range, OptionSet options, CancellationToken token);
  void ResolveConfiguration(ConfigurationResolutionContext context);
  IEnumerable<(string, object)> GetResolvedConfig(OptionSet options);
}
