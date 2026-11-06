using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Options;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dprint.Plugins.Roslyn.Configuration;

/// <summary>
/// Generates the JSON schema of the plugin's configuration, which is published with each release.
/// </summary>
[TestFixture]
public class SchemaTests
{
    private const string UpdateEnvVarName = "UPDATE";
    private static readonly string[] _languagePrefixes = ["csharp.", "visualBasic."];

    private const string GeneralSection = "General";
    private const string IndentationSection = "Indentation";
    private const string BracesSection = "Braces";
    private const string NewLinesSection = "New lines";
    private const string SpacesSection = "Spaces";
    private const string ParenthesesSection = "Parentheses and brackets";
    private const string WrappingSection = "Wrapping";
    private const string OtherSection = "Other";

    private static readonly (string Title, string Description)[] _sections =
    [
        (GeneralSection, "The indentation and newlines of every language, and the languages that are formatted."),
        (IndentationSection, "The indentation of blocks, braces, `switch` statements, and labels in C#."),
        (BracesSection, "Whether an opening brace goes on a new line in C#."),
        (NewLinesSection, "Whether keywords, query clauses, and the members of initializers go on a new line in C#."),
        (SpacesSection, "The spaces around punctuation, keywords, and operators in C#."),
        (ParenthesesSection, "The spaces in and before parentheses and square brackets in C#."),
        (WrappingSection, "Whether code that's on a single line stays there in C#."),
        // Roslyn options that aren't documented below
        (OtherSection, "The other options of Roslyn for C#."),
    ];

    private static readonly string[] _generalProperties = ["indentWidth", "useTabs", "newLineKind", "csharp.enabled", "visualBasic.enabled"];

