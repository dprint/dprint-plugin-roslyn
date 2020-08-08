using Dprint.Plugins.Roslyn.Communication;
using System;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn
{
    class Program
    {
        static void Main(string[] args)
        {
            var cliArgs = new ArgParser().ParseArgs(args);
            var parentProcessChecker = new ParentProcessChecker(cliArgs.ParentProcessId);

            // start the task to periodically check if the parent process has exited and exit if so
            Task.Run(() =>
            {
                return parentProcessChecker.RunCheckerLoop();
            });

            // start the stdio message handler loop
            try
            {
                var stdInOut = new StdInOutReaderWriter();
                var workspace = new Workspace();

                var messageProcessor = new MessageProcessor(stdInOut, workspace);
                messageProcessor.RunBlockingMessageLoop();
            }
            catch (Exception ex)
            {
                // An exception might be thrown because the parent process is not active anymore.
                // If so, just display that message and ignore the exception.
                if (!parentProcessChecker.IsProcessActive)
                    parentProcessChecker.KillCurrentProcessWithErrorMessage();

                throw ex;
            }
        }
    }
}
