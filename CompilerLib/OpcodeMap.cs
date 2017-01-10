using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public static class OpcodeMap
    {
        public struct Entry
        {
            public Entry(Opcode opcode, OperandType operandType)
            {
                Opcode = opcode;
                OperandType = operandType;

            }
            public Opcode Opcode { get; private set;  }
            public OperandType OperandType { get; private set;  }

        }

        public static Entry [] Table  =
        {
            new Entry(Opcode.Nop,OperandType.None),

            // stack
            new Entry(Opcode.Push,OperandType.Byte),
            new Entry(Opcode.Push,OperandType.Int32),
            new Entry(Opcode.Push,OperandType.Float32),
            new Entry(Opcode.Pop,OperandType.None),
            new Entry(Opcode.Dup,OperandType.None),
            new Entry(Opcode.Swap,OperandType.None),
            new Entry(Opcode.Rot3,OperandType.None),
            new Entry(Opcode.ClearStack,OperandType.None),
            new Entry(Opcode.PopStack,OperandType.None),
            new Entry(Opcode.Reverse,OperandType.None),

            // mem
            new Entry(Opcode.Load,OperandType.Global),
            new Entry(Opcode.Load,OperandType.Local),
            new Entry(Opcode.Load,OperandType.Const),
            new Entry(Opcode.Read,OperandType.Global),
            new Entry(Opcode.Read,OperandType.Local),
            new Entry(Opcode.Read,OperandType.Const),
            new Entry(Opcode.Write,OperandType.Byte),
            new Entry(Opcode.Write,OperandType.Int32),
            new Entry(Opcode.Write,OperandType.Float32),
            new Entry(Opcode.Update,OperandType.Byte),
            new Entry(Opcode.Update,OperandType.Int32),
            new Entry(Opcode.Update,OperandType.Float32),
            new Entry(Opcode.Addr,OperandType.Global),
            new Entry(Opcode.Addr,OperandType.Local),
            new Entry(Opcode.Addr,OperandType.Const),
            new Entry(Opcode.Array,OperandType.None),
            new Entry(Opcode.MakeArr,OperandType.Global),
            new Entry(Opcode.MakeArr,OperandType.Local),

            // label/branch/call/ret
            new Entry(Opcode.Call,OperandType.Local),
            new Entry(Opcode.Call,OperandType.Const),
            new Entry(Opcode.Return,OperandType.None),
            new Entry(Opcode.BrTrue,OperandType.None),
            new Entry(Opcode.BrFalse,OperandType.None),
            new Entry(Opcode.BrAlways,OperandType.None),
            new Entry(Opcode.ForStart,OperandType.None),
            new Entry(Opcode.ForLoop,OperandType.None),

            // bitwise
            new Entry(Opcode.Or,OperandType.None),
            new Entry(Opcode.And,OperandType.None),
            new Entry(Opcode.Xor,OperandType.None),
            new Entry(Opcode.Not,OperandType.None),
            new Entry(Opcode.RightShift,OperandType.None),
            new Entry(Opcode.LeftShift,OperandType.None),
            new Entry(Opcode.RightRotate,OperandType.None),
            new Entry(Opcode.LeftRotate,OperandType.None),

            // comparison
            new Entry(Opcode.NotEqual,OperandType.Byte),
            new Entry(Opcode.NotEqual,OperandType.Int32),
            new Entry(Opcode.NotEqual,OperandType.Float32),
            new Entry(Opcode.IsEqual,OperandType.Byte),
            new Entry(Opcode.IsEqual,OperandType.Int32),
            new Entry(Opcode.IsEqual,OperandType.Float32),
            new Entry(Opcode.GreaterThan,OperandType.Byte),
            new Entry(Opcode.GreaterThan,OperandType.Int32),
            new Entry(Opcode.GreaterThan,OperandType.Float32),
            new Entry(Opcode.GreaterThanOrEqual,OperandType.Byte),
            new Entry(Opcode.GreaterThanOrEqual,OperandType.Int32),
            new Entry(Opcode.GreaterThanOrEqual,OperandType.Float32),
            new Entry(Opcode.LessThan,OperandType.Byte),
            new Entry(Opcode.LessThan,OperandType.Int32),
            new Entry(Opcode.LessThan,OperandType.Float32),
            new Entry(Opcode.LessThanOrEqual,OperandType.Byte),
            new Entry(Opcode.LessThanOrEqual,OperandType.Int32),
            new Entry(Opcode.LessThanOrEqual,OperandType.Float32),

            // arithmetic
            new Entry(Opcode.Neg,OperandType.Byte),
            new Entry(Opcode.Neg,OperandType.Int32),
            new Entry(Opcode.Neg,OperandType.Float32),
            new Entry(Opcode.Add,OperandType.Byte),
            new Entry(Opcode.Add,OperandType.Int32),
            new Entry(Opcode.Add,OperandType.Float32),
            new Entry(Opcode.Sub,OperandType.Byte),
            new Entry(Opcode.Sub,OperandType.Int32),
            new Entry(Opcode.Sub,OperandType.Float32),
            new Entry(Opcode.Mul,OperandType.Byte),
            new Entry(Opcode.Mul,OperandType.Int32),
            new Entry(Opcode.Mul,OperandType.Float32),
            new Entry(Opcode.Div,OperandType.Byte),
            new Entry(Opcode.Div,OperandType.Int32),
            new Entry(Opcode.Div,OperandType.Float32),
            new Entry(Opcode.Mod,OperandType.Byte),
            new Entry(Opcode.Mod,OperandType.Int32),

            // convert
            new Entry(Opcode.I2F,OperandType.None),
            new Entry(Opcode.F2I,OperandType.None),
            };

        /// <summary>
        /// Get index of this opcode/operand type pair. If doesn't exist, throw exception
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operandType"></param>
        /// <returns></returns>
        public static uint GetEntry(Opcode opcode, OperandType operandType)
        {
            for (var i = 0U; i < Table.Length; ++i)
            {
                var entry = Table[i];
                if (entry.Opcode == opcode && entry.OperandType == operandType)
                    return i;

            }
            throw new NotImplementedException($"Cannot find opcode/type pair {opcode},{operandType}");
        }
    }
}
