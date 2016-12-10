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
        public InternalType Type { get; set; }

        public Ast()
        {
            Children = new List<Ast>();
        }

        public void AddChild(Ast child)
        {
            if (child != null)
                Children.Add(child);
        }

        // allows derived nodes to insert some info
        protected string Format(string insertion)
        {
            var typeStr = Type == null ? "" : $" <{Type}>";

            return $"{this.GetType().Name.Replace("Ast", "")} :: {Token} :: {typeStr} :: {insertion}";
        }

        public override string ToString()
        {
            return Format("");
        }
    }
}
