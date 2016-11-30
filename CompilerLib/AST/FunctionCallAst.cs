using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class FunctionCallAst : ExpressionAst
    {
        public FunctionCallAst(Token identifier, Ast parameters)
        {
            Token = identifier;
            Children.AddRange(parameters.Children);
        }
    }
}
