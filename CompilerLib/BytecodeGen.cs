using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{


    /* Format for an assembly (values big endian):
     * 
     * RIFF format is composed of chunks:
     *    each chunk is a 4 byte chunk name (RIFF requires ASCII, we relax it), 
     *    then 4 byte big endian size of chunk (excluding chunk name and chunk size) (usual RIFF little endian here!)
     *    then data, padded at end with 0 to make even sized
     *    
     * Skip any chunks not understood
     *    
     * Other than size fields, all values big endian   
     *    
     * Structure (chunk name and data, size field implied):  
     *    Chunk "RIFF" (required by the format)
     *        data is 4 byte: "CLSC" ASCII
     *        then 2 byte version (major, then minor)
     *        
     *        Then chunks. "code" and "link" required. Others optional
     *        
     *        Chunk "code" : the code to execute
     *            code binary blob
     *        Chunk "link" : link entries have imports and exports to link with
     *            4 byte number L1 of link imports
     *            4 byte number L2 of link exports
     *            L1 4 byte entries of offsets to import link entry from start of link section
     *            L1 4 byte entries of offsets to export link entry from start of link section
     *            
     *            Link entries. Each is
     *                4 byte unique id (start at 0, incremented for each link item, marks calling address from code)
     *                4 byte address of item from start of code blob
     *                4 byte # stack entries return values
     *                4 byte # stack entries parameter values
     *                4 byte # of attributes
     *                0 terminated UTF-8 item name
     *                Attributes: each 
     *                     0 terminated UTF-8 attribute name
     *                     4 byte byte # of parameters for attribute
     *                     0 terminated UTF-8 parameter strings
     *                     
     *        Chunk "init" : If present, code to execute on load to set up globals. 
     *            4 byte ending address, starts at 0
     *            
     *        Chunk "img0" : global vars for the assembly (todo)
     *            one byte 0 (no chunk padding) or 1 (chunk padding)
     *            initialization image (copied into RAM, stack pointer points past it to start
     *            
     *        Chunk "text" : string table (todo)
     * 
     * todo - types?, RAM/ROM distinction, item type (var or func)
     * 
     */
    public class BytecodeGen
    {
        /// <summary>
        /// Generation version 0.1
        /// </summary>
        public ushort GenVersion => 0x0001; // major.minor each stored as a byte

        // the final compiled assembly
        public byte[] CompiledAssembly { get; set; }

        List<byte> code = new List<byte>();
        readonly Environment env;
        Dictionary<string, int> labelAddresses;
        
        // where import/export/attribute items are stored
        readonly List<LinkEntry> linkEntries = new List<LinkEntry>();

        public BytecodeGen(Environment environment)
        {
            env = environment;
        }

        // address, symbol needed, if it is relative or absolute
        List<Tuple<int,string,bool>> fixups;

        // generate byte code, return true on success
        public bool Generate(List<Instruction> instructions)
        {
            CompiledAssembly = null;
            code = new List<byte>();
            labelAddresses = new Dictionary<string, int>();
            fixups = new List<Tuple<int, string, bool>>();

            foreach (var inst in instructions)
                Encode(inst);

            var imports = linkEntries.Where(le => le.Flags.HasFlag(SymbolAttribute.Import)).ToList();

            foreach (var fix in fixups)
            {
                var target = fix.Item1;     // where fixup is written to
                var label  = fix.Item2;     // label to go to
                var isRelative = fix.Item3; // is it relative? else absolute

                if (label.StartsWith(CodeGeneratorVisitor.ImportPrefix))
                {
                    var importName = label.Substring(CodeGeneratorVisitor.ImportPrefix.Length);
                    var index = imports.FindIndex(le => le.Name == importName);
                    if (index < 0)
                        throw new InternalFailure($"Unknown import {importName}");
                    Fixup(index,target,false);
                }
                else if (labelAddresses.ContainsKey(label))
                {
                    var fixAddress = labelAddresses[label];
                    Fixup(fixAddress, target, isRelative);
                }
                else
                    throw new InternalFailure($"Label {label} not in labels addresses");
            }

            WriteAssembly();
            return env.ErrorCount == 0;
        }

        void WriteAssembly()
        {
            var assembly = new List<byte>();
            WriteChunk(assembly,"RIFF");

            ByteWriter.Write(assembly, "CLSC", false);
            ByteWriter.Write(assembly,GenVersion>>8,1);
            ByteWriter.Write(assembly, GenVersion&255, 1);

            WriteChunk(assembly, "code", code);

            if (labelAddresses.ContainsKey(CodeGeneratorVisitor.GlobalStartSymbol))
            {
                var initLength = labelAddresses[CodeGeneratorVisitor.GlobalEndSymbol] -
                                 labelAddresses[CodeGeneratorVisitor.GlobalStartSymbol];
                var init = new List<byte>();
                ByteWriter.Write(init, initLength, 4);
                WriteChunk(assembly, "init", init);
            }

            var linkData = new List<byte>();
            WriteLinkEntries(linkData);

            WriteChunk(assembly, "link", linkData);


            // write some testing nonsense chunks
/*            var rand = new Random(1234);
            for (var i = 0; i < rand.Next(1,5); ++i)
            {
                var data = new byte[rand.Next(7, 29)];
                rand.NextBytes(data);
                var name = $"abc" + i;
                WriteChunk(assembly,name,data.ToList());
                env.Warning($"Writing garbage chunk {name} for testing");
            } */


            // var empty = new List<byte>();
            // WriteChunk(assembly, "img0", empty); // initial RAM image
            // WriteChunk(assembly, "text", empty); // string table & const data

            // fill in size
            ByteWriter.Write(assembly,assembly.Count-8,4,4);

            CompiledAssembly = assembly.ToArray();
        }

        void WriteChunk(List<byte> destination, string chunkName, List<byte> chunkData = null)
        {
            for (var i =0; i < 4; ++i)
                destination.Add((byte)chunkName[i]);
            var len = chunkData?.Count ?? 0;
            if ((len & 1) == 1)
                ++len; // even
            ByteWriter.Write(destination, len, 4);
            if (chunkData != null)
            {
                destination.AddRange(chunkData);
                if ((chunkData.Count & 1) == 1)
                    destination.Add(0); // even padded
            }
        }

        void WriteLinkEntries(List<byte> linkData)
        {
            var imports = linkEntries.Where(v => v.Flags.HasFlag(SymbolAttribute.Import)).ToList();
            var exports = linkEntries.Where(v => v.Flags.HasFlag(SymbolAttribute.Export)).ToList();
            var importCount = imports.Count;
            var exportCount = exports.Count;
            if (importCount + exportCount != linkEntries.Count)
                throw new InternalFailure($"Import {importCount} and export {exportCount} link entries don't add up to {linkEntries.Count}");
            ByteWriter.Write(linkData, importCount, 4);
            ByteWriter.Write(linkData, exportCount, 4);


            var start = linkData.Count + 4 * (importCount+exportCount); // byte offset from "link" chunk start, excluding 8 byte header

            var temp = new List<byte>();
            foreach (var entry in imports)
            {
                ByteWriter.Write(linkData,temp.Count+start,4);
                entry.Write(temp);
            }
            foreach (var entry in exports)
            {
                ByteWriter.Write(linkData, temp.Count + start, 4);
                entry.Write(temp);
            }

            linkData.AddRange(temp);
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
                        Pack((uint)((int)item));
                    break;

                // single int32 operand follows
                case Opcode.PopStack:
                case Opcode.Reverse:
                // case Opcode.Pick:
                case Opcode.ClearStack:
                case Opcode.Load:
                case Opcode.Addr:
                case Opcode.ForStart:
                    Pack((uint)((int)inst.Operands[0]));
                    break;

                // two int32 operand follows
                case Opcode.Array:
                case Opcode.Return:
                    Pack((uint)((int)inst.Operands[0]));
                    Pack((uint)((int)inst.Operands[1]));
                    break;

                // address then label
                case Opcode.ForLoop:
                    Pack((uint)((int)inst.Operands[0]));
                    AddFixup((string)inst.Operands[1]);
                    break;

                // single byte/int32/float32 operand follows
                case Opcode.Push:
                    if (inst.OperandType == OperandType.Byte)
                        Write((uint)((int)inst.Operands[0]), 1);
                    else if (inst.OperandType == OperandType.Int32)
                        Pack((uint)((int)inst.Operands[0]));
                    else if (inst.OperandType == OperandType.Float32)
                        Write((float)(double)(inst.Operands[0]));
                    else
                        throw new InternalFailure($"Unsupported type {inst.OperandType}");
                    break;

                // these take a label
                case Opcode.Call:
                case Opcode.BrFalse:
                case Opcode.BrTrue:
                case Opcode.BrAlways:
                    AddFixup((string)inst.Operands[0]);
                    break;

                // unique?
                case Opcode.Update:
                    WriteCodeByte((byte)((int)inst.Operands[0])); // opcode
                    WriteCodeByte((byte)((int)inst.Operands[1])); // pre-increment
                    break;

                // done above - special case
                case Opcode.Label:  
                case Opcode.Symbol: 
                    break;

                // nothing to do for these
                case Opcode.Dup:
                case Opcode.Swap:
                case Opcode.Rot3:
                case Opcode.Read: 
                case Opcode.Write: 
                case Opcode.Pop:
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
                case Opcode.F2I:
                case Opcode.I2F:
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
            var flags = symbol.Attrib;
            // create item to import/export/attribute, etc
            if (flags.HasFlag(SymbolAttribute.Export) || flags.HasFlag(SymbolAttribute.Import))
            {
                
                //if (flags.HasFlag( SymbolAttribute.Import))
                //    env.Info($"Importing symbol {symbol.Name}");
                //if (flags.HasFlag(SymbolAttribute.Export))
                //    env.Info($"Exporting symbol {symbol.Name}");

                var funcType = symbol.Type as FunctionType;
                if (funcType == null)
                    throw new InternalFailure($"Link entry only supports functions, was {symbol}");

                var linkEntry = new LinkEntry(symbol.Name,flags,address, funcType.ReturnType.Tuple.Count, funcType.ParamsType.Tuple.Count,symbol.UniqueId);
                if (symbol.Attributes.Any())
                    linkEntry.Attributes = symbol.Attributes;
                linkEntries.Add(linkEntry);
            }
        }

        class LinkEntry
        {
            public LinkEntry(string name, SymbolAttribute flags, int address, int returnEntries, int paramEntries, int uniqueId)
            {
                Address = address;
                Name = name;
                Flags = flags;
                ReturnEntries = returnEntries;
                ParameterEntries = paramEntries;
                UniqueId = uniqueId;
            }

            public int UniqueId { get; }

            public int ReturnEntries { get; }

            public int ParameterEntries { get; }

            public SymbolAttribute Flags { get; }

            public int Address { get; }

            public string Name { get; }

            public List<Attribute> Attributes { get; set; }

            // write into a byte assembly
            public void Write(List<byte> bytes)
            {
                ByteWriter.Write(bytes, UniqueId, 4);
                ByteWriter.Write(bytes, Address, 4);
                ByteWriter.Write(bytes, ReturnEntries, 4);
                ByteWriter.Write(bytes, ParameterEntries, 4);
                ByteWriter.Write(bytes, Attributes?.Count ?? 0, 4);
                ByteWriter.Write(bytes, Name);
                if (Attributes != null)
                {
                    foreach (var a in Attributes)
                    {
                        ByteWriter.Write(bytes, a.Name);
                        ByteWriter.Write(bytes, a.Parameters.Count, 4);
                        foreach (var p in a.Parameters)
                            ByteWriter.Write(bytes, p);
                    }
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
            var i32 = Runtime.Float32ToInt32(value);
            Write((uint)i32,4);
        }

        int address;

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
            //Write((uint)opcode,1);
            //Write((uint)operandType, 1);

            //var v = 6*(uint) opcode + (uint) operandType;
            //Write(v, 1);
            //
            //if ((int)operandType >= 7)
            //    throw new InternalFailure($"Opcode {operandType} must be smaller than 7, is now {(int)operandType}");
            //if ((int)opcode*6+5 >= 255)
            //    throw new InternalFailure($"Opcode {opcode} too large, is now {(int)opcode}.");

            Write(OpcodeMap.GetEntry(opcode,operandType),1);

        }

        // int pack1 = 0, pack2 = 0, pack3 = 0;
        // pack 32 bit value into as few bytes as possible
        // format: 
        void Pack(uint value)
        {
#if false
            Write(value,4);
            //var v = (int) value;
            //if (-63 <= v && v <= 63)
            //    pack1++;
            //else
            //{
            //    pack2++;
            //    env.Info($"Large {v}");
            //}
            //env.Info($"Pack1 {pack1} pack2 {pack2}");
#else
            // simple encoding: -127 to 127 stored as byte in 0-254, else 255 stored then 4 byte int
            var v = (int)value;
            if (-127 <= v && v <= 127)
                Write((uint)(v + 127), 1);
            else
            {
                Write(255, 1);
                Write(value, 4);
            }
#endif
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
        public static void Write(List<byte> bytes, string text, bool zeroTerminated = true)
        {
            var txt = Encoding.UTF8.GetBytes(text);
            foreach (var b in txt)
                bytes.Add(b);
            if (zeroTerminated)
                bytes.Add(0); // 0 terminated
        }

        // MSB
        public static void Write(List<byte> bytes, int value, int length, int address = -1)
        {
            var shift = length * 8 - 8;
            for (var i = 0; i < length; ++i)
            {
                var b = (byte) (value >> shift);
                if (address == -1)
                    bytes.Add(b);
                else
                    bytes[address++] = b;
                shift -= 8;
            }
        }

    }
}
