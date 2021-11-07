using System;
using System.Collections.Generic;
using System.Linq;

namespace Dprint.Plugins.Roslyn.Communication
{
  public class StdIoMessenger
  {
    private readonly StdIoReaderWriter _readerWriter;

    public StdIoMessenger(StdIoReaderWriter readerWriter)
    {
      _readerWriter = readerWriter;
    }

    public int ReadCode()
    {
      return _readerWriter.ReadInt();
    }

    public List<MessagePart> ReadMultiPartMessage(int partCount)
    {
      var parts = new List<MessagePart>(partCount);
      for (var i = 0; i < partCount; i++)
        parts.Add(new VariableMessagePart(_readerWriter.ReadVariableData()));
      _readerWriter.ReadSuccessBytes();
      return parts;
    }

    public MessagePart ReadSinglePartMessage()
    {
      return ReadMultiPartMessage(1).Single();
    }

    public void ReadZeroPartMessage()
    {
      _readerWriter.ReadSuccessBytes();
    }

    public void SendMessage(int code, params MessagePart[] parts)
    {
      try
      {
        _readerWriter.SendInt(code);
        foreach (var part in parts)
        {
          switch (part)
          {
            case VariableMessagePart variablePart:
              _readerWriter.SendVariableWidth(variablePart.Data);
              break;
            case IntegerMessagePart intPart:
              _readerWriter.SendInt(intPart.Value);
              break;
            default:
              throw new NotImplementedException($"Not implemented message part: {part.GetType()}");
          }
        }
        _readerWriter.SendSuccessBytes();
      }
      catch (Exception ex)
      {
        Console.Error.Write($"Catastrophic error sending message: {ex.Message}");
        Environment.Exit(1); // exit the process... can't send back invalid data at this point
      }
    }
  }
}
