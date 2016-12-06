using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class TypedItemsAst :Ast
    {
        public TypedItemsAst()
        {
            
        }
        public TypedItemsAst(List<Ast> children)
        {
            Children.AddRange(children);
        }

    }
}
