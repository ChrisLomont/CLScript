using System;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class MatchKeyword : MatchBase
    {
        public string Match { get; set; }

        TokenType TokenType { get; set; }

        public MatchKeyword(TokenType type, String match)
        {
            Match = match;
            TokenType = type;
        }

        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            if (characterStream.StartsWith(Match))
            {
                characterStream.Consume(Match.Length);
                return new Token(TokenType);
            }
            return null;
        }
    }
}
