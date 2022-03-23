using Dprint.Plugins.Roslyn.Communication;
using System;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn;

class Program
{
    static async void Main(string[] args)
    {
        var cliArgs = new ArgParser().ParseArgs(args);
        var parentProcessChecker = new ParentProcessChecker(cliArgs.ParentProcessId);

        // start the task to periodically check if the parent process has exited and exit if so
        var _ignore = Task.Run(() => parentProcessChecker.RunCheckerLoop());

        // start the stdio message handler loop
        try
        {
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();
            var reader = new MessageReader(stdin);
            var writer = new MessageWriter(stdout);

            EstablishSchemaVersion(reader, writer);

            var workspace = new Workspace();

            var messageProcessor = new MessageProcessor(workspace);
            await messageProcessor.Run(reader, writer);
        }
        catch
        {
            // An exception might be thrown because the parent process is not active anymore.
            if (!parentProcessChecker.IsProcessActive)
                parentProcessChecker.ExitCurrentProcessWithErrorCode();

            throw;
        }
    }

    static void EstablishSchemaVersion(MessageReader reader, MessageWriter writer)
    {
        // 1. An initial `0` (4 bytes) is sent asking for the schema version.
        var request = reader.ReadUint();
        if (request != 0)
            throw new Exception("Expected a schema version request of `0`.");

        // 2. The client responds with `0` (4 bytes) for success, then `4` (4 bytes) for the schema version.
        writer.WriteUint(0);
        writer.WriteUint(4);
        writer.Flush();
    }
}
