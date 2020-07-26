using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dprint.Plugins.Roslyn.Communication;
using Dprint.Plugins.Roslyn.Formatters;

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

    class Program
    {
        private static ICodeFormatter[] _codeFormatters = new ICodeFormatter[]
        {
            new CSharpCodeFormatter(),
            new VisualBasicCodeFormatter(),
        };

        static void Main(string[] args)
        {
            var stdInOut = new StdInOutReaderWriter();
            while (true)
            {
                var messageKind = stdInOut.ReadMessageKind();
                try
                {
                    if (!HandleMessageKind(stdInOut, (MessageKind) messageKind))
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
                    SendErrorResponse(stdInOut, sb.ToString());
                }
            }
        }

        private static bool HandleMessageKind(StdInOutReaderWriter stdInOut, MessageKind messageKind)
        {
            switch (messageKind)
            {
                case MessageKind.Close:
                    return false;
                case MessageKind.GetPluginSchemaVersion:
                    SendInt(stdInOut, 1);
                    break;
                case MessageKind.GetPluginInfo:
                    SendString(stdInOut, GetPluginInfo());
                    break;
                case MessageKind.GetLicenseText:
                    SendString(stdInOut, ReadLicenseText());
                    break;
                case MessageKind.GetResolvedConfig:
                    SendString(stdInOut, "{}");
                    break;
                case MessageKind.SetGlobalConfig:
                    stdInOut.ReadMessagePart(); // todo: handle
                    SendSuccess(stdInOut);
                    break;
                case MessageKind.SetPluginConfig:
                    stdInOut.ReadMessagePart(); // todo: handle
                    SendSuccess(stdInOut);
                    break;
                case MessageKind.GetConfigDiagnostics:
                    SendString(stdInOut, "[]");
                    break;
                case MessageKind.FormatText:
                    var filePath = stdInOut.ReadMessagePartAsString();
                    var fileText = stdInOut.ReadMessagePartAsString();
                    var formattedText = FormatCode(filePath, fileText);
                    if (formattedText == fileText)
                        SendInt(stdInOut, (int)FormatResult.NoChange);
                    else
                    {
                        SendResponse(
                            stdInOut,
                            new List<byte[]>
                            {
                                BigEndianBitConverter.GetBytes((uint) FormatResult.Change),
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

        private static void SendString(StdInOutReaderWriter stdInOut, string value)
        {
            SendResponse(stdInOut, new List<byte[]> { Encoding.UTF8.GetBytes(value) });
        }

        private static void SendInt(StdInOutReaderWriter stdInOut, int value)
        {
            SendResponse(stdInOut, new List<byte[]> { BigEndianBitConverter.GetBytes((uint)value) });
        }

        private static void SendSuccess(StdInOutReaderWriter stdInOut)
        {
            SendResponse(stdInOut, new List<byte[]>());
        }

        private static void SendResponse(StdInOutReaderWriter stdInOut, IList<byte[]> messageParts)
        {
            stdInOut.SendMessageKind((int)ResponseKind.Success);
            foreach (var messagePart in messageParts)
                stdInOut.SendMessagePart(messagePart);
        }

        private static void SendErrorResponse(StdInOutReaderWriter stdInOut, string message)
        {
            stdInOut.SendMessageKind((int)ResponseKind.Error);
            stdInOut.SendMessagePart(Encoding.UTF8.GetBytes(message));
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
            return fileVersionInfo.FileVersion;
        }

        private static string ReadLicenseText()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dprint.Plugins.Roslyn.LICENSE");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static string FormatCode(string filePath, string code)
        {
            var formatter = _codeFormatters.FirstOrDefault(formatter => formatter.ShouldFormat(filePath));
            if (formatter == null)
                throw new Exception($"Could not find formatter for file path: {filePath}");
            return formatter.FormatText(code);
        }
    }
}
