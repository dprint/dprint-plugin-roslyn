using System;

namespace Dprint.Plugins.Roslyn.Communication;

public enum MessageKind
{
    Success = 0,
    DataResponse = 1,
    ErrorResponse = 2,
    Shutdown = 3,
    Active = 4,
    GetPluginInfo = 5,
    GetLicenseText = 6,
    RegisterConfig = 7,
    ReleaseConfig = 8,
    GetConfigDiagnostics = 9,
    GetResolvedConfig = 10,
    FormatText = 11,
    FormatTextResponse = 12,
    CancelFormat = 13,
    HostFormat = 14,
}

public abstract class Message
{
    public uint MessageId { get; set; }
    public MessageKind Kind { get; set; }

    public Message(uint messageId, MessageKind kind)
    {
        MessageId = messageId;
        Kind = kind;
    }

    public void Write(MessageWriter writer)
    {
        writer.WriteUint(MessageId);
        writer.WriteUint((uint)Kind);
        WriteBody(writer);
        writer.WriteSuccessBytes();
    }

    protected abstract void WriteBody(MessageWriter writer);

    public static Message Read(MessageReader reader)
    {
        var messageId = reader.ReadUint();
        var kind = reader.ReadUint();
        if (kind > 14)
            throw new ArgumentOutOfRangeException($"Unknown message kind: {kind}");

        var messageKind = (MessageKind)kind;
        Message result = messageKind switch
        {
            MessageKind.Success => new SuccessResponseMessage(messageId, reader.ReadUint()),
            MessageKind.DataResponse => new DataResponseMessage(messageId, reader.ReadUint(), reader.ReadVariableData()),
            MessageKind.ErrorResponse => new ErrorResponseMessage(messageId, reader.ReadUint(), reader.ReadVariableData()),
            MessageKind.Shutdown => new ShutdownMessage(messageId),
            MessageKind.Active => new ActiveMessage(messageId),
            MessageKind.GetPluginInfo => new GetPluginInfoMessage(messageId),
            MessageKind.GetLicenseText => new GetLicenseTextMessage(messageId),
            MessageKind.RegisterConfig => new RegisterConfigMessage(messageId, reader.ReadUint(), reader.ReadVariableData(), reader.ReadVariableData()),
            MessageKind.ReleaseConfig => new ReleaseConfigMessage(messageId, reader.ReadUint()),
            MessageKind.GetConfigDiagnostics => new GetConfigDiagnosticsMessage(messageId, reader.ReadUint()),
            MessageKind.GetResolvedConfig => new GetResolvedConfigMessage(messageId, reader.ReadUint()),
            MessageKind.FormatText => new FormatTextMessage(messageId, reader.ReadVariableData(), reader.ReadUint(), reader.ReadUint(), reader.ReadUint(), reader.ReadVariableData(), reader.ReadVariableData()),
            MessageKind.FormatTextResponse => FormatTextResponseMessage.FromReader(messageId, reader),
            MessageKind.CancelFormat => new CancelFormatMessage(messageId, reader.ReadUint()),
            MessageKind.HostFormat => new HostFormatMessage(messageId, reader.ReadVariableData(), reader.ReadUint(), reader.ReadUint(), reader.ReadVariableData(), reader.ReadVariableData()),
            _ => throw new ArgumentOutOfRangeException($"Unknown message kind: {messageKind}"),
        };

        reader.ReadSuccessBytes();

        return result;
    }
}

public class SuccessResponseMessage : Message
{
    public uint OriginalMessageId { get; }

    public SuccessResponseMessage(uint messageId, uint originalMessageId) : base(messageId, MessageKind.Success)
    {
        OriginalMessageId = originalMessageId;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(OriginalMessageId);
    }
}

public class DataResponseMessage : Message
{
    public uint OriginalMessageId { get; }
    public byte[] Data { get; }

    public DataResponseMessage(uint messageId, uint originalMessageId, byte[] data) : base(messageId, MessageKind.DataResponse)
    {
        OriginalMessageId = originalMessageId;
        Data = data;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(OriginalMessageId);
        writer.WriteVariableWidth(Data);
    }
}

public class ErrorResponseMessage : Message
{
    public uint OriginalMessageId { get; }
    public byte[] Data { get; }

    public ErrorResponseMessage(uint messageId, uint originalMessageId, byte[] data) : base(messageId, MessageKind.ErrorResponse)
    {
        OriginalMessageId = originalMessageId;
        Data = data;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(OriginalMessageId);
        writer.WriteVariableWidth(Data);
    }
}

public class ShutdownMessage : Message
{
    public ShutdownMessage(uint messageId) : base(messageId, MessageKind.Shutdown)
    {
    }

    protected override void WriteBody(MessageWriter writer)
    {
        // no body
    }
}

public class ActiveMessage : Message
{
    public ActiveMessage(uint messageId) : base(messageId, MessageKind.Active)
    {
    }

    protected override void WriteBody(MessageWriter writer)
    {
        // no body
    }
}

