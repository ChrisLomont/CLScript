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

        public Ast Parent { get; set; }

        /// <summary>
        /// Type of this node for type checking
        /// </summary>
        public string Type { get; set; }

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
            var typeStr = "";
            if (!String.IsNullOrEmpty(Type))
                typeStr = $":{Type}";

            return this.GetType().Name + $"{typeStr} " +
                   // Children.Aggregate("", (acc, ast) => acc + " " + ast) + 
                   $"{Token}" + 
                   "";
        }
    }
}
