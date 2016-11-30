using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class AssignItemsAst : Ast
    {
        public AssignItemsAst(List<Ast> children)
        {
            Children.AddRange(children);
        }

    }
}
