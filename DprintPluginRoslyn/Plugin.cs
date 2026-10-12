using Dprint.Plugins.Roslyn.Serialization;
using System;
using System.IO;
using System.Threading;

namespace Dprint.Plugins.Roslyn;

/// <summary>
/// Handles the requests made by dprint.
/// </summary>
public class Plugin
{
    private readonly Workspace _workspace = new();

    public byte[] GetPluginInfo()
    {
        return PluginJson.SerializePluginInfo(GetAssemblyVersion());
    }

    public string GetLicenseText()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("Dprint.Plugins.Roslyn.LICENSE") ?? throw new Exception("Could not find license text.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void RegisterConfig(uint configId, byte[] configJson)
    {
        var (globalConfig, pluginConfig) = PluginJson.DeserializeRegisterConfig(configJson);
        _workspace.SetConfig(configId, globalConfig, pluginConfig);
    }

    public void ReleaseConfig(uint configId)
    {
        _workspace.ReleaseConfig(configId);
    }

    public byte[] GetConfigDiagnostics(uint configId)
    {
        return PluginJson.SerializeDiagnostics(_workspace.GetDiagnostics(configId));
    }

    public byte[] GetResolvedConfig(uint configId)
    {
        return PluginJson.SerializeConfig(_workspace.GetFormatters(configId).GetResolvedConfig());
    }

    public byte[] GetFileMatchingInfo(uint configId)
    {
        return PluginJson.SerializeFileMatchingInfo(_workspace.GetFormatters(configId).GetFileExtensions());
    }

    public byte[] CheckConfigUpdates()
    {
        return """{"kind":"ok","data":[]}"""u8.ToArray();
    }

    /// <summary>
    /// Formats the file returning the formatted bytes or null when there was no change.
    /// </summary>
    public byte[]? Format(uint configId, string filePath, byte[] fileBytes, (int Start, int End)? byteRange, byte[]? overrideConfigJson)
    {
        var overrideConfig = overrideConfigJson is null ? null : PluginJson.DeserializeConfig(overrideConfigJson);
        var formatters = _workspace.GetFormatters(configId, overrideConfig);
        return formatters.FormatCode(filePath, fileBytes, byteRange, CancellationToken.None);
    }

    private static string GetAssemblyVersion()
    {
        var version = typeof(Plugin).Assembly.GetName().Version ?? throw new Exception("Could not find assembly version.");
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
