using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Dprint.Plugins.Roslyn;

/// <summary>
/// The functions dprint calls on a schema version 4 Wasm plugin.
/// </summary>
/// <remarks>
/// Data is passed between the host and the plugin using a shared buffer. The
/// host writes to it after calling <c>clear_shared_bytes</c> and reads from
/// it using <c>get_shared_bytes_ptr</c> after a function returns a length.
/// </remarks>
public static unsafe class WasmExports
{
    private const uint FormatResultNoChange = 0;
    private const uint FormatResultChange = 1;
    private const uint FormatResultError = 2;

    private static readonly Plugin _plugin = new();
    private static byte* _sharedBytes;
    private static int _sharedBytesLength;
    private static string? _filePath;
    private static byte[]? _overrideConfig;
    private static byte[]? _formattedText;
    private static string? _errorText;

    [UnmanagedCallersOnly(EntryPoint = "dprint_plugin_version_4")]
    public static uint DprintPluginVersion4() => 4;

    [UnmanagedCallersOnly(EntryPoint = "get_plugin_info")]
    public static uint GetPluginInfo() => SetSharedBytes(_plugin.GetPluginInfo());

    [UnmanagedCallersOnly(EntryPoint = "get_license_text")]
    public static uint GetLicenseText() => SetSharedBytes(Encoding.UTF8.GetBytes(_plugin.GetLicenseText()));

    [UnmanagedCallersOnly(EntryPoint = "register_config")]
    public static void RegisterConfig(uint configId) => _plugin.RegisterConfig(configId, TakeSharedBytes());

    [UnmanagedCallersOnly(EntryPoint = "release_config")]
    public static void ReleaseConfig(uint configId) => _plugin.ReleaseConfig(configId);

    [UnmanagedCallersOnly(EntryPoint = "get_config_diagnostics")]
    public static uint GetConfigDiagnostics(uint configId) => SetSharedBytes(_plugin.GetConfigDiagnostics(configId));

    [UnmanagedCallersOnly(EntryPoint = "get_resolved_config")]
    public static uint GetResolvedConfig(uint configId) => SetSharedBytes(_plugin.GetResolvedConfig(configId));

    [UnmanagedCallersOnly(EntryPoint = "get_config_file_matching")]
    public static uint GetConfigFileMatching(uint configId) => SetSharedBytes(_plugin.GetFileMatchingInfo());

    [UnmanagedCallersOnly(EntryPoint = "check_config_updates")]
    public static uint CheckConfigUpdates()
    {
        TakeSharedBytes();
        return SetSharedBytes(_plugin.CheckConfigUpdates());
    }

    [UnmanagedCallersOnly(EntryPoint = "set_override_config")]
    public static void SetOverrideConfig() => _overrideConfig = TakeSharedBytes();

    [UnmanagedCallersOnly(EntryPoint = "set_file_path")]
    public static void SetFilePath() => _filePath = Encoding.UTF8.GetString(TakeSharedBytes());

    [UnmanagedCallersOnly(EntryPoint = "format")]
    public static uint Format(uint configId) => FormatInner(configId, null);

    [UnmanagedCallersOnly(EntryPoint = "format_range")]
    public static uint FormatRange(uint configId, uint rangeStart, uint rangeEnd) => FormatInner(configId, ((int)rangeStart, (int)rangeEnd));

    [UnmanagedCallersOnly(EntryPoint = "get_formatted_text")]
    public static uint GetFormattedText()
    {
        var bytes = _formattedText ?? throw new InvalidOperationException("Expected to have formatted text.");
        _formattedText = null;
        return SetSharedBytes(bytes);
    }

    [UnmanagedCallersOnly(EntryPoint = "get_error_text")]
    public static uint GetErrorText()
    {
        var text = _errorText ?? throw new InvalidOperationException("Expected to have error text.");
        _errorText = null;
        return SetSharedBytes(Encoding.UTF8.GetBytes(text));
    }

    [UnmanagedCallersOnly(EntryPoint = "get_shared_bytes_ptr")]
    public static byte* GetSharedBytesPtr() => _sharedBytes;

    [UnmanagedCallersOnly(EntryPoint = "clear_shared_bytes")]
    public static byte* ClearSharedBytes(uint size)
    {
        NativeMemory.Free(_sharedBytes);
        _sharedBytes = (byte*)NativeMemory.AllocZeroed(size);
        _sharedBytesLength = (int)size;
        return _sharedBytes;
    }

    private static uint FormatInner(uint configId, (int Start, int End)? byteRange)
    {
        try
        {
            var filePath = _filePath ?? throw new InvalidOperationException("Expected the file path to be set.");
            var overrideConfig = _overrideConfig;
            _filePath = null;
            _overrideConfig = null;
            var fileBytes = TakeSharedBytes();

            // Roslyn can't be interrupted from the host, so only check before starting
            if (HostHasCancelled() == 1)
                return FormatResultNoChange;

            _formattedText = _plugin.Format(configId, filePath, fileBytes, byteRange, overrideConfig);
            return _formattedText is null ? FormatResultNoChange : FormatResultChange;
        }
        catch (Exception ex)
        {
            _errorText = ex.ToString();
            return FormatResultError;
        }
    }

    private static uint SetSharedBytes(ReadOnlySpan<byte> bytes)
    {
        NativeMemory.Free(_sharedBytes);
        _sharedBytes = (byte*)NativeMemory.Alloc((nuint)bytes.Length);
        _sharedBytesLength = bytes.Length;
        bytes.CopyTo(new Span<byte>(_sharedBytes, bytes.Length));
        return (uint)bytes.Length;
    }

    private static byte[] TakeSharedBytes()
    {
        var bytes = new ReadOnlySpan<byte>(_sharedBytes, _sharedBytesLength).ToArray();
        NativeMemory.Free(_sharedBytes);
        _sharedBytes = null;
        _sharedBytesLength = 0;
        return bytes;
    }

    [DllImport("dprint", EntryPoint = "host_has_cancelled")]
    [WasmImportLinkage]
    private static extern int HostHasCancelled();
}
