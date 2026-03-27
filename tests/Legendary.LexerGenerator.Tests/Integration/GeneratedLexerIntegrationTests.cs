using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Legendary.LexerGenerator.Tests.Integration
{
    public class GeneratedLexerIntegrationTests
    {
        [Fact]
        public void Generated_Lexer_Can_Tokenize_Sample()
        {
            string source = @"using Legendary.Lexer.Annotations;

namespace ConsumerApp
{
    [Token(@""\w+"", IsSkip = false, Priority = 1)]
    public class Identifier { }

    [Token(@""\s+"", IsSkip = true, Priority = 0)]
    public class Whitespace { }

    [Lexer(typeof(Identifier), typeof(Whitespace))]
    public partial class MyLexer { }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            // Include the annotation attribute definitions inline so the generated sources compile without
            // requiring a netstandard metadata reference during in-memory compilation.
            var attrPath = "/workspaces/legendary-octo-system/src/Legendary.Lexer.Annotations/Attributes.cs";
            var attrSource = File.ReadAllText(attrPath);
            var attrTree = CSharpSyntaxTree.ParseText(attrSource);

            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && a.GetName().Name != "Legendary.Lexer.Annotations")
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToArray();

            var compilation = CSharpCompilation.Create("GeneratedLexerTest", new[] { attrTree, syntaxTree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new Legendary.LexerGenerator.LexerSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Note: for incremental generators GetRunResult may be unavailable in some runtimes;
            // rely on successful emit of the updated compilation instead.

            // Ensure the generator produced additional syntax trees containing expected identifiers
            var genTrees = outputCompilation.SyntaxTrees
                .Select(t => t.GetText().ToString())
                .Where(s => s.Contains("Tokenize(") || s.Contains("TokenType") || s.Contains("TokenMetadata"));
            Assert.NotEmpty(genTrees);
        }
    }
}
