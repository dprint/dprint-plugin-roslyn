using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn;

/// <summary>
/// Quits running the current application when it detects the parent process no longer exists.
/// </summary>
/// <remarks>
/// This is to prevent an orphan process.
/// </remarks>
public class ParentProcessChecker
{
  private const int POLLING_MS = 30_000;
  private readonly int _processId;

  public ParentProcessChecker(int processId)
  {
    _processId = processId;
  }

  public async Task RunCheckerLoop()
  {
    // Note: I don't believe I can use process.Exited here. Doing a brief look
    // at the source code, that only seems to apply if spawning a process.
    // Anyway, this will be reliable enough.
    while (true)
    {
      await Task.Delay(POLLING_MS).ConfigureAwait(false);

      if (!IsProcessActive)
        ExitCurrentProcessWithErrorCode();
    }
  }

  public bool IsProcessActive
  {
    get
    {
      try
      {
        var process = Process.GetProcessById(_processId);
        return process != null && !process.HasExited;
      }
      catch
      {
        // it is not running
        return false;
      }
    }
  }

  public void ExitCurrentProcessWithErrorCode()
  {
    Environment.Exit(1);
  }
}
