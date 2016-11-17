using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    class MatchEndOfLine : MatchBase
    {
        public static bool IsEndOfLineChar(char ch)
        {
                return ch == '\n' || ch == '\r';
        }

        // windows is '\n', Unix '\r\n', most common. 
        static string[] endmarkers = {"\r\n","\n"};

        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            foreach (var m in endmarkers)
                if (characterStream.StartsWith(m))
                {
                    characterStream.Consume(m.Length);
                    return new Token(TokenType.EndOfLine);
                }
            return null;
        }

    }
}
