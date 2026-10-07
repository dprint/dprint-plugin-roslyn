using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn.Wasm;

/// <summary>
/// A temporary directory with a dprint configuration file that uses the Wasm plugin.
/// </summary>
public sealed class DprintDirectory : IDisposable
{
    private readonly string _dirPath;
    private readonly string _cacheDir;

    /// <param name="wasmPath">Path to the plugin's .wasm file.</param>
    /// <param name="cacheDir">Directory dprint stores the compiled plugin in.</param>
    /// <param name="config">Properties to include in the configuration file (ex. <c>"indentWidth": 2</c>).</param>
    public DprintDirectory(string wasmPath, string cacheDir, string config)
    {
        _cacheDir = cacheDir;
        _dirPath = Directory.CreateTempSubdirectory("dprint-plugin-roslyn-test").FullName;
        var pluginsProperty = $"\"plugins\": [{JsonSerializer.Serialize(wasmPath.Replace('\\', '/'))}]";
        var properties = string.IsNullOrWhiteSpace(config) ? pluginsProperty : $"{config},\n{pluginsProperty}";
        WriteFile("dprint.json", $"{{\n{properties}\n}}\n");
    }

    public void Dispose()
    {
        DeleteDirectory(_dirPath);
    }

    public string GetPath(string fileName) => Path.Combine(_dirPath, fileName);

    public void WriteFile(string fileName, string text) => WriteFile(fileName, Encoding.UTF8.GetBytes(text));

    public void WriteFile(string fileName, byte[] bytes) => File.WriteAllBytes(GetPath(fileName), bytes);

    public string ReadFile(string fileName) => Encoding.UTF8.GetString(ReadFileBytes(fileName));

    public byte[] ReadFileBytes(string fileName) => File.ReadAllBytes(GetPath(fileName));

    /// <summary>
    /// Runs the dprint CLI in the directory.
    /// </summary>
    public DprintResult Run(params string[] args)
    {
        using var process = Process.Start(CreateStartInfo(args)) ?? throw new Exception("Could not start dprint.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            throw new Exception($"Timed out running dprint {string.Join(' ', args)}");
        }
        Task.WaitAll(stdout, stderr);
        return new DprintResult(process.ExitCode, stdout.Result + stderr.Result);
    }

    public EditorService StartEditorService()
    {
        return new EditorService(CreateStartInfo("editor-service", "--parent-pid", Environment.ProcessId.ToString()));
    }

    public static void DeleteDirectory(string dirPath)
    {
        try
        {
            Directory.Delete(dirPath, recursive: true);
        }
        catch (IOException)
        {
            // ignore, a process may not have released a file yet
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private ProcessStartInfo CreateStartInfo(params string[] args)
    {
        var dprintPath = Environment.GetEnvironmentVariable("DPRINT_PATH");
        var startInfo = new ProcessStartInfo(string.IsNullOrEmpty(dprintPath) ? "dprint" : dprintPath, args)
        {
            WorkingDirectory = _dirPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        startInfo.Environment["DPRINT_CACHE_DIR"] = _cacheDir;
        startInfo.Environment["NO_COLOR"] = "1";
        return startInfo;
    }
}

/// <param name="Output">The combined stdout and stderr text.</param>
public record DprintResult(int ExitCode, string Output);