    /// <summary>
    /// Documentation of the Roslyn options by their name in <see cref="CSharpFormattingOptions"/>, in the order they're shown.
    /// An option Roslyn adds is shown in the "Other" section until it's documented here.
    /// </summary>
    private static readonly OptionDocs[] _optionDocs =
    [
        new("IndentBlock", IndentationSection, "Whether to indent the contents of blocks.", ["void Run()", "{", "    Start();", "}"]),
        new("IndentBraces", IndentationSection, "Whether to indent the braces of blocks to the level of their contents.", ["if (ready)", "{", "    Start();", "}"]),
        new("IndentSwitchSection", IndentationSection, "Whether to indent the `case` labels of a `switch` statement.", ["switch (value)", "{", "    case 1:", "        Start();", "        break;", "}"]),
        new("IndentSwitchCaseSection", IndentationSection, "Whether to indent the statements under a `case` label.", ["switch (value)", "{", "    case 1:", "        Start();", "        break;", "}"]),
        new("IndentSwitchCaseSectionWhenBlock", IndentationSection, "Whether to indent a block under a `case` label.", ["switch (value)", "{", "    case 1:", "        {", "            Start();", "            break;", "        }", "}"]),
        new(
            "LabelPositioning",
            IndentationSection,
            "Where to put labels.",
            ["class Worker", "{", "    void Run()", "    {", "    retry:", "        if (!TryStart())", "            goto retry;", "    }", "}"],
            new()
            {
                ["leftMost"] = "Puts labels in the first column.",
                ["oneLess"] = "Indents labels one level less than the statements around them.",
                ["noIndent"] = "Indents labels the same as the statements around them.",
            }
        ),
        new("NewLinesForBracesInTypes", BracesSection, "Whether to put the opening brace of a type on a new line.", ["class Counter", "{", "    int count;", "}"]),
        new("NewLinesForBracesInMethods", BracesSection, "Whether to put the opening brace of a method or local function on a new line.", ["void Run()", "{", "    Start();", "}"]),
        new("NewLinesForBracesInProperties", BracesSection, "Whether to put the opening brace of a property on a new line.", ["class Counter", "{", "    int Count", "    {", "        get => count;", "    }", "}"]),
        new("NewLinesForBracesInAccessors", BracesSection, "Whether to put the opening brace of a property accessor on a new line.", ["class Counter", "{", "    int Count", "    {", "        get", "        {", "            return count;", "        }", "    }", "}"]),
        new("NewLinesForBracesInControlBlocks", BracesSection, "Whether to put the opening brace of a statement like `if` or `for` on a new line.", ["if (ready)", "{", "    Start();", "}"]),
        new("NewLinesForBracesInAnonymousMethods", BracesSection, "Whether to put the opening brace of an anonymous method on a new line.", ["button.Click += delegate", "{", "    Save();", "};"]),
        new("NewLinesForBracesInLambdaExpressionBody", BracesSection, "Whether to put the opening brace of a lambda's body on a new line.", ["button.Click += (sender, args) =>", "{", "    Save();", "};"]),
        new("NewLinesForBracesInAnonymousTypes", BracesSection, "Whether to put the opening brace of an anonymous type on a new line.", ["var point = new", "{", "    X = 1,", "    Y = 2", "};"]),
        new("NewLinesForBracesInObjectCollectionArrayInitializers", BracesSection, "Whether to put the opening brace of an object, collection, or array initializer on a new line.", ["var point = new Point", "{", "    X = 1,", "    Y = 2", "};"]),
        new("NewLineForElse", NewLinesSection, "Whether to put `else` on a new line.", ["if (ready)", "{", "    Start();", "}", "else", "{", "    Wait();", "}"]),
        new("NewLineForCatch", NewLinesSection, "Whether to put `catch` on a new line.", ["try", "{", "    Run();", "}", "catch (Exception)", "{", "    Log();", "}"]),
        new("NewLineForFinally", NewLinesSection, "Whether to put `finally` on a new line.", ["try", "{", "    Run();", "}", "finally", "{", "    Close();", "}"]),
        new("NewLineForClausesInQuery", NewLinesSection, "Whether to put each clause of a multi-line query expression on a new line.", ["var names = from person in people where person.Age >= 18", "    select person.Name;"]),
        new("NewLineForMembersInObjectInit", NewLinesSection, "Whether to put each member of a multi-line object initializer on a new line.", ["var point = new Point", "{", "    X = 1, Y = 2", "};"]),
        new("NewLineForMembersInAnonymousTypes", NewLinesSection, "Whether to put each member of a multi-line anonymous type on a new line.", ["var point = new", "{", "    X = 1, Y = 2", "};"]),
        new("SpaceAfterComma", SpacesSection, "Whether to put a space after a comma.", ["Add(1, 2);"]),
        new("SpaceBeforeComma", SpacesSection, "Whether to put a space before a comma.", ["Add(1, 2);"]),
        new("SpaceAfterDot", SpacesSection, "Whether to put a space after the dot of a member access.", ["list.Add(1);"]),
        new("SpaceBeforeDot", SpacesSection, "Whether to put a space before the dot of a member access.", ["list.Add(1);"]),
        new("SpaceAfterSemicolonsInForStatement", SpacesSection, "Whether to put a space after the semicolons of a `for` statement.", ["for (int i = 0; i < 10; i++)", "    Step(i);"]),
        new("SpaceBeforeSemicolonsInForStatement", SpacesSection, "Whether to put a space before the semicolons of a `for` statement.", ["for (int i = 0; i < 10; i++)", "    Step(i);"]),
        new("SpaceAfterColonInBaseTypeDeclaration", SpacesSection, "Whether to put a space after the colon before the base types of a type.", ["class Dog : Animal", "{", "}"]),
        new("SpaceBeforeColonInBaseTypeDeclaration", SpacesSection, "Whether to put a space before the colon before the base types of a type.", ["class Dog : Animal", "{", "}"]),
        new("SpaceAfterControlFlowStatementKeyword", SpacesSection, "Whether to put a space after a keyword like `if` or `while`.", ["while (running)", "    Step();"]),
        new("SpaceAfterCast", SpacesSection, "Whether to put a space after a cast.", ["var count = (int)total;"]),
        new(
            "SpacingAroundBinaryOperator",
            SpacesSection,
            "The spaces around binary operators.",
            ["var total = a +  b*c;"],
            new()
            {
                ["single"] = "Puts a space on each side of binary operators.",
                ["ignore"] = "Maintains the spaces around binary operators.",
                ["remove"] = "Removes the spaces around binary operators.",
            }
        ),
        new("SpacesIgnoreAroundVariableDeclaration", SpacesSection, "Whether to keep the spaces in variable declarations as they are.", ["int  count   = 0;"]),
        new("SpaceAfterMethodCallName", ParenthesesSection, "Whether to put a space between the name of a method and the parentheses of a call to it.", ["Add(1, 2);"]),
        new("SpacingAfterMethodDeclarationName", ParenthesesSection, "Whether to put a space between the name of a method and its parameters.", ["void Run()", "{", "}"]),
        new("SpaceWithinMethodCallParentheses", ParenthesesSection, "Whether to put spaces within the parentheses of a call.", ["Add(1, 2);"]),
        new("SpaceWithinMethodDeclarationParenthesis", ParenthesesSection, "Whether to put spaces within the parentheses of a method's parameters.", ["int Add(int a, int b)", "{", "    return a + b;", "}"]),
        new("SpaceBetweenEmptyMethodCallParentheses", ParenthesesSection, "Whether to put a space within the empty parentheses of a call.", ["Run();"]),
        new("SpaceBetweenEmptyMethodDeclarationParentheses", ParenthesesSection, "Whether to put a space within the empty parentheses of a method without parameters.", ["void Run()", "{", "}"]),
        new("SpaceWithinCastParentheses", ParenthesesSection, "Whether to put spaces within the parentheses of a cast.", ["var count = (int)total;"]),
        new("SpaceWithinExpressionParentheses", ParenthesesSection, "Whether to put spaces within parentheses around an expression.", ["var total = (a + b) * c;"]),
        new("SpaceWithinOtherParentheses", ParenthesesSection, "Whether to put spaces within other parentheses, like the ones of an `if` statement.", ["if (ready)", "    Start();"]),
        new("SpaceBeforeOpenSquareBracket", ParenthesesSection, "Whether to put a space before the square brackets of an element access.", ["var first = values[0];"]),
        new("SpaceWithinSquareBrackets", ParenthesesSection, "Whether to put spaces within the square brackets of an element access.", ["var first = values[0];"]),
        new("SpaceBetweenEmptySquareBrackets", ParenthesesSection, "Whether to put a space within empty square brackets, like the ones of an array type.", ["int[] values = new int[] { 1, 2 };"]),
        new("WrappingKeepStatementsOnSingleLine", WrappingSection, "Whether to keep statements that are on the same line together instead of putting each on its own line.", ["int a = 1; int b = 2;"]),
        new("WrappingPreserveSingleLine", WrappingSection, "Whether to keep blocks and properties that are on a single line instead of spreading them over multiple lines.", ["class Counter", "{", "    int Count { get { return count; } }", "}"]),
    ];

