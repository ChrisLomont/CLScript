using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib.AST
{
    class CastAst : ExpressionAst
    {
        // todo - ugly cross reference, rethink
        public CodeGeneratorVisitor.CastOp CastOp { get; set; }
    }
}
