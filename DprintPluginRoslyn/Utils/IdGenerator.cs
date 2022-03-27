using System.Threading;

namespace Dprint.Plugins.Roslyn.Utils;

/// <summary>
/// Thread safe counter.
/// </summary>
public class IdGenerator
{
    private uint _counter = 0;

    public uint Next()
    {
        return Interlocked.Increment(ref _counter);
    }
}
