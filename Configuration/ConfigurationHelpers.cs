using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dprint.Plugins.Roslyn.Configuration
{
    public static class ConfigurationHelpers
    {
        public static void HandleGlobalConfig(ConfigurationResolutionContext context, string configPrefix, string language)
        {
            HandleGlobalConfig(context, configPrefix, new[] { language });
        }

        public static void HandleGlobalConfig(ConfigurationResolutionContext context, string configPrefix, IEnumerable<string> languages)
        {
            if (context.RemoveInt($"{configPrefix}indentWidth", out var indentWidth))
            {
                context.ChangeOption(FormattingOptions.IndentationSize, languages, indentWidth);
                context.ChangeOption(FormattingOptions.TabSize, languages, indentWidth);
            }

            if (context.RemoveBool($"{configPrefix}useTabs", out var useTabs))
            {
                context.ChangeOption(FormattingOptions.UseTabs, languages, useTabs);
            }

            {
                var propertyName = $"{configPrefix}newLineKind";
                if (context.RemoveString(propertyName, out var newLineKind))
                {
                    context.ChangeOption(FormattingOptions.NewLine, languages, GetNewLineKind(context, propertyName, newLineKind));
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
                var genericArgumentType = propertyInfo.PropertyType.GetGenericArguments()[0];

                if (value is string)
                {
                    var stringValue = (string)value;
                    stringValue = stringValue.Substring(0, 1).ToUpper() + stringValue.Substring(1);
                    if (!genericArgumentType.IsEnum)
                    {
                        AddMismatchedTypesDiagnostic(key, genericArgumentType, value);
                    }
                    else if (Enum.TryParse(genericArgumentType, stringValue, out var result))
                    {
                        IOption option = (IOption)optionObject;
                        context.ChangeOption(new OptionKey(option), result!);
                    }
                    else
                    {
                        context.AddDiagnostic(key, $"Could not parse string '{(string)value}' to enum: {genericArgumentType.FullName}");
                    }
                }
                else if (genericArgumentType != value?.GetType())
                {
                    AddMismatchedTypesDiagnostic(key, genericArgumentType, value);
                }
                else if (value is bool)
                {
                    HandleOption(key, optionObject, (bool)value);
                }
                else if (value is int)
                {
                    HandleOption(key, optionObject, (int)value);
                }
                else
                {
                    context.AddDiagnostic(key, $"Unhandled value type: {value?.GetType().Name}.");
                }
            }

            void AddMismatchedTypesDiagnostic(string propertyName, Type expectedType, object? value)
            {
                context.AddDiagnostic(
                    propertyName,
                    $"Property value was expected to be {expectedType.Name}, but was {value?.GetType().Name}."
                );
            }

            void HandleOption<T>(string propertyName, object optionObject, T value)
            {
                context.ChangeOption((Option<T>) optionObject, (T) value);
            }
        }

        public static IEnumerable<(string, object)> GetResolvedGlobalConfig(OptionSet options, string configPrefix, string language)
        {
            yield return ($"{configPrefix}indentationSize", options.GetOption(FormattingOptions.IndentationSize, language)!);
            yield return ($"{configPrefix}tabSize", options.GetOption(FormattingOptions.TabSize, language)!);
            yield return ($"{configPrefix}useTabs", options.GetOption(FormattingOptions.UseTabs, language)!);
            yield return ($"{configPrefix}newLine", options.GetOption(FormattingOptions.NewLine, language)!);
        }

        public static IEnumerable<(string, object)> GetResolvedPluginConfig(OptionSet options, Type formattingOptionsType, string languageKeyPrefix)
        {
            foreach (var prop in formattingOptionsType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                var optionObject = prop.GetValue(null, null)!;
                var value = options.GetOption(new OptionKey((IOption)optionObject))!;
                yield return (GetPropName(prop, languageKeyPrefix), value);
            }

            static string GetPropName(PropertyInfo prop, string languageKeyPrefix)
            {
                return $"{languageKeyPrefix}{prop.Name.Substring(0, 1).ToLower()}{prop.Name.Substring(1)}";
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
