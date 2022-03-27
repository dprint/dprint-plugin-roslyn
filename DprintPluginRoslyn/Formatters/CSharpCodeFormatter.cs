using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public class CSharpCodeFormatter : ICodeFormatter
{
  private readonly AdhocWorkspace _workspace;
  private readonly string _languageKeyPrefix = "csharp.";

  public CSharpCodeFormatter(AdhocWorkspace workspace)
  {
    _workspace = workspace;
  }

  public string RoslynLanguageName => LanguageNames.CSharp;

  public bool ShouldFormat(string filePath)
  {
    return filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
  }

  public string FormatText(string text, TextSpan? range, OptionSet options, CancellationToken token)
  {
    SyntaxNode formattedNode;

    var syntaxTree = CSharpSyntaxTree.ParseText(text);
    var root = syntaxTree.GetCompilationUnitRoot();
    if (range.HasValue)
      formattedNode = Formatter.Format(root, range.Value, _workspace, options, token);
    else
      formattedNode = Formatter.Format(root, _workspace, options, token);
    return formattedNode.WriteToString();
  }

  public void ResolveConfiguration(ConfigurationResolutionContext context)
  {
    // global config
    ConfigurationHelpers.HandleGlobalConfig(context, _languageKeyPrefix, LanguageNames.CSharp);

    // plugin config
    ConfigurationHelpers.HandlePluginConfig(context, typeof(CSharpFormattingOptions), _languageKeyPrefix);
  }

  public IEnumerable<(string, object)> GetResolvedConfig(OptionSet options)
  {
    return ConfigurationHelpers.GetResolvedGlobalConfig(options, _languageKeyPrefix, LanguageNames.CSharp)
        .Concat(ConfigurationHelpers.GetResolvedPluginConfig(options, typeof(CSharpFormattingOptions), _languageKeyPrefix));
  }
}
