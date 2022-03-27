using System;

namespace Dprint.Plugins.Roslyn;

public struct CliArguments
{
    public int ParentProcessId { get; set; }
}

public class ArgParser
{
    public CliArguments ParseArgs(string[] args)
    {
        // very simple and not smart for now
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--parent-pid" && i + 1 < args.Length)
            {
                return new CliArguments
                {
                    ParentProcessId = int.Parse(args[i + 1]),
                };
            }
        }
        throw new Exception("Failed parsing arguments. Expected --parent-pid");
    }
}
