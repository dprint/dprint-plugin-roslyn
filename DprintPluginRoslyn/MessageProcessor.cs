using Dprint.Plugins.Roslyn.Communication;
using Dprint.Plugins.Roslyn.Configuration;
using Dprint.Plugins.Roslyn.Serialization;
using Dprint.Plugins.Roslyn.Utils;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn;

public class MessageProcessor
{
    private readonly Workspace _workspace;
    private readonly StdoutWriter _stdoutWriter;
    private readonly JsonSerializer _serializer = new JsonSerializer();
    private readonly ConcurrentStorage<CancellationTokenSource> _tokens = new();

    public MessageProcessor(StdoutWriter writer)
    {
        _workspace = new Workspace();
        _stdoutWriter = writer;
    }

    public ShutdownMessage RunStdinMessageLoop(MessageReader reader)
    {
        while (true)
        {
            var receivedMessage = Message.Read(reader);
            switch (receivedMessage)
            {
                case ErrorResponseMessage message:
                    break;
                case ShutdownMessage message:
                    return message; // exit
                case ActiveMessage message:
                    _stdoutWriter.SendSuccessResponse(message.MessageId);
                    break;
                case GetPluginInfoMessage message:
                    _stdoutWriter.SendDataResponse(message.MessageId, GetPluginInfo());
                    break;
                case GetLicenseTextMessage message:
                    _stdoutWriter.SendDataResponse(message.MessageId, ReadLicenseText());
                    break;
                case RegisterConfigMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        var globalConfig = _serializer.Deserialize<GlobalConfiguration>(message.GlobalConfigData);
                        var pluginConfig = _serializer.Deserialize<Dictionary<string, object>>(message.PluginConfigData);
                        _workspace.SetConfig(message.ConfigId, globalConfig, pluginConfig);
                        _stdoutWriter.SendSuccessResponse(message.MessageId);
                    });
                    break;
                case ReleaseConfigMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        _workspace.ReleaseConfig(message.ConfigId);
                        _stdoutWriter.SendSuccessResponse(message.MessageId);
                    });
                    break;
                case GetConfigDiagnosticsMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        var jsonDiagnostics = _serializer.Serialize(_workspace.GetDiagnostics(message.ConfigId));
                        _stdoutWriter.SendDataResponse(message.MessageId, jsonDiagnostics);
                    });
                    break;
                case GetFileMatchingInfo message:
                    _stdoutWriter.SendDataResponse(message.MessageId, GetFileMatchingInfo());
                    break;
                case GetResolvedConfigMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        var formatters = _workspace.GetFormatters(message.ConfigId);
                        var jsonConfig = _serializer.Serialize(formatters.GetResolvedConfig());
                        _stdoutWriter.SendDataResponse(message.MessageId, jsonConfig);
                    });
                    break;
                case CheckConfigUpdatesMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        _stdoutWriter.SendDataResponse(message.MessageId, "{ \"changes\": [] }");
                    });
                    break;
                case FormatTextMessage message:
                    StartFormatText(message);
                    break;
                case CancelFormatMessage message:
                    _tokens.Take(message.OriginalMessageId)?.Cancel();
                    break;
                case FormatTextResponseMessage message:
                    // ignore, host formatting is not used by this plugin
                    break;
                case SuccessResponseMessage:
                case DataResponseMessage:
                    // ignore
                    break;
                case HostFormatMessage message:
                    _stdoutWriter.SendError(message.MessageId, "Cannot host format with a plugin.");
                    break;
                default:
                    // exit the process
                    throw new NotImplementedException($"Unimlemented message: {receivedMessage.GetType().FullName}");
            }
        }
    }

    private void StartFormatText(FormatTextMessage message)
    {
        TryAction(message.MessageId, () =>
        {
            // workspace is not thread safe, so keep it here
            var overrideConfig = _serializer.Deserialize<Dictionary<string, object>>(message.OverrideConfig);
            var formatters = _workspace.GetFormatters(message.ConfigId, overrideConfig);
            var filePath = Encoding.UTF8.GetString(message.FilePath);
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            _tokens.StoreValue(message.MessageId, cts);

            // SAFETY - Ensure everything sent here is thread safe
            Task.Run(() =>
            {
                TryAction(message.MessageId, () =>
                {
                    var range = GetTextSpan(message);
                    var result = formatters.FormatCode(filePath, message.FileText, range, token);
                    _stdoutWriter.SendFormatTextResponse(message.MessageId, result);
                });

                // release the token
                _tokens.Take(message.MessageId);
            });
        });

        static TextSpan? GetTextSpan(FormatTextMessage message)
        {
            if (message.StartByteIndex == 0 && message.FileText.Length == message.EndByteIndex)
                return null;
            return new TextSpan(
                ByteToCharIndex(message.StartByteIndex, message.FileText),
                ByteToCharIndex(message.EndByteIndex, message.FileText)
            );
        }

        static int ByteToCharIndex(uint index, byte[] text)
        {
            // seems very inefficient. Better way?
            return Encoding.UTF8.GetString(text[0..(int)index]).Length;
        }
    }

    private void TryAction(uint originalMessageId, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _stdoutWriter.SendError(originalMessageId, ex);
        }
    }

    private static string GetPluginInfo()
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append(@"""name"":""dprint-plugin-roslyn"",");
        sb.Append($@"""version"":""{GetAssemblyVersion()}"",");
        sb.Append(@"""configKey"":""roslyn"",");
        sb.Append(@"""helpUrl"":""https://dprint.dev/plugins/roslyn"",");
        sb.Append(@"""configSchemaUrl"":"""",");
        sb.Append(@"""updateUrl"":""https://plugins.dprint.dev/dprint/dprint-plugin-roslyn/latest.json""");
        sb.Append("}");
        return sb.ToString();
    }

    private static string GetFileMatchingInfo()
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append(@"""fileExtensions"":[""cs"",""vb""]");
        sb.Append("}");
        return sb.ToString();
    }

    private static string GetAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return $"{fileVersionInfo.FileMajorPart}.{fileVersionInfo.FileMinorPart}.{fileVersionInfo.FileBuildPart}";
    }

    private static string ReadLicenseText()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dprint.Plugins.Roslyn.LICENSE") ?? throw new Exception("Could not find license text.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
