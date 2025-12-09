using Dprint.Plugins.Roslyn.Configuration;
using Dprint.Plugins.Roslyn.Formatters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dprint.Plugins.Roslyn;

public class ResolvedContext
{
    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; init; } = null!;
    public OptionSet Options { get; init; } = null!;
    public AdhocWorkspace AdhocWorkspace { get; init; } = null!;
    public CodeFormatters Formatters { get; init; } = null!;
}

public class StoredConfig
{
    public IReadOnlyDictionary<string, object> PluginConfig { get; init; } = null!;
    public GlobalConfiguration GlobalConfig { get; init; } = null!;
    public Lazy<ResolvedContext> DefaultContext { get; init; } = null!;
}

/// <summary>
/// Not thread safe.
/// </summary>
public class Workspace
{
    private readonly Dictionary<uint, StoredConfig> _configs = new();

    public CodeFormatters GetFormatters(uint configId, Dictionary<string, object>? overrideConfig = null)
    {
        var config = GetStoredConfig(configId);
        var context = overrideConfig == null || overrideConfig.Count == 0
            ? config.DefaultContext.Value
            : CreateResolvedContext(config.GlobalConfig, config.PluginConfig, overrideConfig);
        return context.Formatters;
    }

    public IReadOnlyList<ConfigurationDiagnostic> GetDiagnostics(uint configId)
    {
        return GetStoredConfig(configId).DefaultContext.Value.Diagnostics;
    }

    public void SetConfig(uint configId, GlobalConfiguration globalConfig, Dictionary<string, object> pluginConfig)
    {
        _configs[configId] = new StoredConfig
        {
            GlobalConfig = globalConfig,
            PluginConfig = pluginConfig,
            DefaultContext = new Lazy<ResolvedContext>(() => CreateResolvedContext(globalConfig, pluginConfig, new Dictionary<string, object>())),
        };
    }

    public void ReleaseConfig(uint configId)
    {
        _configs.Remove(configId, out var _);
    }

    private StoredConfig GetStoredConfig(uint configId)
    {
        if (_configs.TryGetValue(configId, out var value))
            return value;
        else
            throw new ArgumentOutOfRangeException(nameof(configId), $"Could not find configuration id: {configId}");
    }

    private ResolvedContext CreateResolvedContext(
        GlobalConfiguration globalConfig,
        IReadOnlyDictionary<string, object> readonlyPluginConfig,
        IReadOnlyDictionary<string, object> overrideConfig
    )
    {
        var workspace = new AdhocWorkspace();
        var formatters = new ICodeFormatter[]
        {
            new CSharpCodeFormatter(workspace),
            new VisualBasicCodeFormatter(workspace),
        };
        var pluginConfig = readonlyPluginConfig.ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var (key, value) in overrideConfig)
        {
            pluginConfig[key] = value;
        }

        var context = new ConfigurationResolutionContext(pluginConfig, workspace.Options);
        var languages = formatters.Select(f => f.RoslynLanguageName).ToList();

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

        foreach (var formatter in formatters)
            formatter.ResolveConfiguration(context);

        // add unhandled configuration diagnostics
        foreach (var configKey in context.GetConfigKeys())
            context.AddDiagnostic(configKey, "Unknown configuration property name.");

        // finalize state
        var options = context.GetOptions();
        return new ResolvedContext
        {
            Options = options,
            Diagnostics = context.GetDiagnostics().ToList(),
            AdhocWorkspace = workspace,
            Formatters = new CodeFormatters(formatters, options),
        };
    }
}
