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
        // size/type of item/opcode
        None,
        Byte,
        Int32,
        Float32,

        // where item exists
        Global, // absolute address
        Local,  // relative to base pointer address
        Const   // in const memory (ROM/FLASH, etc)
    }

    public enum Opcode
    {
                            // first letter of allowed operand types
        Nop,                // [   ]

        // stack
        Push,               // [BIF] read bytes from code, expanded to stack entry size, pushed onto stack
        Pop,                // [   ] pop entry from stack
        Pick,               // [   ] push stack value from n back onto stack
        Dup,                // [   ] copy top stack value
        Swap,               // [   ] swap top two stack values
        ClearStack,         // [   ] add n zeroes to stack (used for function stack frames)
                            
        // mem   
        // todo - these will need sized to handle byte accesses later
        Load,               // [GLC] push value from memory location onto stack
        Read,               // [GLC] Push value onto stack whose address on stack top
        Write,              // [BIF] address on stack top, value one underneath, store it. Note addr creates absolute addresses on stack
        Addr,               // [GLC] push physical address of variable. Global/const are absolute, local computed relative to base pointer

        // array
        Array,              // [   ] checked array access: takes k indices on stack, reverse order, then address of array, 
                            //       k is in code after opcode. Then computes address of item, checking bounds along the way
                            //       Array in memory has length at position -1, and a stack size of rest in -2 (header size 2)
        MakeArr,            // [GL ] make an array by filling in values. 
                            //       values in code, in order: address a, # dims n, s total size, dims in order x1,x2,...,xn
                            //       total size s is h + x1(h+x2(h+x3...(h+xn*t)..) where t is base type size

        // label/branch/call/ret
        Call,               // [   ] relative call address
        Return,             // [   ] two values (parameter entries, local stack entries) for cleaning stack after call
        BrFalse,            // [   ] pop stack. If 0, branch to relative address
        BrAlways,           // [   ] always branch to relative address
        ForStart,           // [   ] start, end, delta values on stack. If delta = 0, compute delta +1 or -1
                            //       store start at memory location, delta at location +1
                            //       pops 2 from stack.
                            //       takes address as operand for frame
        ForLoop,            // [   ] update for stack frame, branch if more to do
                            //       end address is counter, then increment, end is on stack top
                            //       pop end value. If more, loop, else don't
        //bitwise           
        Or,                 // [   ]
        And,                // [   ]
        Xor,                // [   ]
        Not,                // [   ]
        RightShift,         // [   ]
        LeftShift,          // [   ]
        RightRotate,        // [   ] 
        LeftRotate,         // [   ]
        // comparison
        NotEqual,           // [BIF]
        IsEqual,            // [BIF]
        GreaterThan,        // [BIF]
        GreaterThanOrEqual, // [BIF]
        LessThanOrEqual,    // [BIF]
        LessThan,           // [BIF]
        //arithmetic       
        Neg,                // [BIF]
        Add,                // [BIF]
        Sub,                // [BIF]
        Mul,                // [BIF]
        Div,                // [BIF]
        Mod,                // [BI ]
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
        
        // address for layout, if exists
        public int? Address { get; set; }

        public Instruction(Opcode opcode, OperandType operandType, string comment, params object[] operands)
        {
            Opcode = opcode;
            Operands = operands;
            OperandType = operandType;
            Comment = comment;
        }

        public override string ToString()
        {
            var addressTxt = Address.HasValue?$"0x{Address.Value:X5}":"";
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

            if (Opcode != Opcode.Label && Opcode != Opcode.Symbol)
            {
                opcode = "     " + opcode;
                foreach (var op in Operands)
                    operands += $" {op}";
            }
            else
                opcode = $"\n{opcode} {Operands[0]}:";
                // operands = $"{Operands[0]}:";

            var comment = "";
            if (!String.IsNullOrEmpty(Comment))
                comment = $" ; {Comment}";

            return $"{addressTxt}{opcode,-20}{operands,-15}{comment}";
        }

    }
}
