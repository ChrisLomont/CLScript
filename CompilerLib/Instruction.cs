﻿using System;
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

        // mem
        Load,
        Store,
        LoadAddress,
        // label/branch/call/ret
        Label,
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
        Mod
        // end
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
            var sb = new StringBuilder();
            var text = Opcode.ToString();
            if (OperandType == OperandType.Float32)
                text += ".f";
            else if (OperandType == OperandType.Int32)
                text += ".i";
            else if (OperandType == OperandType.Byte)
                text += ".b";
            else if (OperandType != OperandType.None)
                throw new InternalFailure($"Unknown operand type {OperandType}");
            if (!text.StartsWith("Label"))
            {
                sb.Append("    " + text);
                foreach (var op in Operands)
                    sb.Append($" {op}");
            }
            else
                sb.Append($"{Operands[0]}:");
            if (!String.IsNullOrEmpty(Comment))
                sb.Append($" ; {Comment}");
            return sb.ToString();
        }

    }

    static class Emit
    {
        #region static Instruction generation

        #region Stack

        public static Instruction Push(object value, OperandType type = OperandType.Int32, string comment = "")
        {
            return new Instruction(Opcode.Push,type, comment,value);
        }

        public static Instruction Pop(int count, OperandType type = OperandType.Int32)
        {
            return new Instruction(Opcode.Pop, type, "", count);
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

        #endregion

        #region Memory
        public static Instruction Load(int address, string comment)
        {
            return new Instruction(Opcode.Load, OperandType.None, comment, address, "TODO");
        }
        public static Instruction Store(OperandType type)
        {
            return new Instruction(Opcode.Store, type, "");
        }

        public static Instruction LoadAddress(int address, string comment)
        {
            return new Instruction(Opcode.LoadAddress, OperandType.None, comment, address, "TODO");
        }
        #endregion

        #region call/return/branch/label
        public static Instruction Label(string label)
        {
            return new Instruction(Opcode.Label, OperandType.None, "", label);
        }

        public static Instruction Call(string value)
        {
            return new Instruction(Opcode.Call, OperandType.None, "", value);
        }

        public static Instruction Return()
        {
            return new Instruction(Opcode.Return, OperandType.None, "", "TODO");
        }

        public static Instruction BrFalse(string label)
        {
            return new Instruction(Opcode.BrFalse, OperandType.None, "", label);
        }

        public static Instruction BrAlways(string label)
        {
            return new Instruction(Opcode.BrAlways, OperandType.None, "", label);
        }

        public static Instruction ForStart(string label)
        { // start, end, delta values on stack. If delta = 0, compute delta +1 or -1
          // store start at memory location, delta at location +1
          // pops 2 from stack
            return new Instruction(Opcode.ForStart, OperandType.None, "", label);
        }

        // update for stack frame, branch if more to do
        public static Instruction ForLoop(string variable, string label)
        {
            // end value on stack, 
            // add memory (at label plus 4) to memory, check if end. 
            // pop end value. If more, loop, else don't
            return new Instruction(Opcode.ForLoop, OperandType.None, "", variable, label);
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
