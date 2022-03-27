using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public class CodeFormatters
{
  private readonly ICodeFormatter[] _codeFormatters;
  private readonly OptionSet _options;

  public CodeFormatters(ICodeFormatter[] codeFormatters, OptionSet options)
  {
    _codeFormatters = codeFormatters;
    _options = options;
  }

  public string FormatCode(string filePath, string code, TextSpan? range, CancellationToken token)
  {
    var formatter = _codeFormatters.FirstOrDefault(formatter => formatter.ShouldFormat(filePath));
    if (formatter is null)
      throw new Exception($"Could not find formatter for file path: {filePath}");
    return formatter.FormatText(code, range, _options, token);
  }

  public Dictionary<string, object> GetResolvedConfig()
  {
    var config = new Dictionary<string, object>();
    foreach (var formatter in _codeFormatters)
    {
      foreach (var (key, value) in formatter.GetResolvedConfig(_options))
        config[key] = value;
    }

    return config;
  }
}
