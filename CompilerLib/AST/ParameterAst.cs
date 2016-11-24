using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class ParameterAst : Ast
    {
        public ParameterAst(Token type, Token name, int arrayDepth)
        {
            Type = type;
            Name = name;
            ArrayDepth = arrayDepth;
        }

        public Token Type { get; set; }
        public Token Name { get; set; }
        public int ArrayDepth { get; set; }

        public override string ToString()
        {
            return $"{base.ToString()} {Type} {Name} {ArrayDepth}";
        }

    }
}
