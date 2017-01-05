using System.Collections.Generic;

namespace Lomont.ClScript.CompilerLib.AST
{
    public abstract class Ast
    {
        public List<Ast> Children { get; }

        public Token Token { get; set; }

        public Ast Parent { get; set; }

        /// <summary>
        /// Get the name associated, which is just the token value
        /// </summary>
        public string Name => Token?.TokenValue;

        /// <summary>
        /// Type of this node for type checking
        /// </summary>
        public InternalType Type { get; set; }

        protected Ast()
        {
            Children = new List<Ast>();
        }

        /// <summary>
        /// Add a a child - attach parent pointer
        /// </summary>
        /// <param name="child"></param>
        public void AddChild(Ast child)
        {
            if (child != null)
            {
                Children.Add(child);
                child.Parent = this;
            }
        }

        // allows derived nodes to insert some info
        protected string Format(string insertion)
        {
            var typeStr = Type == null ? "" : $"<{Type}>";

            return $"{GetType().Name.Replace("Ast", "")} :: {Token} :: {typeStr} :: {insertion}";
        }

        public override string ToString()
        {
            return Format("");
        }
    }
}
