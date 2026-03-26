using System;
using ConsumerApp;

var src = "abc 123\nxyz";
var lexer = new MyLexer();
var result = lexer.Tokenize(src);
Console.WriteLine("Tokens:");
foreach (var t in result.Tokens)
{
    Console.WriteLine($"{t.Type} '{t.Text}' @ {t.Span.Line}:{t.Span.Column} offset={t.Span.Offset} len={t.Span.Length}");
}
if (result.Errors.Length > 0)
{
    Console.WriteLine("Errors:");
    foreach (var e in result.Errors) Console.WriteLine($"{e.Message} @ {e.Span.Line}:{e.Span.Column}");
}
