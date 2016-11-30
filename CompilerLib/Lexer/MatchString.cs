using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class MatchString : MatchBase
    {
        public const char QUOTE = '\"';

        public const char TIC = '\'';

        private char StringDelim { get; set; }

        public MatchString(char delim)
        {
            StringDelim = delim;
        }


        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            var str = new StringBuilder();

            if (characterStream.Current == StringDelim)
            {
                characterStream.Consume();

                while (!characterStream.End && characterStream.Current != StringDelim)
                {
                    str.Append(characterStream.Current);
                    characterStream.Consume();
                }

                if (characterStream.Current == StringDelim)
                {
                    characterStream.Consume();
                }
            }

            if (str.Length > 0)
            {
                if (StringDelim == QUOTE)
                    return new Token(TokenType.StringLiteral, str.ToString());
                else if (str.Length == 1)
                    return new Token(TokenType.ByteLiteral, str.ToString());
            }

            return null;
        }
    }
}
