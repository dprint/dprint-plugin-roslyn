using Dprint.Plugins.Roslyn.Communication;
using Dprint.Plugins.Roslyn.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Dprint.Plugins.Roslyn
{
    enum MessageKind
    {
        GetPluginSchemaVersion = 0,
        GetPluginInfo = 1,
        GetLicenseText = 2,
        GetResolvedConfig = 3,
        SetGlobalConfig = 4,
        SetPluginConfig = 5,
        GetConfigDiagnostics = 6,
        FormatText = 7,
        Close = 8,
    }

    enum ResponseKind
    {
        Success = 0,
        Error = 1,
    }

    enum FormatResult
    {
        NoChange = 0,
        Change = 1,
    }

    public class MessageProcessor
    {
        private readonly StdIoMessenger _messenger;
        private readonly Workspace _workspace;

        public MessageProcessor(StdIoMessenger messenger, Workspace workspace)
        {
            _messenger = messenger;
            _workspace = workspace;
        }

        public void RunBlockingMessageLoop()
        {
            while (true)
            {
                var messageKind = _messenger.ReadCode();
                try
                {
                    if (!HandleMessageKind((MessageKind)messageKind))
                        return;
                }
                catch (Exception ex)
                {
                    var sb = new StringBuilder();
                    sb.Append(ex.Message);
                    if (ex.StackTrace != null)
                    {
                        sb.Append("\n");
                        sb.Append(ex.StackTrace);
                    }
                    SendErrorResponse(sb.ToString());
                }
            }
        }

        private bool HandleMessageKind(MessageKind messageKind)
        {
            switch (messageKind)
            {
                case MessageKind.Close:
                    return false;
                case MessageKind.GetPluginSchemaVersion:
                    _messenger.ReadZeroPartMessage();
                    SendSuccess(MessagePart.FromInt(3));
                    break;
                case MessageKind.GetPluginInfo:
                    _messenger.ReadZeroPartMessage();
                    SendSuccess(MessagePart.FromString(GetPluginInfo()));
                    break;
                case MessageKind.GetLicenseText:
                    _messenger.ReadZeroPartMessage();
                    SendSuccess(MessagePart.FromString(ReadLicenseText()));
                    break;
                case MessageKind.GetResolvedConfig:
                    {
                        _messenger.ReadZeroPartMessage();
                        var config = _workspace.GetResolvedConfig();
                        SendSuccess(MessagePart.FromString(new Serialization.JsonSerializer().Serialize(config)));
                        break;
                    }
                case MessageKind.SetGlobalConfig:
                    {
                        var message = _messenger.ReadSinglePartMessage();
                        var globalConfig = new Serialization.JsonSerializer().Deserialize<GlobalConfiguration>(message.IntoString());
                        _workspace.SetGlobalConfig(globalConfig);
                        SendSuccess();
                        break;
                    }
                case MessageKind.SetPluginConfig:
                    {
                        var message = _messenger.ReadSinglePartMessage();
                        var pluginConfig = new Serialization.JsonSerializer().Deserialize<Dictionary<string, object>>(message.IntoString());
                        _workspace.SetPluginConfig(pluginConfig);
                        SendSuccess();
                        break;
                    }
                case MessageKind.GetConfigDiagnostics:
                    {
                        _messenger.ReadZeroPartMessage();
                        var diagnostics = _workspace.GetDiagnostics();
                        SendSuccess(MessagePart.FromString(new Serialization.JsonSerializer().Serialize(diagnostics)));
                        break;
                    }
                case MessageKind.FormatText:
                    {
                        var message = _messenger.ReadMultiPartMessage(3);
                        var filePath = message[0].IntoString();
                        var fileText = message[1].IntoString();
                        var overrideConfig = new Serialization.JsonSerializer().Deserialize<Dictionary<string, object>>(message[2].IntoString());
                        var formattedText = _workspace.FormatCode(filePath, fileText, overrideConfig);
                        if (formattedText == fileText)
                            SendSuccess(MessagePart.FromInt((int)FormatResult.NoChange));
                        else
                        {
                            SendSuccess(
                                MessagePart.FromInt((int)FormatResult.Change),
                                MessagePart.FromString(formattedText)
                            );
                        }
                        break;
                    }
                default:
                    throw new NotImplementedException($"Unhandled message kind: {messageKind}");
            }

            return true;
        }

        private void SendSuccess(params MessagePart[] messageParts)
        {
            _messenger.SendMessage((int)ResponseKind.Success, messageParts);
        }

        private void SendErrorResponse(string message)
        {
            _messenger.SendMessage((int)ResponseKind.Error, MessagePart.FromString(message));
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
            sb.Append(@"""configSchemaUrl"":""""");
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
}
