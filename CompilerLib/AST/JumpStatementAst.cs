using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class JumpStatementAst :Ast
    {
        public JumpStatementAst(Token token)
        {
            Token = token;
        }
    }
}
