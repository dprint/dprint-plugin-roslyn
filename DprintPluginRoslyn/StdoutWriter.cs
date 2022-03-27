using Dprint.Plugins.Roslyn.Communication;
using Dprint.Plugins.Roslyn.Utils;
using System;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Dprint.Plugins.Roslyn;

/// <summary>
/// Sends messages on stdout.
/// </summary>
public sealed class StdoutWriter : IDisposable
{
  private readonly IdGenerator _id = new IdGenerator();
  private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
  private readonly ChannelWriter<Message> _writer;

  public StdoutWriter(MessageWriter writer)
  {
    var channel = Channel.CreateUnbounded<Message>();
    _writer = channel.Writer;
    var reader = channel.Reader;
    var token = _disposeCts.Token;

    // run all of the writing to stdout on a single thread
    Task.Run(async () =>
    {
      try
      {
        while (true)
        {
          var message = await reader.ReadAsync(token);
          message.Write(writer);
        }
      }
      catch (OperationCanceledException)
      {
            // do nothing
          }
    });
  }

  public void Dispose()
  {
    _disposeCts.Cancel();
    _disposeCts.Dispose();
  }

  public void SendSuccessResponse(uint originalMessageId)
  {
    SendMessage(new SuccessResponseMessage(_id.Next(), originalMessageId));
  }

  public void SendDataResponse(uint originalMessageId, string text)
  {
    SendDataResponse(originalMessageId, Encoding.UTF8.GetBytes(text));
  }

  public void SendDataResponse(uint originalMessageId, byte[] data)
  {
    SendMessage(new DataResponseMessage(_id.Next(), originalMessageId, data));
  }

  public void SendError(uint originalMessageId, Exception ex)
  {
    SendError(originalMessageId, ExceptionToString(ex));
  }

  public void SendError(uint originalMessageId, string text)
  {
    SendMessage(new ErrorResponseMessage(_id.Next(), originalMessageId, Encoding.UTF8.GetBytes(text)));
  }

  public void SendFormatTextResponse(uint originalMessageId, string? text)
  {
    SendMessage(new FormatTextResponseMessage(_id.Next(), originalMessageId, text == null ? null : Encoding.UTF8.GetBytes(text)));
  }

  private void SendMessage(Message message)
  {
    Task.Run(() => _writer.WriteAsync(message));
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
}
