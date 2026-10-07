using Dprint.Plugins.Roslyn.Configuration;
using Dprint.Plugins.Roslyn.Utils;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

public class CodeFormatters
{
    private readonly ICodeFormatter[] _codeFormatters;
    private readonly OptionSet _options;

    public CodeFormatters(ICodeFormatter[] codeFormatters, OptionSet options)
    {
        _codeFormatters = codeFormatters;
        _options = options;
    }

    public byte[]? FormatCode(string filePath, byte[] code, (int Start, int End)? byteRange, CancellationToken token)
    {
        var formatter = _codeFormatters.FirstOrDefault(formatter => formatter.ShouldFormat(filePath));
        if (formatter is null)
            throw new Exception($"Could not find formatter for file path: {filePath}");
        // Don't create the source text from the bytes because that computes a checksum
        // using System.Security.Cryptography, which isn't supported in Wasm.
        var encoding = DetectEncoding(code);
        var preambleLength = encoding.Preamble.Length;
        var text = encoding.GetString(code, preambleLength, code.Length - preambleLength);
        var sourceText = SourceText.From(text, encoding);
        var result = formatter.FormatText(sourceText, GetTextSpan(byteRange), GetOptionsForText(formatter, text), token);
        var bytes = result.GetBytes();
        return bytes.SequenceEqual(code) ? null : bytes;

        TextSpan? GetTextSpan((int Start, int End)? byteRange)
        {
            if (byteRange is not { } range || range.Start == 0 && range.End == code.Length)
                return null;
            return TextSpan.FromBounds(ByteToCharIndex(range.Start), ByteToCharIndex(range.End));
        }

        int ByteToCharIndex(int byteIndex)
        {
            var index = Math.Clamp(byteIndex, preambleLength, code.Length);
            return encoding.GetCharCount(code, preambleLength, index - preambleLength);
        }
    }

    public Dictionary<string, object> GetResolvedConfig()
    {
        var config = new Dictionary<string, object>();
        foreach (var formatter in _codeFormatters)
        {
            foreach (var (key, value) in formatter.GetResolvedConfig(_options))
                config[key] = value;
        }

        return config;
    }

    /// <summary>
    /// Gets the options with an "auto" new line resolved to the line endings of the text.
    /// </summary>
    private OptionSet GetOptionsForText(ICodeFormatter formatter, string text)
    {
        var language = formatter.RoslynLanguageName;
        if (_options.GetOption(FormattingOptions.NewLine, language) != ConfigurationHelpers.AutoNewLine)
            return _options;
        return _options.WithChangedOption(FormattingOptions.NewLine, language, DetectNewLine(text));
    }

    /// <summary>
    /// Gets the last line ending in the text, defaulting to a line feed.
    /// </summary>
    private static string DetectNewLine(string text)
    {
        var index = text.LastIndexOf('\n');
        return index > 0 && text[index - 1] == '\r' ? "\r\n" : "\n";
    }

    /// <summary>
    /// Gets the encoding based on the byte order mark, defaulting to UTF-8.
    /// </summary>
    private static Encoding DetectEncoding(byte[] code)
    {
        Encoding[] encodingsWithPreamble =
        [
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
            new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true),
            new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true),
        ];
        return encodingsWithPreamble.FirstOrDefault(encoding => code.AsSpan().StartsWith(encoding.Preamble))
            ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }
}
