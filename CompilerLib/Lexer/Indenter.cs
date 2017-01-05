using System.Collections.Generic;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    class Indenter
    {
        public Indenter()
        {
            indentPosition.Push(1); // lowest level is line position 1
        }

        readonly List<Token> tokens = new List<Token>();

        // stack of indentation regions
        readonly Stack<int> indentPosition = new Stack<int>();

        readonly List<Token> history = new List<Token>();
        public bool Indented => indentPosition.Count > 1;

        // history 0 is current, 1 is 1 back, etc
        Token History(int index)
        {
            var hIndex = history.Count-1-index;
            if (hIndex < 0 || history.Count <= hIndex)
                return new Token(TokenType.Unknown);
            return history[hIndex];
        }
        

        // inspect the next token about to go out, add indent/unindent tokens as needed
        // return empty list if none
        public List<Token> ProcessToken(Token current)
        {
            tokens.Clear();
            if (current.TokenType == TokenType.Comment)
                return tokens;  // nothing to update

            history.Add(current);

            var checkStack = false;

            var h0 = History(0).TokenType;
            var h1 = History(1).TokenType;
            var h2 = History(2).TokenType;
            if (h1 == TokenType.EndOfLine && h0 != TokenType.WhiteSpace && h0 != TokenType.EndOfLine)
                checkStack = true;
            if (h2 == TokenType.EndOfLine && h1 == TokenType.WhiteSpace && h0 != TokenType.EndOfLine)
                checkStack = true;


            if (checkStack)
            {
                var pos = History(0).Position.LinePosition;
                if (pos > indentPosition.Peek())
                {   // add an indention
                    indentPosition.Push(pos);
                    tokens.Add(new Token(TokenType.Indent));
                }
                while (pos < indentPosition.Peek())
                {   // undent as many as needed
                    indentPosition.Pop();
                    tokens.Add(new Token(TokenType.Undent));
                }
            }

            return tokens;
        }

        public void Unindent()
        {
            if (Indented)
                indentPosition.Pop();
        }
    }
}
