using System;

namespace Lomont.ClScript.CompilerLib
{
    public enum OperandType
    {
        // size/type of item/opcode
        Int32,
        Float32,
        Byte,

        // where item exists
        Global, // absolute address
        Local,  // relative to base pointer address
        Const,   // in const memory (ROM/FLASH, etc)
        
        None=Int32 // duplicate default
    }

    public class Instruction
    {
        public Opcode Opcode { get; }
        public object[] Operands { get; }
        public OperandType OperandType { get; }
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
            else if (OperandType == OperandType.Const)
                opcode += ".C";
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
