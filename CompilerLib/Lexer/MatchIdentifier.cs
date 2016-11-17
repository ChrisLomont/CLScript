using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    class MatchIdentifier :MatchBase
    {
        bool IsIdStart(char ch)
        {
            return Char.IsLetter(ch) || ch == '_';

        }
        bool IsIdFollow(char ch)
        {
            return Char.IsLetter(ch) || Char.IsDigit(ch) || ch == '_';
        }

        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            if (IsIdStart(characterStream.Current))
            {
                characterStream.Consume();
                while (!characterStream.End && IsIdFollow(characterStream.Current))
                    characterStream.Consume();
                return new Token(TokenType.Identifier);
            }
            return null;
        }

    }
}
