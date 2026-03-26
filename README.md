# legendary-octo-system
This is a .dotnet 10 library for generating LL(1) parser generators via source generators. 

## Example

ˋˋˋc#
[Lexer]
public partial class MyLexer {
  
  [LexImpl]  
	 public static partial MyLexer Lex(string input); 
}

ˋˋˋ