    [Test]
    public void Schema_Generated_SameAsFile()
    {
        var filePath = Path.Combine(GetRepositoryRootDir(), "deployment", "schema.json");
        var expected = GenerateSchema();

        if (Environment.GetEnvironmentVariable(UpdateEnvVarName) == "1")
            File.WriteAllText(filePath, expected);

        var actual = File.Exists(filePath) ? File.ReadAllText(filePath).ReplaceLineEndings("\n") : "";
        Assert.That(actual, Is.EqualTo(expected), $"The schema is out of date. Run the tests with the {UpdateEnvVarName}=1 environment variable to update it.");
    }

    private static string GenerateSchema()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("$schema", "http://json-schema.org/draft-07/schema#");
            // the version is filled in when releasing
            writer.WriteString("$id", "https://plugins.dprint.dev/dprint/dprint-plugin-roslyn/0.0.0/schema.json");
            writer.WriteString("type", "object");
            WritePlayground(writer);
            WriteSections(writer);
            WriteDefinitions(writer);
            WriteProperties(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>
    /// Writes the unformatted code that the dprint.dev playground starts with.
    /// </summary>
    private static void WritePlayground(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("x-playground");
        WriteLines(writer, "input", [
            "using System;",
            "using System.Collections.Generic;",
            "",
            "namespace Library{",
            "  public enum Genre{Fiction,History,Science}",
            "",
            "  public class Book{",
            "    public string Title{get;set;}",
            "    public Genre Genre{get;init;}",
            "      public int Year {get;set;}",
            "  }",
            "",
            "    public class Catalog {",
            "  private readonly Dictionary<string,Book> books=new Dictionary<string,Book>();",
            "",
            "  public void Add(Book book){",
            "        if(books.ContainsKey(book.Title))throw new InvalidOperationException($\"'{book.Title}' is already in the catalog\");",
            "    books[book.Title]=book;",
            "  }",
            "",
            "      public IEnumerable<Book> Published(int from,int to){",
            "    foreach(var book in books.Values){if(book.Year>=from&&book.Year<=to)yield return book;}",
            "      }",
            "  }",
            "}",
        ]);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the sections that dprint.dev groups the properties into.
    /// </summary>
    private static void WriteSections(Utf8JsonWriter writer)
    {
        var docsByName = _optionDocs.ToDictionary(docs => docs.Name);
        var optionNames = GetOptionProperties().Select(property => property.Name).ToHashSet();
        writer.WriteStartArray("x-sections");
        foreach (var (title, description) in _sections)
        {
            var properties = title switch
            {
                GeneralSection => _generalProperties,
                OtherSection => optionNames.Where(name => !docsByName.ContainsKey(name)).Order(StringComparer.Ordinal).Select(GetOptionPropertyName).ToArray(),
                _ => _optionDocs.Where(docs => docs.Section == title && optionNames.Contains(docs.Name)).Select(docs => GetOptionPropertyName(docs.Name)).ToArray(),
            };
            if (properties.Length == 0)
                continue;

            writer.WriteStartObject();
            writer.WriteString("title", title);
            writer.WriteString("description", description);
            WriteLines(writer, "properties", properties);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteDefinitions(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("definitions");

        writer.WriteStartObject("indentWidth");
        writer.WriteString("description", "The number of characters for an indent.");
        writer.WriteString("type", "integer");
        writer.WriteNumber("default", 4);
        writer.WriteEndObject();

        writer.WriteStartObject("useTabs");
        writer.WriteString("description", "Whether to use tabs (true) or spaces (false).");
        writer.WriteString("type", "boolean");
        writer.WriteBoolean("default", false);
        writer.WriteStartArray("oneOf");
        WriteConst(writer, true, "Uses tabs for indentation.");
        WriteConst(writer, false, "Uses spaces for indentation.");
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteStartObject("newLineKind");
        writer.WriteString("description", "The kind of newline to use.");
        writer.WriteString("type", "string");
        writer.WriteString("default", "auto");
        writer.WriteStartArray("oneOf");
        WriteConst(writer, "auto", "For each file, uses the newline kind found at the end of the last line.");
        WriteConst(writer, "crlf", "Uses carriage return, line feed.");
        WriteConst(writer, "lf", "Uses line feed.");
        WriteConst(writer, "system", "Uses line feed, because the plugin is unable to see the system standard.");
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteProperties(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("properties");

        foreach (var prefix in _languagePrefixes.Prepend(""))
        {
            foreach (var name in new[] { "indentWidth", "useTabs", "newLineKind" })
            {
                writer.WriteStartObject(prefix + name);
                writer.WriteString("$ref", $"#/definitions/{name}");
                // the language specific properties default to the ones without a prefix
                if (prefix.Length > 0)
                    writer.WriteString("x-overrides", name);
                else if (name == "useTabs")
                    WriteExample(writer, ["class Counter", "{", "    int count;", "}"]);
                writer.WriteEndObject();
            }
        }

        foreach (var (prefix, languageName) in new[] { ("csharp.", "C#"), ("visualBasic.", "Visual Basic") })
        {
            writer.WriteStartObject(prefix + "enabled");
            writer.WriteString("description", $"Whether to format {languageName} files.");
            writer.WriteString("type", "boolean");
            writer.WriteBoolean("default", true);
            writer.WriteEndObject();
        }

        var docsByName = _optionDocs.ToDictionary(docs => docs.Name);
        foreach (var property in GetOptionProperties())
            WriteOption(writer, property, docsByName.GetValueOrDefault(property.Name));

        writer.WriteEndObject();
    }

    private static void WriteOption(Utf8JsonWriter writer, PropertyInfo property, OptionDocs? docs)
    {
        var option = (IOption)property.GetValue(null)!;
        writer.WriteStartObject(GetOptionPropertyName(property.Name));
        var roslynDescription = $"Sets the `{nameof(CSharpFormattingOptions)}.{property.Name}` option of Roslyn.";
        writer.WriteString("description", docs is null ? roslynDescription : $"{docs.Description} {roslynDescription}");

        if (option.Type == typeof(bool))
        {
            writer.WriteString("type", "boolean");
            writer.WriteBoolean("default", (bool)option.DefaultValue!);
        }
        else if (option.Type == typeof(int))
        {
            writer.WriteString("type", "integer");
            writer.WriteNumber("default", (int)option.DefaultValue!);
        }
        else if (option.Type.IsEnum)
        {
            writer.WriteString("type", "string");
            writer.WriteString("default", ToCamelCase(option.DefaultValue!.ToString()!));
            writer.WriteStartArray("oneOf");
            foreach (var name in Enum.GetNames(option.Type).Select(ToCamelCase))
            {
                writer.WriteStartObject();
                writer.WriteString("const", name);
                if (docs?.Values?.GetValueOrDefault(name) is { } valueDescription)
                    writer.WriteString("description", valueDescription);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        else
        {
            throw new NotSupportedException($"Unhandled type of option {property.Name}: {option.Type}");
        }

        if (docs?.Example is { } example)
            WriteExample(writer, example);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the C# code that dprint.dev formats with each value of the property.
    /// </summary>
    private static void WriteExample(Utf8JsonWriter writer, string[] input)
    {
        writer.WriteStartObject("x-example");
        WriteLines(writer, "input", input);
        writer.WriteEndObject();
    }

    private static void WriteConst(Utf8JsonWriter writer, string value, string description)
    {
        writer.WriteStartObject();
        writer.WriteString("const", value);
        writer.WriteString("description", description);
        writer.WriteEndObject();
    }

    private static void WriteConst(Utf8JsonWriter writer, bool value, string description)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("const", value);
        writer.WriteString("description", description);
        writer.WriteEndObject();
    }

    private static void WriteLines(Utf8JsonWriter writer, string name, IEnumerable<string> lines)
    {
        writer.WriteStartArray(name);
        foreach (var line in lines)
            writer.WriteStringValue(line);
        writer.WriteEndArray();
    }

    private static IEnumerable<PropertyInfo> GetOptionProperties()
    {
        return typeof(CSharpFormattingOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(property => property.Name, StringComparer.Ordinal);
    }

    private static string GetOptionPropertyName(string name) => "csharp." + ToCamelCase(name);

    private static string ToCamelCase(string name) => name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);

    private static string GetRepositoryRootDir()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DprintPluginRoslyn.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Could not find the root directory of the repository.");
    }

    /// <param name="Name">The name of the option in <see cref="CSharpFormattingOptions"/>.</param>
    /// <param name="Example">The C# code that dprint.dev formats with each value of the option.</param>
    /// <param name="Values">The descriptions of the values of an enum option by their name in the configuration.</param>
    private sealed record OptionDocs(string Name, string Section, string Description, string[]? Example = null, Dictionary<string, string>? Values = null);
}
