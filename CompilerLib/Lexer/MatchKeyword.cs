using System;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class MatchKeyword : MatchBase
    {
        public string Match { get; set; }

        TokenType TokenType { get; }

        readonly Func<char, bool> nextOk = c => true; // allows anything

        public MatchKeyword(TokenType type, String match, Func<char,bool> nextOk = null)
        {
            Match = match;
            TokenType = type;
            if (nextOk != null) this.nextOk = nextOk;
        }

        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            if (characterStream.StartsWith(Match) && nextOk(characterStream.Peek(Match.Length)))
            {
                characterStream.Consume(Match.Length);
                return new Token(TokenType);
            }
            return null;
        }
    }
}
