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
        public List<Ast> Children { get; private set; }

        public Token Token { get; set; }

        public SymbolTable SymbolTable { get; set; }

        public Ast Parent { get; set; }

        public Ast()
        {
            Children = new List<Ast>();
        }

        public void AddChild(Ast child)
        {
            if (child != null)
                Children.Add(child);
        }

        public override string ToString()
        {
            return this.GetType().Name + " " +
                   // Children.Aggregate("", (acc, ast) => acc + " " + ast) + 
                   $"{Token}" + 
                   "";
        }
    }
}
