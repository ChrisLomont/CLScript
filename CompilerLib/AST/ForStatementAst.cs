using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class ForStatementAst :Ast
    {
        public string ForVariable => Token.TokenValue;
        public SymbolEntry VariableSymbol { get; set; }
    }
}
