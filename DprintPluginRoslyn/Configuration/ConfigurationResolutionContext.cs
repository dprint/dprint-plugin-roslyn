using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dprint.Plugins.Roslyn.Configuration;

public class ConfigurationResolutionContext
{
    private readonly IDictionary<string, object> _pluginConfig;
    private readonly IList<ConfigurationDiagnostic> _diagnostics = new List<ConfigurationDiagnostic>();
    private OptionSet _options;

    public ConfigurationResolutionContext(IDictionary<string, object> pluginConfig, OptionSet options)
    {
        _pluginConfig = pluginConfig;
        _options = options;
    }

    public IEnumerable<ConfigurationDiagnostic> GetDiagnostics()
    {
        return _diagnostics;
    }

    public OptionSet GetOptions()
    {
        return _options;
    }

    public IEnumerable<string> GetConfigKeys()
    {
        return _pluginConfig.Keys.ToList(); // make a copy
    }

    public bool RemoveInt(string propertyName, out int value)
    {
        return RemoveValue(propertyName, out value);
    }

    public bool RemoveBool(string propertyName, out bool value)
    {
        return RemoveValue(propertyName, out value);
    }

    public bool RemoveString(string propertyName, out string value)
    {
        return RemoveValue(propertyName, out value);
    }

    public object? RemoveValue(string propertyName)
    {
        object? value;
        _pluginConfig.Remove(propertyName, out value);
        return value;
    }

    private bool RemoveValue<T>(string propertyName, out T value)
    {
        if (_pluginConfig.Remove(propertyName, out var removedValue))
        {
            if (removedValue is T)
            {
                value = (T)removedValue;
                return true;
            }
            else if (typeof(T) == typeof(int) && removedValue is long)
            {
                // convert any long values to their expected int values
                value = (T)(object)(int)(long)removedValue;
                return true;
            }
            else
            {
                AddDiagnostic(propertyName, $"Value must be a {typeof(T).Name}");
#pragma warning disable CS8601 // Possible null reference assignment.
                value = default;
#pragma warning restore CS8601
                return false;
            }
        }
        else
        {
#pragma warning disable CS8601 // Possible null reference assignment.
            value = default;
#pragma warning restore CS8601
            return false;
        }
    }

    public void ChangeOption<T>(Option<T> option, T value)
    {
        _options = _options.WithChangedOption(option, value);
    }

    public void ChangeOption(OptionKey optionKey, object value)
    {
        _options = _options.WithChangedOption(optionKey, value);
    }

    public void ChangeOption<T>(PerLanguageOption<T> option, string language, T value)
    {
        _options = _options.WithChangedOption(option, language, value);
    }

    public void ChangeOption<T>(PerLanguageOption<T> option, IEnumerable<string> languages, T value)
    {
        foreach (var language in languages)
            _options = _options.WithChangedOption(option, language, value);
    }

    public void AddDiagnostic(string propertyName, string message)
    {
        _diagnostics.Add(new ConfigurationDiagnostic(propertyName, message));
    }
}
