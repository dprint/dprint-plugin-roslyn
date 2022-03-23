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
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn;

public sealed class MessageProcessor : IDisposable
{
    private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
    private readonly Workspace _workspace;
    private readonly Channel<Message> _stdoutChannel;
    private readonly IdGenerator _id = new IdGenerator();
    private readonly JsonSerializer _serializer = new JsonSerializer();

    public MessageProcessor(Workspace workspace)
    {
        _workspace = workspace;
        _stdoutChannel = Channel.CreateUnbounded<Message>();
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    public Task Run(MessageReader reader, MessageWriter writer)
    {
        var disposeToken = _disposeCts.Token;
        // exits when stdin receives a close or either throws a hard exception
        return Task.WhenAny(
            Task.Run(() => RunStdinMessageLoop(reader)),
            Task.Run(() => RunStdoutMessageLoop(_stdoutChannel.Reader, writer, disposeToken))
        );

        static async Task RunStdoutMessageLoop(ChannelReader<Message> channelReader, MessageWriter writer, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    var message = await channelReader.ReadAsync(token);
                    message.Write(writer);
                }
            }
            catch (OperationCanceledException)
            {
                // do nothing
            }
        }
    }

    public void RunStdinMessageLoop(MessageReader reader)
    {
        var tokens = new Dictionary<uint, CancellationTokenSource>();
        while (true)
        {
            var receivedMessage = Message.Read(reader);
            switch (receivedMessage)
            {
                case ErrorResponseMessage message:
                    break;
                case ShutdownMessage message:
                    return; // exit
                case ActiveMessage message:
                    SendSuccessResponse(message.MessageId);
                    break;
                case GetPluginInfoMessage message:
                    SendDataResponse(message.MessageId, GetPluginInfo());
                    break;
                case GetLicenseTextMessage message:
                    SendDataResponse(message.MessageId, ReadLicenseText());
                    break;
                case RegisterConfigMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        var globalConfig = _serializer.Deserialize<GlobalConfiguration>(message.GlobalConfigData);
                        var pluginConfig = _serializer.Deserialize<Dictionary<string, object>>(message.PluginConfigData);
                        _workspace.SetConfig(message.ConfigId, globalConfig, pluginConfig);
                        SendSuccessResponse(message.MessageId);
                    });
                    break;
                case ReleaseConfigMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        _workspace.ReleaseConfig(message.ConfigId);
                        SendSuccessResponse(message.MessageId);
                    });
                    break;
                case GetConfigDiagnosticsMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        _workspace.GetDiagnostics(message.ConfigId);
                        SendSuccessResponse(message.MessageId);
                    });
                    break;
                case GetResolvedConfigMessage message:
                    TryAction(message.MessageId, () =>
                    {
                        _workspace.GetResolvedConfig(message.ConfigId);
                        SendSuccessResponse(message.MessageId);
                    });
                    break;
                case FormatTextMessage message:
                    StartFormatText(message);
                    break;
                case FormatTextResponseMessage message:
                    break;
                case CancelFormatMessage message:
                    if (tokens.TryGetValue(message.OriginalMessageId, out var token))
                        token.Cancel();
                    break;
                case SuccessResponseMessage:
                case DataResponseMessage:
                    // ignore
                    break;
                case HostFormatMessage message:
                    SendError(message.MessageId, "Cannot host format with a plugin.");
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
            // Resolve the options on the current thread to eliminate a
            // race condition where the configId could be released between
            // now and when the task is spawned.
            var overrideConfig = _serializer.Deserialize<Dictionary<string, object>>(message.OverrideConfig);
            var options = _workspace.ResolveOptions(message.ConfigId, overrideConfig);

            Task.Run(() =>
            {
                TryAction(message.MessageId, () =>
                {
                    var filePath = Encoding.UTF8.GetString(message.FilePath);
                    var fileText = Encoding.UTF8.GetString(message.FileText);
                    var range = GetTextSpan(message);
                    var result = _workspace.FormatCode(filePath, fileText, range, options, token);
                });
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
            SendError(originalMessageId, ex);
        }
    }

    private void SendSuccessResponse(uint originalMessageId)
    {
        SendMessage(new SuccessResponseMessage(_id.Next(), originalMessageId));
    }

    private void SendDataResponse(uint originalMessageId, string text)
    {
        SendMessage(new DataResponseMessage(_id.Next(), originalMessageId, Encoding.UTF8.GetBytes(text)));
    }

    private void SendError(uint originalMessageId, Exception ex)
    {
        SendError(originalMessageId, ExceptionToString(ex));
    }

    private void SendError(uint originalMessageId, string text)
    {
        SendMessage(new ErrorResponseMessage(_id.Next(), originalMessageId, Encoding.UTF8.GetBytes(text)));
    }

    private void SendMessage(Message message)
    {
        Task.Run(async () =>
        {
            await _stdoutChannel.Writer.WriteAsync(message);
        });
    }

    private static string ExceptionToString(Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append(ex.Message);
        if (ex.StackTrace != null)
        {
            sb.Append('\n');
            sb.Append(ex.StackTrace);
        }
        return sb.ToString();
    }

    private static string GetPluginInfo()
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append(@"""name"":""dprint-plugin-roslyn"",");
        sb.Append($@"""version"":""{GetAssemblyVersion()}"",");
        sb.Append(@"""configKey"":""roslyn"",");
        sb.Append(@"""fileExtensions"":[""cs"",""vb""],");
        sb.Append(@"""helpUrl"":""https://dprint.dev/plugins/roslyn"",");
        sb.Append(@"""configSchemaUrl"":"""",");
        sb.Append(@"""updateUrl"":""https://plugins.dprint.dev/dprint/dprint-plugin-roslyn/latest.json""");
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
