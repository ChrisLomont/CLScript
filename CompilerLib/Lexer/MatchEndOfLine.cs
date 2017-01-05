namespace Lomont.ClScript.CompilerLib.Lexer
{
    class MatchEndOfLine : MatchBase
    {
        public static bool IsEndOfLineChar(char ch)
        {
                return ch == '\n' || ch == '\r';
        }

        // windows is '\n', Unix '\r\n', most common. 
        static readonly string[] Endmarkers = {"\r\n","\n"};

        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            foreach (var m in Endmarkers)
                if (characterStream.StartsWith(m))
                {
                    characterStream.Consume(m.Length);
                    return new Token(TokenType.EndOfLine);
                }
            return null;
        }

    }
}
