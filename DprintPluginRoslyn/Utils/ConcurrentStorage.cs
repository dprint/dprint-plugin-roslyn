using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn.Utils;

/// <summary>
/// Concurrent dictionary but less verbose API.
/// </summary>
public class ConcurrentStorage<TValue>
{
    private readonly object _lock = new object();
    private readonly Dictionary<uint, TValue> _values = new();

    public void StoreValue(uint messageId, TValue value)
    {
        lock (_lock)
            _values[messageId] = value;
    }

    public TValue? Take(uint messageId)
    {
        lock (_lock)
        {
            if (_values.Remove(messageId, out var value))
                return value;
            else
                return default(TValue);
        }
    }
}
