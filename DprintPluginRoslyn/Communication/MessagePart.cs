using System.Text;

namespace Dprint.Plugins.Roslyn.Communication
{
  public abstract class MessagePart
  {
    public abstract byte[] GetBytes();

    public string IntoString()
    {
      return Encoding.UTF8.GetString(GetBytes());
    }

    public static MessagePart FromString(string text)
    {
      return new VariableMessagePart(Encoding.UTF8.GetBytes(text));
    }

    public static MessagePart FromInt(int value)
    {
      return new IntegerMessagePart(value);
    }
  }

  internal class VariableMessagePart : MessagePart
  {
    public VariableMessagePart(byte[] data)
    {
      Data = data;
    }

    public byte[] Data { get; }

    public override byte[] GetBytes() => Data;
  }

  internal class IntegerMessagePart : MessagePart
  {
    public IntegerMessagePart(int value)
    {
      Value = value;
    }

    public int Value { get; }

    public override byte[] GetBytes() => BigEndianBitConverter.GetBytes((uint)Value);
  }
}
