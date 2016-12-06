using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{
    public class BytecodeGen
    {
        // the final compiled assembly
        public byte[] CompiledAssembly { get; set; }

        List<byte> code = new List<byte>();
        SymbolTableManager table;
        Environment env;
        Dictionary<string, int> labelAddresses;

        public BytecodeGen(Environment environment)
        {
            env = environment;
        }

        // address, symbol needed, if it is relative or absolute
        List<Tuple<int,string,bool>> fixups;

        // generate byte code, return true on success
        public bool Generate(SymbolTableManager symbolTable, List<Instruction> instructions)
        {
            table = symbolTable;
            CompiledAssembly = null;
            code = new List<byte>();
            labelAddresses = new Dictionary<string, int>();
            fixups = new List<Tuple<int, string, bool>>();

            foreach (var inst in instructions)
                Encode(inst);

            foreach (var fix in fixups)
            {
                var target = fix.Item1;     // where fixup is written to
                var label  = fix.Item2;      // label to go to
                var isRelative = fix.Item3; // is it relative? else absolute

                if (labelAddresses.ContainsKey(label))
                {
                    var address = labelAddresses[label];
                    Fixup(address, target, isRelative);
                }
                else
                    throw new InternalFailure($"Label {label} not in labels addresses");
            }
            CompiledAssembly = code.ToArray();
            return env.ErrorCount == 0;
        }

        void Fixup(int codeAddress, int targetAddress, bool isRelative)
        {
            var delta = (uint)(isRelative?targetAddress-codeAddress:targetAddress);
            WriteTo(delta,codeAddress);
        }


        void Encode(Instruction inst)
        {
            if (inst.Opcode == Opcode.Label)
            {
                var label = (string) inst.Operands[0];
                if (labelAddresses.ContainsKey(label))
                    throw new InternalFailure($"Duplicate label {label}");
                labelAddresses.Add(label,address);
                return;
            }

            // always write the opcode
            Write(inst.Opcode, inst.OperandType);

            // handle parameters
            switch (inst.Opcode)
            {
                // single int32 operand follows
                case Opcode.Pick:
                case Opcode.Dup:
                case Opcode.AddStack:
                case Opcode.LoadGlobal:
                case Opcode.LoadLocal:
                case Opcode.LocAddr:
                case Opcode.Return:
                case Opcode.ForStart:
                    Write((uint)((int)inst.Operands[0]), 4);
                    break;

                // address then label
                case Opcode.ForLoop:
                    Write((uint)((int)inst.Operands[0]), 4);
                    AddFixup((string)inst.Operands[1]);
                    break;

                // single int32/float32 operand follows
                case Opcode.Push:
                    if (inst.OperandType == OperandType.Int32)
                        Write((uint)((int)inst.Operands[0]),4);
                    else if (inst.OperandType == OperandType.Float32)
                        Write((float)(double)(inst.Operands[0]));
                    else
                        throw new InternalFailure($"Unsupported type {inst.OperandType}");
                    break;

                // these take a label
                case Opcode.Call:
                case Opcode.BrFalse:
                case Opcode.BrAlways:
                    AddFixup((string)inst.Operands[0]);
                    break;

                case Opcode.Label: // done above - special case
                    break;

                // nothing to do for these
                case Opcode.Store:
                case Opcode.Pop:
                case Opcode.Nop:
                case Opcode.Swap:
                case Opcode.Or:
                case Opcode.And:
                case Opcode.Xor:
                case Opcode.Not:
                case Opcode.RightShift:
                case Opcode.LeftShift:
                case Opcode.RightRotate:
                case Opcode.LeftRotate:
                case Opcode.NotEqual:
                case Opcode.IsEqual:
                case Opcode.GreaterThan:
                case Opcode.GreaterThanOrEqual:
                case Opcode.LessThanOrEqual:
                case Opcode.LessThan:
                case Opcode.Neg:
                case Opcode.Add:
                case Opcode.Sub:
                case Opcode.Mul:
                case Opcode.Div:
                case Opcode.Mod:
                    if (inst.Operands != null && inst.Operands.Length > 0)
                    {
                        env.Warning($"Instruction {inst} has operands, bytecode failed to write");
                        // todo - reinstate  throw new InternalFailure($"Instruction {inst} has operands, bytecode failed to write");
                    }
                    break;
                default:
                    throw new InternalFailure($"Bytecode missing instruction {inst}");
            }

        }

        // add a label to fix later, with ther result put here. Adds 4 bytes to output
        void AddFixup(string label)
        {
            fixups.Add(new Tuple<int, string, bool>(address, label, true));
            Write(0,4); // space for result to be written here
        }

        void Write(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            foreach (var b in bytes)
                WriteCodeByte(b);
        }

        int address = 0;


        // send all byte writes through here that update address
        void WriteCodeByte(byte b)
        {
            if (address < code.Count)
                code[address] = b;
            else
                code.Add(b);
            ++address;
        }


        // default byte packing
        void Write(Opcode opcode, OperandType operandType)
        {
            if ((int)opcode >= 64)
                throw new InternalFailure($"Opcode {opcode} must be smaller than 64, is now {(int)opcode}.");
            if ((int)operandType >= 4)
                throw new InternalFailure($"Opcode {operandType} must be smaller than 4, is now {(int)operandType}");

            var op = ((uint) (operandType) << 6) | (uint)opcode;
            Write(op,1);
        }

        // write some bytes, MSB first
        void Write(uint value, int numBytes)
        {
            var shift = (numBytes - 1)*8; // to get highest byte to write
            for (var i = 0; i < numBytes; ++i)
            {
                byte b = (byte) (value >> shift);
                WriteCodeByte(b);
                shift -= 8; // smaller
            }
        }

        void WriteTo(uint value, int addressToWrite)
        {
            var temp = address;
            address = addressToWrite;
            Write(value,4);
            address = temp;
        }

    }
}
