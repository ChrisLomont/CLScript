using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public enum OperandType
    {
        None,
        Byte,
        Int32,
        Float32,
        Global,
        Local
    }

    public enum Opcode
    {
        Nop,

        // stack
        Push,       // some from code, expanded to 4 bytes, onto stack
        Pop,        // pop entry from stack
        Pick,       // push stack value from n back onto stack
        Dup,        // copy top stack value
        Swap,       // swap top two stack values
        AddStack,   // add n to stack pointer

        // mem
        Load,       // get value from memory location (local to base pointer, or global = absolute)
        Store,      // address on stack top, value one underneath, store it
        Addr,       // get address (local = relative to base pointer, global = global value)

        // label/branch/call/ret
        Call,       // relative call address
        Return,
        BrFalse,
        BrAlways,
        ForStart,   // start, end, delta values on stack. If delta = 0, compute delta +1 or -1
                    // store start at memory location, delta at location +1
                    // pops 2 from stack
        ForLoop,    // update for stack frame, branch if more to do
                    // end address is counter, then increment, end is on stack top
                    // pop end value. If more, loop, else don't
        //bitwise
        Or,
        And,
        Xor,
        Not,
        RightShift,
        LeftShift,
        RightRotate,
        LeftRotate,
        // comparison
        NotEqual,
        IsEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        LessThan,
        //arithmetic
        Neg,
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        // end

        // pseudo-ops - take no space, merely placeholders
        Label,
        Symbol

    }

    public class Instruction
    {
        public Opcode Opcode { get; private set; }
        public object[] Operands { get; private set; }
        public OperandType OperandType { get; private set; }
        public string Comment { get; set; }

        public Instruction(Opcode opcode, OperandType operandType, string comment, params object[] operands)
        {
            Opcode = opcode;
            Operands = operands;
            OperandType = operandType;
            Comment = comment;
        }

        public override string ToString()
        {
            var opcode = Opcode.ToString();
            if (OperandType == OperandType.Float32)
                opcode += ".f";
            else if (OperandType == OperandType.Int32)
                opcode += ".i";
            else if (OperandType == OperandType.Byte)
                opcode += ".b";
            else if (OperandType == OperandType.Local)
                opcode += ".L";
            else if (OperandType == OperandType.Global)
                opcode += ".G";
            else if (OperandType != OperandType.None)
                throw new InternalFailure($"Unknown operand type {OperandType}");

            var operands = "";

            if (!opcode.StartsWith("Label") && Opcode != Opcode.Symbol)
            {
                opcode = "     " + opcode;
                foreach (var op in Operands)
                    operands += $" {op}";
            }
            else
                opcode = $"{opcode} {Operands[0]}:";
                // operands = $"{Operands[0]}:";

            var comment = "";
            if (!String.IsNullOrEmpty(Comment))
                comment = $" ; {Comment}";

            return $"{opcode,-20}{operands,-15}{comment}";
        }

    }
}
