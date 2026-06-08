using System.IO;
using Microsoft.CodeAnalysis.Text;

namespace Dprint.Plugins.Roslyn.Utils;

public static class SourceTextExtensions
{
    public static byte[] GetBytes(this SourceText sourceText)
    {
        var ms = new MemoryStream();
        using var sw = new StreamWriter(ms, sourceText.Encoding);
        sw.Write(sourceText.ToString());
        sw.Flush();
        return ms.ToArray();
    }
}
