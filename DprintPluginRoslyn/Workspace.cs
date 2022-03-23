using Dprint.Plugins.Roslyn.Configuration;
using Dprint.Plugins.Roslyn.Formatters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dprint.Plugins.Roslyn;

public class OptionsWithDiagnostics
{
    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; init; } = null!;
    public OptionSet Options { get; init; } = null!;
}

public class StoredConfig
{
    public IReadOnlyDictionary<string, object> PluginConfig { get; init; } = null!;
    public GlobalConfiguration GlobalConfig { get; init; } = null!;
    public Lazy<OptionsWithDiagnostics> DefaultConfig { get; init; } = null!;
}

public class Workspace
{
    private readonly AdhocWorkspace _workspace;
    private readonly ICodeFormatter[] _codeFormatters;
    private readonly ConcurrentDictionary<uint, StoredConfig> _configs = new();

    public Workspace()
    {
        _workspace = new AdhocWorkspace();
        _codeFormatters = new ICodeFormatter[]
        {
            new CSharpCodeFormatter(_workspace),
            new VisualBasicCodeFormatter(_workspace),
        };
    }

    public string FormatCode(string filePath, string code, TextSpan? range, uint configId, Dictionary<string, object> overrideConfig, CancellationToken token)
    {
        var formatter = _codeFormatters.FirstOrDefault(formatter => formatter.ShouldFormat(filePath));
        if (formatter is null)
            throw new Exception($"Could not find formatter for file path: {filePath}");
        var config = GetStoredConfig(configId);
        var options = overrideConfig.Count == 0 ? config.DefaultConfig.Value : CreateOptions(config.GlobalConfig, config.PluginConfig, overrideConfig);
        return formatter.FormatText(code, range, options.Options, token);
    }

    public IReadOnlyList<ConfigurationDiagnostic> GetDiagnostics(uint configId)
    {
        return GetStoredConfig(configId).DefaultConfig.Value.Diagnostics;
    }

    public void SetConfig(uint configId, GlobalConfiguration globalConfig, Dictionary<string, object> pluginConfig)
    {
        var config = new StoredConfig
        {
            GlobalConfig = globalConfig,
            PluginConfig = pluginConfig,
            DefaultConfig = new Lazy<OptionsWithDiagnostics>(() => CreateOptions(globalConfig, pluginConfig, new Dictionary<string, object>())),
        };
        _configs.AddOrUpdate(configId, (_) => config, (_, _) => config);
    }

    public void ReleaseConfig(uint configId)
    {
        _configs.Remove(configId, out var _);
    }

    public Dictionary<string, object> GetResolvedConfig(uint configId)
    {
        var config = new Dictionary<string, object>();
        var options = GetStoredConfig(configId).DefaultConfig.Value.Options;

        foreach (var formatter in _codeFormatters)
        {
            foreach (var (key, value) in formatter.GetResolvedConfig(options))
                config[key] = value;
        }

        return config;
    }

    private StoredConfig GetStoredConfig(uint configId)
    {
        if (_configs.TryGetValue(configId, out var value))
            return value;
        else
            throw new ArgumentOutOfRangeException(nameof(configId), $"Could not find configuration id: {configId}");
    }

    private OptionsWithDiagnostics CreateOptions(
        GlobalConfiguration globalConfig,
        IReadOnlyDictionary<string, object> readonlyPluginConfig,
        IReadOnlyDictionary<string, object> overrideConfig
    )
    {
        var pluginConfig = readonlyPluginConfig.ToDictionary(kv => kv.Key, kv => kv.Value);
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
        return new OptionsWithDiagnostics
        {
            Options = context.GetOptions(),
            Diagnostics = context.GetDiagnostics().ToList()
        };
    }
}
