using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    class Opcode
    {
        string text;
        object[] operands;
        Opcode(string opcode, params object [] operands)
        {
            text = opcode;
            this.operands = operands;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!text.StartsWith("Label"))
            {
                sb.Append("    " + text);
                foreach (var op in operands)
                    sb.Append($" {op}");
            }
            else
                sb.Append($"{operands[0]}:");
            return sb.ToString();
        }

        #region static Opcode generation
        public static Opcode Push32(int value)
        {
            return new Opcode("Push32",value);
        }

        public static Opcode PushF(double value)
        {
            return new Opcode("PushF", value);
        }

        public static Opcode Call(string value)
        {
            return new Opcode("Call", value);
        }

        public static Opcode Xor()
        {
            return new Opcode("Xor");
        }

        public static Opcode Not()
        {
            return new Opcode("Not");
        }

        public static Opcode Neg()
        {
            return new Opcode("Neg");
        }

        public static Opcode NegF()
        {
            return new Opcode("NegF");
        }

        public static Opcode NotEqual()
        {
            return new Opcode("NotEqual");
        }

        public static Opcode NotEqualF()
        {
            return new Opcode("NotEqualF");
        }

        public static Opcode Compare()
        {
            return new Opcode("Compare");
        }

        public static Opcode CompareF()
        {
            return new Opcode("CompareF");
        }

        public static Opcode GreaterThan()
        {
            return new Opcode("GreaterThan");
        }

        public static Opcode GreaterThanF()
        {
            return new Opcode("GreaterThanF");
        }

        public static Opcode GreaterThanOrEqual()
        {
            return new Opcode("GreaterThanOrEqual");
        }

        public static Opcode GreaterThanOrEqualF()
        {
            return new Opcode("GreaterThanOrEqualF");
        }

        public static Opcode LessThanOrEqual()
        {
            return new Opcode("LessThanOrEqual");
        }

        public static Opcode LessThanOrEqualF()
        {
            return new Opcode("LessThanOrEqualF");
        }

        public static Opcode LessThan()
        {
            return new Opcode("LessThan");
        }

        public static Opcode LessThanF()
        {
            return new Opcode("LessThanF");
        }

        public static Opcode Or()
        {
            return new Opcode("Or");
        }

        public static Opcode And()
        {
            return new Opcode("And");
        }

        public static Opcode RightShift()
        {
            return new Opcode("RightShift");
        }

        public static Opcode LeftShift()
        {
            return new Opcode("LeftShift");
        }

        public static Opcode RightRotate()
        {
            return new Opcode("RightRotate");
        }

        public static Opcode LeftRotate()
        {
            return new Opcode("LeftRotate");
        }

        public static Opcode Mod()
        {
            return new Opcode("Mod");
        }

        public static Opcode Add()
        {
            return new Opcode("Add");
        }

        public static Opcode Sub()
        {
            return new Opcode("Sub");
        }

        public static Opcode Mul()
        {
            return new Opcode("Mul");
        }

        public static Opcode Div()
        {
            return new Opcode("Div");
        }

        public static Opcode AddF()
        {
            return new Opcode("AddF");
        }

        public static Opcode SubF()
        {
            return new Opcode("SubF");
        }

        public static Opcode MulF()
        {
            return new Opcode("MulF");
        }

        public static Opcode DivF()
        {
            return new Opcode("DivF");
        }

        public static Opcode Load(string name)
        {
            return new Opcode("Load", name, "TODO");
        }
        public static Opcode Store()
        {
            return new Opcode("Store","TODO");
        }

        public static Opcode LoadAddress(string name)
        {
            return new Opcode("LoadAddress", name, "TODO");
        }

        public static Opcode Label(string label)
        {
            return new Opcode("Label", label);
        }

        public static Opcode Return()
        {
            return new Opcode("Return","TODO");
        }

        public static Opcode BrFalse(string label)
        {
            return new Opcode("BrFalse", label);
        }

        public static Opcode BrAlways(string label)
        {
            return new Opcode("BrAlways", label);
        }

        public static Opcode Pick(int value)
        {
            return new Opcode("Pick", value);
        }

        public static Opcode Pop(int count)
        {
            return new Opcode("Pop", count);
        }

        public static Opcode Dup(int count)
        {
            return new Opcode("Dup", count);
        }
        public static Opcode ForStart()
        { // create -1 or 1 on stack, for loop delta
            return new Opcode("ForStart");
        }

        public static Opcode ForLoop(string label)
        {
            return new Opcode("ForLoop", label);
        }
        #endregion

    }
}
