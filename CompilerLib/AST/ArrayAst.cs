using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class ArrayAst : Ast
    {
        /// <summary>
        /// Dimension of the array
        /// </summary>
        public int Dimension => Children.Count;
    }
}
