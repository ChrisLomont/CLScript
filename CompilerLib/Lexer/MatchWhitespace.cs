namespace Lomont.ClScript.CompilerLib.Lexer
{
    class MatchWhiteSpace : MatchBase
    {
        bool IsWhitespace(char ch)
        {
            if (ch == '\t')
                throw new IllegalCharacter("Tab character not allowed");
            return ch == ' ';

        }

        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            var foundWhiteSpace = false;

            while (!characterStream.End && IsWhitespace(characterStream.Current))
            {
                foundWhiteSpace = true;
                characterStream.Consume();
            }

            return foundWhiteSpace ? new Token(TokenType.WhiteSpace) : null;
        }
    }
}
