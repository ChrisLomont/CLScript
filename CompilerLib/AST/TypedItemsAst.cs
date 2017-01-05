using System.Collections.Generic;

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
