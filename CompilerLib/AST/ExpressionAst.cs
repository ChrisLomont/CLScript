﻿using System;

namespace Lomont.ClScript.CompilerLib.AST
{
    class ExpressionAst : Ast
    {
        public bool? BoolValue { get; set; }
        public Byte? ByteValue { get; set; }
        public Int32? IntValue { get; set; }
        public Double? FloatValue { get; set; }
        public bool HasValue => FloatValue.HasValue || IntValue.HasValue || BoolValue.HasValue || ByteValue.HasValue;

        // some expressions have an associated symbol 
        public SymbolEntry Symbol { get; set; }

        protected string FormatSymbol()
        {
            if (Symbol == null)
                return "";
            return $"{Symbol.LayoutAddress} {Symbol.VariableUse}";
        }

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

            msg += $":: {FormatSymbol()} ";

        return Format(msg);
        }
    }
}
