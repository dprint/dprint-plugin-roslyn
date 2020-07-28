using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis.Options;

namespace Dprint.Plugins.Roslyn.Formatters
{
    public interface ICodeFormatter
    {
        public bool ShouldFormat(string filePath);
        public string FormatText(string text, OptionSet options);
        public void ResolveConfiguration(ConfigurationResolutionContext context);
    }
}
