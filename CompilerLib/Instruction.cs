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
        Float32
    }

    public enum Opcode
    {
        Nop,

        // stack
        Push,
        Pop,
        Pick,
        Dup,
        Swap,
        AddStack,

        // mem
        LoadGlobal,
        LoadLocal,
        Store,
        LocAddr,

        // label/branch/call/ret
        Call,
        Return,
        BrFalse,
        BrAlways,
        ForStart,
        ForLoop,
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

    static class Emit
    {
        #region static Instruction generation

        public static Instruction Symbol(SymbolEntry symbol, string comment = "")
        {
            return new Instruction(Opcode.Symbol, OperandType.None, comment, symbol);
        }

        #region Stack

        public static Instruction Push(object value, OperandType type = OperandType.Int32, string comment = "")
        {
            return new Instruction(Opcode.Push,type, comment,value);
        }

        public static Instruction Pop(int count)
        {
            return new Instruction(Opcode.Pop, OperandType.None, "", count);
        }

        public static Instruction Pick(int value)
        {
            return new Instruction(Opcode.Pick, OperandType.None, "", value);
        }
        public static Instruction Dup(int count)
        {
            return new Instruction(Opcode.Dup, OperandType.None, "", count);
        }
        public static Instruction Swap()
        {
            return new Instruction(Opcode.Swap, OperandType.None, "");
        }
        public static Instruction AddStack(int value, string comment)
        {
            return new Instruction(Opcode.AddStack, OperandType.None, comment, value);
        }

        #endregion

        #region Memory
        public static Instruction LoadGlobal(int address, string comment)
        {
            return new Instruction(Opcode.LoadGlobal, OperandType.None, comment, address);
        }
        public static Instruction LoadLocal(int address, string comment)
        {
            return new Instruction(Opcode.LoadLocal, OperandType.None, comment, address);
        }
        public static Instruction Store(OperandType type)
        {
            return new Instruction(Opcode.Store, type, "");
        }

        public static Instruction LocalAddress(int address, string comment)
        {
            return new Instruction(Opcode.LocAddr, OperandType.None, comment, address);
        }
        #endregion

        #region call/return/branch/label
        public static Instruction Label(string label, string comment = "")
        {
            return new Instruction(Opcode.Label, OperandType.None, comment, label);
        }

        public static Instruction Call(string value)
        {
            return new Instruction(Opcode.Call, OperandType.None, "", value);
        }

        public static Instruction Return(int size)
        {
            return new Instruction(Opcode.Return, OperandType.None, "", size);
        }

        public static Instruction BrFalse(string label)
        {
            return new Instruction(Opcode.BrFalse, OperandType.None, "", label);
        }

        public static Instruction BrAlways(string label)
        {
            return new Instruction(Opcode.BrAlways, OperandType.None, "", label);
        }

        public static Instruction ForStart(int address, string comment)
        { // start, end, delta values on stack. If delta = 0, compute delta +1 or -1
          // store start at memory location, delta at location +1
          // pops 2 from stack
            return new Instruction(Opcode.ForStart, OperandType.None, comment, address);
        }

        // update for stack frame, branch if more to do
        public static Instruction ForLoop(int localAddress, string label, string comment)
        {
            // end address is counter, then increment, end is on stack top
            // pop end value. If more, loop, else don't
            return new Instruction(Opcode.ForLoop, OperandType.None, comment, localAddress, label);
        }

        // empty instruction
        public static Instruction Nop()
        {
            return new Instruction(Opcode.Nop, OperandType.None, "");
        }

        #endregion

        #region bitwise
        public static Instruction Or()
        {
            return new Instruction(Opcode.Or, OperandType.None, "");
        }

        public static Instruction And()
        {
            return new Instruction(Opcode.And, OperandType.None, "");
        }

        public static Instruction Xor()
        {
            return new Instruction(Opcode.Xor, OperandType.None, "");
        }

        public static Instruction Not()
        {
            return new Instruction(Opcode.Not, OperandType.None, "");
        }
        public static Instruction RightShift(OperandType type)
        {
            return new Instruction(Opcode.RightShift, type, "");
        }

        public static Instruction LeftShift(OperandType type)
        {
            return new Instruction(Opcode.LeftShift, OperandType.None, "");
        }

        public static Instruction RightRotate(OperandType type)
        {
            return new Instruction(Opcode.RightRotate, type, "");
        }

        public static Instruction LeftRotate(OperandType type)
        {
            return new Instruction(Opcode.LeftRotate, type, "");
        }

        #endregion

        #region Comparison
        public static Instruction NotEqual(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.NotEqual, type, "");
        }

        public static Instruction IsEqual(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.IsEqual, type, "");
        }

        public static Instruction GreaterThan(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.GreaterThan, type, "");
        }

        public static Instruction GreaterThanOrEqual(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.GreaterThanOrEqual, type, "");
        }


        public static Instruction LessThanOrEqual(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.LessThanOrEqual, type, "");
        }

        public static Instruction LessThan(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.LessThan, type, "");
        }
        #endregion

        #region Arithmetic
        public static Instruction Neg(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Neg, type, "");
        }

        public static Instruction Add(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Add, type, "");
        }

        public static Instruction Sub(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Sub, type, "");
        }

        public static Instruction Mul(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Mul, type, "");
        }

        public static Instruction Div(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Div,type, "");
        }

        public static Instruction Mod(OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Mod, type, "");
        }

        #endregion

        #endregion

    }
}
