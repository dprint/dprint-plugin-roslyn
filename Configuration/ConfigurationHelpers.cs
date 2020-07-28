using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Linq;

namespace Dprint.Plugins.Roslyn.Configuration
{
    public static class ConfigurationHelpers
    {
        public static void HandleGlobalConfig(ConfigurationResolutionContext context, string configPrefix, string? language)
        {
            if (context.RemoveInt($"{configPrefix}indentWidth", out var indentWidth))
            {
                context.ChangeOption(FormattingOptions.IndentationSize, language, indentWidth);
                context.ChangeOption(FormattingOptions.TabSize, language, indentWidth);
            }

            if (context.RemoveBool($"{configPrefix}useTabs", out var useTabs))
            {
                context.ChangeOption(FormattingOptions.UseTabs, language, useTabs);
            }

            {
                var propertyName = $"{configPrefix}newLineKind";
                if (context.RemoveString(propertyName, out var newLineKind))
                {
                    context.ChangeOption(FormattingOptions.NewLine, language, GetNewLineKind(context, propertyName, newLineKind));
                }
            }
        }

        public static void HandlePluginConfig(ConfigurationResolutionContext context, Type formattingOptionsType, string languageKeyPrefix)
        {
            foreach (var key in context.GetConfigKeys().Where(key => key.StartsWith(languageKeyPrefix)))
            {
                var value = context.RemoveValue(key);
                var keyEnd = key.Substring(languageKeyPrefix.Length);
                var formattingOptionsPropertyName = keyEnd.Substring(0, 1).ToUpper() + keyEnd.Substring(1); // convert to pascale case

                var propertyInfo = formattingOptionsType.GetProperty(formattingOptionsPropertyName);
                var optionObject = propertyInfo?.GetValue(null, null);
                if (propertyInfo is null || optionObject is null)
                {
                    context.AddDiagnostic(key, $"Could not find property on {formattingOptionsType.Name} with name '{formattingOptionsPropertyName}'");
                    continue;
                }

                if (value is bool)
                    HandleOption(key, optionObject, (bool)value);
                else if (value is int)
                    HandleOption(key, optionObject, (int)value);
                else if (value is string)
                {
                    var genericArgumentType = propertyInfo.PropertyType.GetGenericArguments()[0];
                    var stringValue = (string)value;
                    stringValue = stringValue.Substring(0, 1).ToUpper() + stringValue.Substring(1);
                    if (Enum.TryParse(genericArgumentType, stringValue, out var result))
                    {
                        IOption option = (IOption)optionObject;
                        context.ChangeOption(new OptionKey(option), result!);
                    }
                    else
                    {
                        context.AddDiagnostic(key, $"Could not parse string '{(string)value}' to enum: {genericArgumentType.FullName}");
                    }
                }
                else
                {
                    context.AddDiagnostic(key, $"Unknown value type: {value?.GetType().Name}");
                }
            }

            void HandleOption<T>(string propertyName, object optionObject, T value)
            {
                var option = optionObject as Option<T>;
                if (option is null)
                {
                    context.AddDiagnostic(propertyName, $"Property value was not expected to be a {typeof(T).Name}.");
                    return;
                }
                context.ChangeOption(option, (T) value);
            }
        }

        public static string GetNewLineKind(ConfigurationResolutionContext context, string propertyName, string newLineKind)
        {
            switch (newLineKind)
            {
                case "lf":
                    return "\n";
                case "crlf":
                case "auto": // todo: do this on a per file basis
                    return "\r\n";
                case "system":
                    return Environment.NewLine;
                default:
                    context.AddDiagnostic(propertyName, $"Unknown new line kind: {newLineKind}");
                    return "\r\n";
            }
        }
    }
}
