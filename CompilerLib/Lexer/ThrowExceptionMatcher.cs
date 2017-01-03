using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    class ThrowExceptionMatcher : MatchBase
    {
        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            throw new InvalidSyntax($"Unsupported syntax character : '{characterStream.Current}'");
        }
    }
}
