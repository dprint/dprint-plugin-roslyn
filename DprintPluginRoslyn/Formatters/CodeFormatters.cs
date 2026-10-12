using Dprint.Plugins.Roslyn.Configuration;
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
        if (!formatter.IsEnabled)
            return null;
        // Don't create the source text from the bytes because that computes a checksum
        // using System.Security.Cryptography, which isn't supported in Wasm.
        var encoding = DetectEncoding(code);
        var preambleLength = encoding.Preamble.Length;
        var text = encoding.GetString(code, preambleLength, code.Length - preambleLength);
        var sourceText = SourceText.From(text, encoding);
        var textSpan = GetTextSpan(byteRange);
        var newLine = GetNewLine(formatter, text);
        var options = _options.WithChangedOption(FormattingOptions.NewLine, formatter.RoslynLanguageName, newLine);
        var result = formatter.FormatText(sourceText, textSpan, options, token).ToString();
        // Roslyn only uses the new line for the ones it inserts, so the rest need to be
        // changed here. Leave them alone when formatting a range to not change text outside it.
        if (textSpan is null)
            result = NormalizeNewLines(result, newLine);
        var bytes = GetBytes(result, encoding);
        return bytes.AsSpan().SequenceEqual(code) ? null : bytes;

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

    /// <summary>
    /// Gets the file extensions of the enabled languages.
    /// </summary>
    public IEnumerable<string> GetFileExtensions()
    {
        return _codeFormatters.Where(formatter => formatter.IsEnabled).Select(formatter => formatter.FileExtension);
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
    /// Gets the configured new line with "auto" resolved to the line endings of the text.
    /// </summary>
    private string GetNewLine(ICodeFormatter formatter, string text)
    {
        var newLine = _options.GetOption(FormattingOptions.NewLine, formatter.RoslynLanguageName);
        return newLine == ConfigurationHelpers.AutoNewLine ? DetectNewLine(text) : newLine;
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
    /// Changes every line ending in the text to the new line.
    /// </summary>
    private static string NormalizeNewLines(string text, string newLine)
    {
        // most files only have the new line, so only allocate once a different one is found
        StringBuilder? builder = null;
        var copiedEnd = 0;
        var index = 0;
        while ((index = text.IndexOf('\n', index)) >= 0)
        {
            var start = index > 0 && text[index - 1] == '\r' ? index - 1 : index;
            index++;
            if (text.AsSpan(start, index - start).SequenceEqual(newLine))
                continue;
            builder ??= new StringBuilder(text.Length + 16);
            builder.Append(text, copiedEnd, start - copiedEnd).Append(newLine);
            copiedEnd = index;
        }

        return builder is null ? text : builder.Append(text, copiedEnd, text.Length - copiedEnd).ToString();
    }

    private static byte[] GetBytes(string text, Encoding encoding)
    {
        var preamble = encoding.Preamble;
        var bytes = new byte[preamble.Length + encoding.GetByteCount(text)];
        preamble.CopyTo(bytes);
        encoding.GetBytes(text, 0, text.Length, bytes, preamble.Length);
        return bytes;
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
