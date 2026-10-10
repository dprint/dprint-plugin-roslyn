using Dprint.Plugins.Roslyn.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.VisualBasic;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Dprint.Plugins.Roslyn.Formatters;

/// <summary>
/// Ensures formatting without a workspace gives the same result as Roslyn's public API.
/// </summary>
[TestFixture]
public class CodeFormatterTests
{
    private const string CSharpCode = """
        using System;
        namespace Test{
        public class Sample<T>where T:class{
        private int[] _values=new int[]{1,2,3};
        public int Value{get;set;}
        public event EventHandler Changed{add{}remove{}}
        public Sample( int value ):base( ){Value=( int )value;}
        public int this[int index]{get{return _values [ index ];}}
        public void Method( int a,int b ){
        if(a>b){Console.WriteLine( a );}else{Console.WriteLine( b );}
        for(var i=0;i<a;i++){}
        try{}catch(Exception){}finally{}
        switch(a){case 1:{break;}default:break;}
        Func<int,int> func=x=>{return x*2;};
        var anon=new{A=1,B=2};
        var obj=new Sample<string>( 1 ){Value=2};
        var query=from v in _values where v>1 select v;
        var size=sizeof( int );var type=typeof( int );
        label:
        goto label;
        }
        }
        }

        """;

    private const string VisualBasicCode = """
        Namespace    Test
        Public Class Sample
        Public Sub Method( a As Integer,b As Integer )
        If a>b Then
        Console.WriteLine( a )
        End If
        End Sub
        End   Class
        End  Namespace

        """;

    [Test]
    public void Format_CSharpDefaultOptions_MatchesRoslyn()
    {
        AssertCSharpMatchesRoslyn(options => options);
    }

    [TestCaseSource(nameof(GetCSharpOptionChanges))]
    public void Format_CSharpChangedOption_MatchesRoslyn(string name, Func<OptionSet, OptionSet> changeOption)
    {
        AssertCSharpMatchesRoslyn(changeOption);
    }

    [TestCase(2, false, "\n")]
    [TestCase(8, true, "\r\n")]
    public void Format_CSharpGlobalOptions_MatchesRoslyn(int indentationSize, bool useTabs, string newLine)
    {
        AssertCSharpMatchesRoslyn(options => WithGlobalOptions(options, LanguageNames.CSharp, indentationSize, useTabs, newLine));
    }

    [TestCase(4, false, "\r\n")]
    [TestCase(2, false, "\n")]
    [TestCase(8, true, "\r\n")]
    public void Format_VisualBasic_MatchesRoslyn(int indentationSize, bool useTabs, string newLine)
    {
        using var workspace = new AdhocWorkspace();
        var root = VisualBasicSyntaxTree.ParseText(VisualBasicCode).GetRoot();
        var expected = Formatter.Format(root, workspace, WithGlobalOptions(workspace.Options, LanguageNames.VisualBasic, indentationSize, useTabs, newLine)).ToFullString();

        var options = WithGlobalOptions(new PluginOptionSet(), LanguageNames.VisualBasic, indentationSize, useTabs, newLine);
        var actual = new VisualBasicCodeFormatter().FormatText(root.GetText(), null, options, CancellationToken.None).ToString();

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual, Is.Not.EqualTo(VisualBasicCode));
    }

    [Test]
    public void Format_CSharpRange_MatchesRoslyn()
    {
        using var workspace = new AdhocWorkspace();
        var root = CSharpSyntaxTree.ParseText(CSharpCode).GetRoot();
        var span = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single().Span;
        var expected = Formatter.Format(root, span, workspace, workspace.Options).ToFullString();

        var actual = new CSharpCodeFormatter().FormatText(root.GetText(), span, new PluginOptionSet(), CancellationToken.None).ToString();

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual, Is.Not.EqualTo(CSharpCode));
    }

    private static void AssertCSharpMatchesRoslyn(Func<OptionSet, OptionSet> changeOptions)
    {
        using var workspace = new AdhocWorkspace();
        var root = CSharpSyntaxTree.ParseText(CSharpCode).GetRoot();
        var expected = Formatter.Format(root, workspace, changeOptions(workspace.Options)).ToFullString();

        var actual = new CSharpCodeFormatter().FormatText(root.GetText(), null, changeOptions(new PluginOptionSet()), CancellationToken.None).ToString();

        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>
    /// Gets a change to a non-default value for every value of every option in <see cref="CSharpFormattingOptions"/>.
    /// </summary>
    private static IEnumerable<TestCaseData> GetCSharpOptionChanges()
    {
        foreach (var property in typeof(CSharpFormattingOptions).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var option = (IOption)property.GetValue(null)!;
            var values = option.Type == typeof(bool) ? new object[] { true, false } : Enum.GetValues(option.Type).Cast<object>();
            foreach (var value in values.Where(value => !value.Equals(option.DefaultValue)))
            {
                var name = $"{property.Name}={value}";
                Func<OptionSet, OptionSet> changeOption = options => options.WithChangedOption(new OptionKey(option), value);
                yield return new TestCaseData(name, changeOption).SetArgDisplayNames(name);
            }
        }
    }

    private static OptionSet WithGlobalOptions(OptionSet options, string language, int indentationSize, bool useTabs, string newLine)
    {
        return options
            .WithChangedOption(FormattingOptions.IndentationSize, language, indentationSize)
            .WithChangedOption(FormattingOptions.TabSize, language, indentationSize)
            .WithChangedOption(FormattingOptions.UseTabs, language, useTabs)
            .WithChangedOption(FormattingOptions.NewLine, language, newLine);
    }
}