public class GetPluginInfoMessage : Message
{
    public GetPluginInfoMessage(uint messageId) : base(messageId, MessageKind.GetPluginInfo)
    {
    }

    protected override void WriteBody(MessageWriter writer)
    {
        // no body
    }
}

public class GetLicenseTextMessage : Message
{
    public GetLicenseTextMessage(uint messageId) : base(messageId, MessageKind.GetLicenseText)
    {
    }

    protected override void WriteBody(MessageWriter writer)
    {
        // no body
    }
}

public class RegisterConfigMessage : Message
{
    public uint ConfigId { get; }
    public byte[] GlobalConfigData { get; }
    public byte[] PluginConfigData { get; }

    public RegisterConfigMessage(uint messageId, uint configId, byte[] globalConfigData, byte[] pluginConfigData) : base(messageId, MessageKind.RegisterConfig)
    {
        ConfigId = configId;
        GlobalConfigData = globalConfigData;
        PluginConfigData = pluginConfigData;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(ConfigId);
        writer.WriteVariableWidth(GlobalConfigData);
        writer.WriteVariableWidth(PluginConfigData);
    }
}

public class ReleaseConfigMessage : Message
{
    public uint ConfigId { get; }

    public ReleaseConfigMessage(uint messageId, uint configId) : base(messageId, MessageKind.ReleaseConfig)
    {
        ConfigId = configId;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(ConfigId);
    }
}

public class GetConfigDiagnosticsMessage : Message
{
    public uint ConfigId { get; }

    public GetConfigDiagnosticsMessage(uint messageId, uint configId) : base(messageId, MessageKind.GetConfigDiagnostics)
    {
        ConfigId = configId;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(ConfigId);
    }
}

public class GetResolvedConfigMessage : Message
{
    public uint ConfigId { get; }

    public GetResolvedConfigMessage(uint messageId, uint configId) : base(messageId, MessageKind.GetResolvedConfig)
    {
        ConfigId = configId;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(ConfigId);
    }
}

public class FormatTextMessage : Message
{
    public byte[] FilePath { get; }
    public uint StartByteIndex { get; }
    public uint EndByteIndex { get; }
    public uint ConfigId { get; }
    public byte[] OverrideConfig { get; }
    public byte[] FileText { get; }

    public FormatTextMessage(uint messageId, byte[] filePath, uint startByteIndex, uint endByteIndex, uint configId, byte[] overrideConfig, byte[] fileText) : base(messageId, MessageKind.FormatText)
    {
        FilePath = filePath;
        StartByteIndex = startByteIndex;
        EndByteIndex = endByteIndex;
        ConfigId = configId;
        OverrideConfig = overrideConfig;
        FileText = fileText;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteVariableWidth(FilePath);
        writer.WriteUint(StartByteIndex);
        writer.WriteUint(EndByteIndex);
        writer.WriteUint(ConfigId);
        writer.WriteVariableWidth(OverrideConfig);
        writer.WriteVariableWidth(FileText);
    }
}

public class FormatTextResponseMessage : Message
{
    public byte[]? Content { get; }

    public FormatTextResponseMessage(uint messageId, byte[]? content) : base(messageId, MessageKind.FormatTextResponse)
    {
        Content = content;
    }

    public static FormatTextResponseMessage FromReader(uint messageId, MessageReader reader)
    {
        var kind = reader.ReadUint();
        return kind switch
        {
            0 => new FormatTextResponseMessage(messageId, null),
            1 => new FormatTextResponseMessage(messageId, reader.ReadVariableData()),
            _ => throw new Exception($"Unknown message kind: {kind}"),
        };
    }

    protected override void WriteBody(MessageWriter writer)
    {
        if (Content == null)
        {
            writer.WriteUint(0);
        }
        else
        {
            writer.WriteUint(1);
            writer.WriteVariableWidth(Content);
        }
    }
}

public class CancelFormatMessage : Message
{
    public uint OriginalMessageId { get; }

    public CancelFormatMessage(uint messageId, uint originalMessageId) : base(messageId, MessageKind.CancelFormat)
    {
        OriginalMessageId = originalMessageId;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteUint(OriginalMessageId);
    }
}

public class HostFormatMessage : Message
{
    public byte[] FilePath { get; }
    public uint StartByteIndex { get; }
    public uint EndByteIndex { get; }
    public byte[] OverrideConfig { get; }
    public byte[] FileText { get; }

    public HostFormatMessage(uint messageId, byte[] filePath, uint startByteIndex, uint endByteIndex, byte[] overrideConfig, byte[] fileText) : base(messageId, MessageKind.FormatText)
    {
        FilePath = filePath;
        StartByteIndex = startByteIndex;
        EndByteIndex = endByteIndex;
        OverrideConfig = overrideConfig;
        FileText = fileText;
    }

    protected override void WriteBody(MessageWriter writer)
    {
        writer.WriteVariableWidth(FilePath);
        writer.WriteUint(StartByteIndex);
        writer.WriteUint(EndByteIndex);
        writer.WriteVariableWidth(OverrideConfig);
        writer.WriteVariableWidth(FileText);
    }
}
