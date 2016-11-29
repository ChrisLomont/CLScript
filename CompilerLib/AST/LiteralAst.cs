using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class LiteralAst : ExpressionAst
    {
        public LiteralAst(Token token)
        {
            Token = token;
        }
    }
}
