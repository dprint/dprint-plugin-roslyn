using Dprint.Plugins.Roslyn.Configuration;
using Dprint.Plugins.Roslyn.Formatters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dprint.Plugins.Roslyn
{
    public class Workspace
    {
        private readonly AdhocWorkspace _workspace;
        private readonly ICodeFormatter[] _codeFormatters;

        private GlobalConfiguration? _globalConfig;
        private Dictionary<string, object>? _pluginConfig;
        private List<ConfigurationDiagnostic> _diagnostics = new List<ConfigurationDiagnostic>();
        private OptionSet? _options;

        public Workspace()
        {
            _workspace = new AdhocWorkspace();
            _codeFormatters = new ICodeFormatter[]
            {
                new CSharpCodeFormatter(_workspace),
                new VisualBasicCodeFormatter(_workspace),
            };
        }

        public IReadOnlyList<ConfigurationDiagnostic> GetDiagnostics()
        {
            GetOptions(); // ensures set
            return _diagnostics;
        }

        public void SetGlobalConfig(GlobalConfiguration globalConfig)
        {
            _globalConfig = globalConfig;
            ResetConfig();
        }

        public void SetPluginConfig(Dictionary<string, object> pluginConfig)
        {
            _pluginConfig = pluginConfig;
            ResetConfig();
        }

        public Dictionary<string, object> GetResolvedConfig()
        {
            var config = new Dictionary<string, object>();
            var options = GetOptions();

            foreach (var formatter in _codeFormatters)
            {
                foreach (var (key, value) in formatter.GetResolvedConfig(options))
                    config[key] = value;
            }

            return config;
        }

        public string FormatCode(string filePath, string code, Dictionary<string, object> overrideConfig)
        {
            var formatter = _codeFormatters.FirstOrDefault(formatter => formatter.ShouldFormat(filePath));
            if (formatter is null)
                throw new Exception($"Could not find formatter for file path: {filePath}");
            var options = overrideConfig.Count == 0 ? GetOptions() : CreateOptions(overrideConfig).Item1;
            return formatter.FormatText(code, GetOptions());
        }

        private OptionSet GetOptions()
        {
            if (_options != null)
                return _options;

            var (options, diagnostics) = CreateOptions(new Dictionary<string, object>());

            // finalize state
            _diagnostics.Clear();
            _diagnostics.AddRange(diagnostics);
            _options = options;

            return _options;
        }

        private (OptionSet, List<ConfigurationDiagnostic>) CreateOptions(Dictionary<string, object> overrideConfig)
        {
            var globalConfig = _globalConfig ?? new GlobalConfiguration();
            var pluginConfig = new Dictionary<string, object>(_pluginConfig ?? new Dictionary<string, object>());

            foreach (var (key, value) in overrideConfig)
            {
                pluginConfig[key] = value;
            }

            var context = new ConfigurationResolutionContext(pluginConfig, _workspace.Options);
            var languages = _codeFormatters.Select(f => f.RoslynLanguageName).ToList();

            if (globalConfig.IndentWidth.HasValue)
            {
                context.ChangeOption(FormattingOptions.IndentationSize, languages, globalConfig.IndentWidth.Value);
                context.ChangeOption(FormattingOptions.TabSize, languages, globalConfig.IndentWidth.Value);
            }
            if (globalConfig.UseTabs.HasValue)
                context.ChangeOption(FormattingOptions.UseTabs, languages, globalConfig.UseTabs.Value);
            if (globalConfig.NewLineKind != null)
                context.ChangeOption(FormattingOptions.NewLine, languages, ConfigurationHelpers.GetNewLineKind(context, "newLineKind", globalConfig.NewLineKind));

            ConfigurationHelpers.HandleGlobalConfig(context, string.Empty, languages);

            foreach (var formatter in _codeFormatters)
                formatter.ResolveConfiguration(context);

            // add unhandled configuration diagnostics
            foreach (var configKey in context.GetConfigKeys())
                context.AddDiagnostic(configKey, "Unknown configuration property name.");

            // finalize state
            return (context.GetOptions(), context.GetDiagnostics().ToList());
        }

        private void ResetConfig()
        {
            _options = null;
            _diagnostics.Clear();
        }
    }
}
