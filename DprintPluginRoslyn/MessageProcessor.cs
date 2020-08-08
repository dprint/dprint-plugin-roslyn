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
        private readonly StdInOutReaderWriter _stdInOut;
        private readonly Workspace _workspace;

        public MessageProcessor(StdInOutReaderWriter readerWriter, Workspace workspace)
        {
            _stdInOut = readerWriter;
            _workspace = workspace;
        }

        public void RunBlockingMessageLoop()
        {
            while (true)
            {
                var messageKind = _stdInOut.ReadMessageKind();
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
                    SendInt(2);
                    break;
                case MessageKind.GetPluginInfo:
                    SendString(GetPluginInfo());
                    break;
                case MessageKind.GetLicenseText:
                    SendString(ReadLicenseText());
                    break;
                case MessageKind.GetResolvedConfig:
                    var config = _workspace.GetResolvedConfig();
                    SendString(new Serialization.JsonSerializer().Serialize(config));
                    break;
                case MessageKind.SetGlobalConfig:
                    var globalConfig = new Serialization.JsonSerializer().Deserialize<GlobalConfiguration>(_stdInOut.ReadMessagePartAsString());
                    _workspace.SetGlobalConfig(globalConfig);
                    SendSuccess();
                    break;
                case MessageKind.SetPluginConfig:
                    var pluginConfig = new Serialization.JsonSerializer().Deserialize<Dictionary<string, object>>(_stdInOut.ReadMessagePartAsString());
                    _workspace.SetPluginConfig(pluginConfig);
                    SendSuccess();
                    break;
                case MessageKind.GetConfigDiagnostics:
                    var diagnostics = _workspace.GetDiagnostics();
                    SendString(new Serialization.JsonSerializer().Serialize(diagnostics));
                    break;
                case MessageKind.FormatText:
                    var filePath = _stdInOut.ReadMessagePartAsString();
                    var fileText = _stdInOut.ReadMessagePartAsString();
                    var overrideConfig = new Serialization.JsonSerializer().Deserialize<Dictionary<string, object>>(_stdInOut.ReadMessagePartAsString());
                    var formattedText = _workspace.FormatCode(filePath, fileText, overrideConfig);
                    if (formattedText == fileText)
                        SendInt((int)FormatResult.NoChange);
                    else
                    {
                        SendResponse(
                            new List<object>
                            {
                                (int)FormatResult.Change,
                                Encoding.UTF8.GetBytes(formattedText),
                            }
                        );
                    }
                    break;
                default:
                    throw new NotImplementedException($"Unhandled message kind: {messageKind}");
            }

            return true;
        }

        private void SendString(string value)
        {
            SendResponse(new List<object> { Encoding.UTF8.GetBytes(value) });
        }

        private void SendInt(int value)
        {
            SendResponse(new List<object> { value });
        }

        private void SendSuccess()
        {
            SendResponse(new List<object>());
        }

        // todo: don't box here
        private void SendResponse(IList<object> messageParts)
        {
            _stdInOut.SendMessageKind((int)ResponseKind.Success);
            try
            {
                foreach (var messagePart in messageParts)
                {
                    if (messagePart is byte[])
                        _stdInOut.SendVariableWidth((byte[])messagePart);
                    else if (messagePart is int)
                        _stdInOut.SendInt((int)messagePart);
                    else
                        throw new Exception($"Unknown message part type: {messagePart.GetType().FullName}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.Write($"Error sending message: {ex.Message}");
                Environment.Exit(1); // exit the process... can't send back invalid data at this point
            }
        }

        private void SendErrorResponse(string message)
        {
            var errorBytes = Encoding.UTF8.GetBytes(message);
            _stdInOut.SendMessageKind((int)ResponseKind.Error);
            _stdInOut.SendVariableWidth(errorBytes);
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
            return $"{fileVersionInfo.FileMajorPart}.{fileVersionInfo.FileMinorPart}.{fileVersionInfo.FilePrivatePart}";
        }

        private static string ReadLicenseText()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dprint.Plugins.Roslyn.LICENSE") ?? throw new Exception("Could not find license text.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
