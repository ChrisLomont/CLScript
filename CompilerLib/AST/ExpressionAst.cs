using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class ExpressionAst : Ast
    {
        public Int32? IntValue { get; set; }
        public Double? DoubleValue { get; set; }
        public bool HasValue => DoubleValue.HasValue || IntValue.HasValue;

        public override string ToString()
        {
            var msg = base.ToString();
            if (IntValue.HasValue)
                msg += $"<i32 {IntValue.Value}>";
            if (DoubleValue.HasValue)
                msg += $"<r32 {DoubleValue.Value}>";
            return msg;
        }
    }
}
