using Microsoft.CodeAnalysis.Options;
using System.Collections.Immutable;

namespace Dprint.Plugins.Roslyn.Configuration;

/// <summary>
/// An option set that isn't tied to a Roslyn workspace.
/// </summary>
public sealed class PluginOptionSet : OptionSet
{
    private readonly ImmutableDictionary<OptionKey, object?> _values;

    public PluginOptionSet()
        : this(ImmutableDictionary<OptionKey, object?>.Empty)
    {
    }

    private PluginOptionSet(ImmutableDictionary<OptionKey, object?> values)
    {
        _values = values;
    }

    public override object? GetInternalOptionValue(OptionKey optionKey)
    {
        return _values.TryGetValue(optionKey, out var value) ? value : ((IOption2)optionKey.Option).Definition.DefaultValue;
    }

    public override OptionSet WithChangedOptionInternal(OptionKey optionKey, object? internalValue)
    {
        return new PluginOptionSet(_values.SetItem(optionKey, internalValue));
    }
}
