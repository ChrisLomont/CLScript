using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib.AST
{
    public abstract class Ast
    {
        // public MemorySpace CallingMemory { get; set; }
        // public Scope CallingScope { get; set; }
        // public Scope CurrentScope { get; set; }
        // public Scope Global { get; set; }
        // public IType AstSymbolType { get; set; }
        /// <summary>
        /// Used instead of reflection to determine the syntax tree type
        /// </summary>
        // public abstract AstTypes AstType { get; }

        public Token Token { get; set; }

        public List<Ast> Children { get; private set; }

        public Ast ConvertedExpression { get; set; }

        public bool IsLink { get; set; }

        protected Ast(Token token)
        {
            Token = token;
            Children = new List<Ast>();
        }

        public void AddChild(Ast child)
        {
            if (child != null)
            {
                Children.Add(child);
            }
        }

        public override string ToString()
        {
            return Token.TokenType + " " + Children.Aggregate("", (acc, ast) => acc + " " + ast);
        }
    }
}
