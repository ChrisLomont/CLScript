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


    /* Format for an assembly (values big endian):
     * 
     * RIFF format: in chunks:
     *    each with 4 byte identifier (RIFF requires ASCII, we relax it), 
     *    then 4 byte little endian size of chunk (size except header and this size field)
     *    data, padded at end with 0 to make even sized
     *    
     * Skip any chunks not understood
     *    
     * Other than size fields, all values big endian   
     *    
     * Structure (chunk name and data, size field implied):  
     *    Chunk "RIFF" (required by the format)
     *        data is "CLSx" where x is a version byte, "CLS" ASCII
     *        Chunk "head"
     *            4 byte length of assembly
     *        Chunk "code"
     *            code binary blob
     *        Chunk "link"
     *            4 byte number L of link entries
     *            L 4 byte entries of offsets to each link entry from start of image
     *            Link entries. Each is
     *            todo;;;;
     *                2 byte length
     *                4 byte address of item from start of bytecode
     *                0 terminated UTF-8 strings
     *            
     *       
     *    
     * 
     * todo - types?, RAM/ROM distinction, item type (var or func)
     * todo - initial memory values
     * todo - string table/code table
     * todo - imported calls
     * 
     * Link entry: types, name, attributes, address in code/global memory
     * 
     */
    public class BytecodeGen
    {
        /// <summary>
        /// Generation version 0.1
        /// </summary>
        public int GenVersion => 1; // major.minor stored as nibbles

        // the final compiled assembly
        public byte[] CompiledAssembly { get; set; }

        List<byte> code = new List<byte>();
        SymbolTableManager table;
        Environment env;
        Dictionary<string, int> labelAddresses;
        // where import/export/attribute items are stored
        List<LinkEntry> linkEntries = new List<LinkEntry>();

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
                var label  = fix.Item2;     // label to go to
                var isRelative = fix.Item3; // is it relative? else absolute

                if (labelAddresses.ContainsKey(label))
                {
                    var address = labelAddresses[label];
                    Fixup(address, target, isRelative);
                }
                else
                    throw new InternalFailure($"Label {label} not in labels addresses");
            }
            WriteAssembly();
            return env.ErrorCount == 0;
        }

        void WriteAssembly()
        {
            // 4 byte Identifier: CLSx where x is a version byte, "CLS" UTF - 8
            // 4 byte length of assembly
            // 4 byte offset (from start of bytecode) to start of code
            // 
            // 4 byte number of LinkEntries
            // LinkEntries
            // 
            // Code

            var linkData = new List<byte>();
            ByteWriter.Write(linkData, linkEntries.Count, 4);
            foreach (var l in linkEntries)
                l.Write(linkData);

            var header = new List<byte>();
            ByteWriter.Write(header, 'C', 1);
            ByteWriter.Write(header, 'L', 1);
            ByteWriter.Write(header, 'S', 1);
            ByteWriter.Write(header, GenVersion, 1);
            ByteWriter.Write(header, linkData.Count + header.Count + 8 + code.Count, 4); // total length
            ByteWriter.Write(header, linkData.Count + header.Count+4, 4);                // code offset

            header.AddRange(linkData);

            header.AddRange(code);

            //for (var i =0; i < Math.Min(10,code.Count); ++i)
            //    env.Info($"Code byte 0x{code[i]:X2}");

            CompiledAssembly = header.ToArray();
        }

        // write code address into target address. 
        // If isRelative is true, write codeAddress-targetAddress
        void Fixup(int codeAddress, int targetAddress, bool isRelative)
        {
            var delta = (uint)(isRelative?codeAddress - targetAddress: codeAddress);
            WriteTo(delta,targetAddress);
        }


        void Encode(Instruction inst)
        {
            inst.Address = address;
            if (inst.Opcode == Opcode.Label)
            {
                var label = (string) inst.Operands[0];
                if (labelAddresses.ContainsKey(label))
                    throw new InternalFailure($"Duplicate label {label}");
                labelAddresses.Add(label,address);
                return;
            }

            if (inst.Opcode == Opcode.Symbol)
            {
                ProcessSymbol(inst.Operands[0] as SymbolEntry);
                return;
            }

            // always write the opcode
            Write(inst.Opcode, inst.OperandType);

            // handle parameters
            switch (inst.Opcode)
            {
                // arbitrary int32 values
                case Opcode.MakeArr:
                    foreach (var item in inst.Operands)
                        Write((uint)((int)item), 4);
                    break;

                // single int32 operand follows
                case Opcode.PopStack:
                case Opcode.Pick:
                case Opcode.ClearStack:
                case Opcode.Load:
                case Opcode.Addr:
                case Opcode.ForStart:
                    Write((uint)((int)inst.Operands[0]), 4);
                    break;

                // two int32 operand follows
                case Opcode.Array:
                case Opcode.Return:
                    Write((uint)((int)inst.Operands[0]), 4);
                    Write((uint)((int)inst.Operands[1]), 4);
                    break;

                // address then label
                case Opcode.ForLoop:
                    Write((uint)((int)inst.Operands[0]), 4);
                    AddFixup((string)inst.Operands[1]);
                    break;

                // single byte/int32/float32 operand follows
                case Opcode.Push:
                    if (inst.OperandType == OperandType.Byte)
                        Write((uint)((int)inst.Operands[0]), 1);
                    else if (inst.OperandType == OperandType.Int32)
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

                // done above - special case
                case Opcode.Label:  
                case Opcode.Symbol: 
                    break;

                // nothing to do for these
                case Opcode.Dup:
                case Opcode.Read: 
                case Opcode.Write: 
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

        void ProcessSymbol(SymbolEntry symbol)
        {
            // create item to import/export/attribute, etc
            if (symbol.Attributes.Any())
            {
                // add a link entry
                var e = new LinkEntry(address);
                e.Attributes = symbol.Attributes;
                linkEntries.Add(e);
            }

        }


        class LinkEntry
        {
            public LinkEntry(int address)
            {
                Address = address;
            }

            public int Address;
            public List<Attribute> Attributes { get; set; }

            // write into a byte assembly
            public void Write(List<byte> bytes)
            {
                // 2 byte length
                // 4 byte address of item from start of bytecode
                // 0 terminated UTF-8 strings
                var length = 2 + 4 + 
                    Attributes.Sum(s=>s.Name.Length+1 + s.Parameters.Sum(p=>p.Length+1));
                if (length > 65535)
                    throw new InternalFailure("Link entry too large");
                ByteWriter.Write(bytes, length, 2);
                ByteWriter.Write(bytes, Address, 4);
                foreach (var a in Attributes)
                {
                    ByteWriter.Write(bytes, a.Name);
                    foreach (var p in a.Parameters)
                        ByteWriter.Write(bytes, p);
                }
            }
        }

        // add a label to fix later, with the result put here. Adds 4 bytes to output
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
            //if ((int)opcode >= 64)
            //    throw new InternalFailure($"Opcode {opcode} must be smaller than 64, is now {(int)opcode}.");
            //if ((int)operandType >= 4)
            //    throw new InternalFailure($"Opcode {operandType} must be smaller than 4, is now {(int)operandType}");

            //var op = ((uint) (operandType) << 6) | (uint)opcode;
            //Write(op,1);
            Write((uint)opcode,1);
            Write((uint)operandType, 1);
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

    static class ByteWriter
    {
        public static void Write(List<byte> bytes, string text)
        {
            // todo - above spacing was based on 8 bit UTF-8 characters...
            // either enforce this, or measure length correctly

            var txt = UTF8Encoding.UTF8.GetBytes(text);
            foreach (var b in txt)
                bytes.Add(b);
            bytes.Add(0); // 0 terminated
        }

        // MSB
        public static void Write(List<byte> bytes, int value, int length)
        {
            var shift = length * 8 - 8;
            for (var i = 0; i < length; ++i)
            {
                bytes.Add((byte)(value >> shift));
                shift -= 8;
            }
        }

    }
}
