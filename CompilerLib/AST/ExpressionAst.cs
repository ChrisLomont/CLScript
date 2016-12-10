using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class ExpressionAst : Ast
    {
        public bool? BoolValue { get; set; }
        public Byte? ByteValue { get; set; }
        public Int32? IntValue { get; set; }
        public Double? FloatValue { get; set; }
        public bool HasValue => FloatValue.HasValue || IntValue.HasValue || BoolValue.HasValue || ByteValue.HasValue;

        public override string ToString()
        {

            var msg = "";
            if (BoolValue.HasValue)
                msg += $"={BoolValue.Value}";
            if (ByteValue.HasValue)
                msg += $"={ByteValue.Value}";
            if (IntValue.HasValue)
                msg += $"={IntValue.Value}";
            if (FloatValue.HasValue)
                msg += $"={FloatValue.Value}";

            return Format(msg);
        }
    }
}
