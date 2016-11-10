using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public class AstNode
    {

        public List<AstNode> Children { get; } = new List<AstNode>();

        public Token Node = null;
        public Token[] Data = null;
        public static AstNode Make(Token node, params Token [] data)
        {
            var n = new AstNode
            {
                Node = node,
                Data = data
            };
            return n;

        }
    }
}
