using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib.AST
{
    class DeclarationsAst : Ast
    {
        public DeclarationsAst(Token token) : base(token)
        {
        }
    }
}
