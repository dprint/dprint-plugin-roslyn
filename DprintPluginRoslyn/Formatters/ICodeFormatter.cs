using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis.Options;
using System.Collections.Generic;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public interface ICodeFormatter
    {
        string RoslynLanguageName { get; }
        bool ShouldFormat(string filePath);
        string FormatText(string text, OptionSet options);
        void ResolveConfiguration(ConfigurationResolutionContext context);
        IEnumerable<(string, object)> GetResolvedConfig(OptionSet options);
    }
}
