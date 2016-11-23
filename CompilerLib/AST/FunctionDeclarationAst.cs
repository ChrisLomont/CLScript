using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class FunctionDeclarationAst : Ast
    {
        public Token ImportToken { get; set; }
        public Token ExportToken { get; set; }
        public override string ToString()
        {
            return $"{base.ToString()} : {ImportToken} {ExportToken}";
        }
    }
}
