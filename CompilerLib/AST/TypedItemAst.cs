using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class TypedItemAst : Ast
    {
        public Token ImportToken { get; set; }
        public Token ExportToken { get; set; }
        public Token ConstToken { get; set; }

        public TypedItemAst(Token nameToken, Token baseTypeToken)
        {
            Token = nameToken;
            BaseTypeToken = baseTypeToken;
        }

        public Token BaseTypeToken;

        public string Name => Token?.TokenValue;

        public override string ToString()
        {
            return Format($"({Name}:{BaseTypeToken?.TokenValue}) {ImportToken} {ExportToken} {ConstToken}");
        }
    }
}
