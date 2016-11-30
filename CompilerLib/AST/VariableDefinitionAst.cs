using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class VariableDefinitionAst:Ast
    {
        public override string ToString()
        {
            var msg = "";
            if (Children.Count > 0 && Children[0].Children.Count > 0)
                msg = Children[0].Children[0].Token?.ToString();
            return Format(msg);
        }
    }
}
