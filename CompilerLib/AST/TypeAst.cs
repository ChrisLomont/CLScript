using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class TypeAst : Ast
    {
        // needs parameterless or exception on reflected construction
        public TypeAst()
        {
            
        }

        public TypeAst(Token token)
        {
            Token = token;
        }
    }
}
