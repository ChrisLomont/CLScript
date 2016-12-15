﻿using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

namespace Lomont.ClScript.CompilerLib
{
    // run a compiled image for testing
    // runs from address 0
    public class Runtime
    {

        /* Format for an assembly (values big endian):
         * 
         * 4 byte Identifier: CLSx where x is a version byte, "CLS" UTF-8
         * 4 byte length of assembly
         * 4 byte offset (from start of bytecode) to start of code
         * 
         * 4 byte number of LinkEntries
         * LinkEntries
         * 
         * Code
         * 
         * LinkEntry is:
         *    2 byte length
         *    4 byte address of item from start of bytecode
         *    0 terminated UTF-8 strings
         * 
         * todo - types?, RAM/ROM distinction, item type (var or func)
         * todo - initial memory values
         * todo - enumerate attributes to allow finding multiple entry points
         */

        public Runtime(Environment environment)
        {
            env = environment;
        }

        /// <summary>
        /// Run an image, starting at the given attributed entry point
        /// </summary>
        /// <param name="image"></param>
        /// <param name="entryAttribute"></param>
        /// <returns></returns>
        public bool Run(byte[] image, string entryAttribute, int [] parameters, int [] returnValues)
        {
            useTracing = true;

            // todo - zero memory in C/C++ style environments
            // todo - get return size to start with
            return RunImage(image, entryAttribute, parameters, returnValues);
        }


        public static int GenVersion = 1; // major.minor nibbles of code
        public static int ArrayHeaderSize = 2; // in stack entries, these are before array address

        // all locals - minimize memory
        #region Locals

        Environment env;

        int StackPointer;
        int BasePointer;
        int ProgramCounter;
        int ImageLength;
        int LinkCount;
        int CodeStartOffset;

        // when true, all memory read/writes are blocked
        bool error;
        byte[] romImage1;
        int [] ramImage1 = new int[10000];

        // is tracing on?
        bool useTracing;

        #endregion

        // all memory accesses done through a few accessors for runtime security
        #region Memory access

        byte ReadRom(int offset, string errorMessage)
        {
            if (offset < 0 || romImage1.Length <= offset)
            {
                env.Error($"ROM out of bounds: {offset}, {errorMessage}");
                error = true;
            }
            if (error)
                return 0;
            return romImage1[offset];
        }

        int ReadRam(int offset, string errorMessage)
        {
            if (offset < 0 || ramImage1.Length <= offset)
            {
                env.Error($"RAM read out of bounds: {offset}, {errorMessage}");
                error = true;
            }
            if (error)
                return 0;
            Trace($" r:{ramImage1[offset]}");
            return ramImage1[offset];
        }

        bool WriteRam(int offset, int value, string errorMessage)
        {
            if (offset < 0 || ramImage1.Length <= offset)
            {
                env.Error($"RAM write out of bounds: {offset}, {errorMessage}");
                error = true;
            }
            if (error)
                return false;
            ramImage1[offset] = value;
            return true;
        }

        #endregion

        #region Setup

        bool RunImage(byte[] image, string entryAttribute, int[] parameters, int[] returnValues)
        {
            error = false;
            romImage1 = image;

            try
            {
                // min size
                if (image.Length < 12)
                {
                    env.Error("bytecode too short to be valid");
                    return false;
                }
                // read header
                if (image[0] != 'C' || image[1] != 'L' || image[2] != 'S' || image[3] != GenVersion)
                {
                    env.Error($"Invalid bytecode header");
                    return false;
                }

                ImageLength = ReadImageInt4(4, 4); // assembly length
                CodeStartOffset = ReadImageInt4(8, 4); // code offset
                LinkCount = ReadImageInt4(12, 4); // number of link entries
                env.Info($"Length {ImageLength}, offset {CodeStartOffset}, link count {LinkCount}");

                // find entry point
                var entryPoint = FindAttributeAddress(entryAttribute);
                if (entryPoint < 0)
                {
                    env.Error($"Cannot find entry point attribute {entryAttribute}");
                    return false;
                }

                return Process(entryPoint, parameters, returnValues);
            }
            catch (Exception ex)
            {
                env.Error("Exception: " + ex);
                return false;
            }
        }

        // find the attribute with the given name
        // return the associated address
        // return -1 if cannot be found
        int FindAttributeAddress(string attributeName)
        {
            var linkCount = ReadImageInt4(12, 4); // number of link entries
            var index = 16; // start here
            for (var i = 0; i < linkCount; ++i)
            {
                // LinkEntry is:
                // 2 byte length
                // 4 byte address of item from start of bytecode
                // 0 terminated UTF-8 strings
                var len = ReadImageInt4(index, 2);
                var addr = ReadImageInt4(index + 2, 4);
                var txt = ReadImageString(index + 6);
                if (txt == attributeName)
                    return addr;
                index += len;
            }
            return -1;
        }

        #endregion

        #region Execution


        // run the code, with the code at the given code offset in the assembly
        // and the entry point address into that
        bool Process(int startAddress, int[] parameters, int[] returnValues)
        {
            env.Info($"Processing code, offset {CodeStartOffset}, entry address {startAddress}");

            ProgramCounter = startAddress;
            BasePointer = -1; // out of bounds
            StackPointer = 0; // todo - start past globals, load them as block

            // create call stack
            // 1. Push space for return values
            for (var i = 0; i < returnValues.Length; ++i)
                PushStack(0);
            // 2. push parameters
            foreach (var v in parameters)
                PushStack(v);
            // 3. push ret code (special code to exit), and base pointer, set bp
            PushStack(returnExitAddress);
            PushStack(BasePointer);
            BasePointer = StackPointer; // frame start

            // now entry looks like a Call instruction to the code

            Trace("TRACE: Tracing" + System.Environment.NewLine);
            Trace("TRACE: PC   Opcode   ?    Operands     SP  BP  reads (c=code,r=ram)" + System.Environment.NewLine);

            while (!error)
            {
                if (!Execute())
                    break;
            }

            env.Info("");
            env.Info("Stackdump: ");
            DumpStack(StackPointer,10);

            return error;
        }


        // when a return jumps here, execution is done
        const int returnExitAddress = -1; 

        // execute the instruction at the current program counter
        // return true if not ending
        bool Execute()
        {
            bool retval = true; // assume this not last instruction
            int p1, p2;
            float f1, f2;

            p1 = ProgramCounter; // save before reading
            
            // read instruction
            var oldTrace = useTracing;
            useTracing = false; // do not trace these reads
            Opcode opcode = (Opcode) ReadCodeItem(OperandType.Byte);
            OperandType opType = (OperandType)ReadCodeItem(OperandType.Byte);
            useTracing = oldTrace; // restore tracing setting

            // trace address, instruction here
            Trace($"TRACE: {p1:X5}: {opcode,-10} {opType,-6} SP:{StackPointer:X4} BP:{BasePointer:X4}  : ");

            // handle parameters
            switch (opcode)
            {

                case Opcode.Nop:
                    break; // do nothing

                // stack
                case Opcode.Push:
                    PushStack(ReadCodeItem(opType));
                    break;
                case Opcode.Pop:
                    PopStack();
                    break;
                case Opcode.Pick:
                    p1 = ReadCodeItem(OperandType.Int32);
                    PushStack(ReadRam(StackPointer - p1,$"Pick {p1}"));
                    break;
                case Opcode.Dup:
                    p1 = PopStack();
                    PushStack(p1);
                    PushStack(p1);
                    break;
                case Opcode.Swap:
                    p1 = PopStack();
                    p2 = PopStack();
                    PushStack(p1);
                    PushStack(p2);
                    break;
                case Opcode.ClearStack:
                    p1 = ReadCodeItem(OperandType.Int32);
                    for (var i =0 ; i < p1; ++i)
                        PushStack(0);
                    break;

                // mem
                case Opcode.Load:
                    p1 = ReadCodeItem(OperandType.Int32);
                    if (opType == OperandType.Global)
                        PushStack(ReadRam(p1,"Load"));
                    else if (opType == OperandType.Local)
                        PushStack(ReadRam(p1 + BasePointer, "Load"));
                    else
                        throw new InternalFailure($"Write optype {opType} unsupported");
                    break;
                case Opcode.Read:
                    p1 = PopStack(); // address
                    if (opType == OperandType.Global)
                        PushStack(ReadRam(p1, "Read"));
                    else if (opType == OperandType.Local)
                        PushStack(ReadRam(p1 + BasePointer, "Read"));
                    else
                        throw new InternalFailure($"Read optype {opType} unsupported");
                    break;
                case Opcode.Write:
                    p1 = PopStack();
                    p2 = PopStack();
                    // todo - store byte if byte sized
                    if (opType == OperandType.Float32 || opType == OperandType.Int32)
                        WriteRam(p1, p2, "Write");
                    else throw new InternalFailure($"Write optype {opType} unsupported");
                    break;
                case Opcode.Addr:
                    p1 = ReadCodeItem(OperandType.Int32);
                    if (opType == OperandType.Local)
                        PushStack(BasePointer + p1);
                    else if (opType == OperandType.Global)
                        PushStack(p1);
                    else
                        throw new InternalFailure($"Illegal operand type {opType} in Addr");
                    break;
                case Opcode.MakeArr:
                {
                    var p = ReadCodeItem(OperandType.Int32); // address
                    if (opType == OperandType.Local)
                        p += BasePointer;
                    else if (opType != OperandType.Global)
                        throw new InternalFailure($"MakeArr optype {opType} unsupported");
                    var n = ReadCodeItem(OperandType.Int32); // dimension, dims are x1,x2,...,xn
                    var si = ReadCodeItem(OperandType.Int32);
                    var m = 1;
                    for (var i = 0; i < n; ++i)
                    {
                        var xi = ReadCodeItem(OperandType.Int32);
                        if (xi == 0 || ((si - ArrayHeaderSize)%xi) != 0)
                            throw new InternalFailure($"Array sizes invalid, not divisible {si}/{xi}");
                        var si2 = si;
                        si = (si - ArrayHeaderSize)/xi;
                        for (var j = 0; j < m; ++j)
                        {
                            WriteRam(p + j*si2 - 1, xi, $"MakeArr dim {xi} position {i + 1} out of bounds");
                            WriteRam(p + j*si2 - 2, si, $"MakeArr stride {si} position {i + 1} out of bounds");

                        }
                        p += ArrayHeaderSize;
                        m *= xi;
                    }
                    break;
                }
                case Opcode.Array:
                {
                    var addr = PopStack(); // current array address
                    var k = ReadCodeItem(OperandType.Int32); // number of levels to dereference
                    var n = ReadCodeItem(OperandType.Int32); // total dimension of array
                    if (k < 1 || n < k)
                        throw new InternalFailure($"Array called with non-positive number of indices {k} or too small dimensionality {n}");
                    for (var i = 0; i < k; ++i)
                    {
                        var bi = PopStack(); // index entry
                        var maxSize = ReadRam(addr - 1, "Error accessing array size");
                        if (bi < 0 || maxSize <= bi)
                            throw new InternalFailure(
                                $"Array out of bounds {bi}, max {maxSize}, address {ProgramCounter}");
                        var nextSize = ReadRam(addr - 2, "Error accessing array stride");
                        addr += bi*nextSize;
                        if (i != n - 1) // add this, except for last possible frame
                            addr += ArrayHeaderSize;
                    }
                    PushStack(addr);
                    break;
                }
                // label/branch/call/ret
                case Opcode.Call:
                    p1 = ProgramCounter + ReadCodeItem(OperandType.Int32); // new program counter 
                    PushStack(ProgramCounter); // return to here
                    PushStack(BasePointer); // save this
                    ProgramCounter = p1; // jump to here
                    BasePointer = StackPointer; // base pointer now points here
                    break;
                case Opcode.Return:
                    p1 = ReadCodeItem(OperandType.Int32); // number of parameters on stack
                    p2 = ReadCodeItem(OperandType.Int32); // number of locals on stack
                    var returnEntryCount = StackPointer - (BasePointer + p2);
                    var srcStackIndex = StackPointer - returnEntryCount;
                    var dstStackIndex = BasePointer - 2 - p1 - returnEntryCount;
                    CopyEntries(srcStackIndex, dstStackIndex, returnEntryCount);
                    StackPointer = BasePointer;
                    BasePointer = PopStack();
                    var retAddress = PopStack();
                    StackPointer -= p1; // pop this many parameter entries
                    ProgramCounter = retAddress;
                    if (ProgramCounter == returnExitAddress)
                        retval = false; // done executing entry function
                    break;
                case Opcode.BrFalse:
                    p1 = ReadCodeItem(OperandType.Int32);
                    if (PopStack() == 0)
                        ProgramCounter += p1;
                    break;
                case Opcode.BrAlways:
                    ProgramCounter += ReadCodeItem(OperandType.Int32);
                    break;


                case Opcode.ForStart:
                {
                    var startIndex = PopStack(); // start
                    var endIndex = PopStack(); // end
                    var deltaIndex = PopStack(); // delta
                    if (deltaIndex== 0) deltaIndex= (startIndex< endIndex) ? 1 : -1;
                    var forAddr = BasePointer + ReadCodeItem(OperandType.Int32); // address to for stack, local to base pointer
                    WriteRam(forAddr, startIndex, "FOR start"); // start address
                    WriteRam(forAddr + 1, deltaIndex, "FOR delta"); // delta
                }
                    break;
                case Opcode.ForLoop:
                {
                    // [   ] update for stack frame, branch if more to do
                    //       Stack has end value, code has local offset to for frame (counter, delta), then delta address to jump on loop
                    //       pops end value after comparison
                    var forAddr = BasePointer + ReadCodeItem(OperandType.Int32); // address to for stack, local to base pointer
                    var index = ReadRam(forAddr, "For loop index"); // index
                    var delta = ReadRam(forAddr+1, "For loop delta"); // delta
                    var endVal = PopStack(); // end value
                    var jumpAddr = ReadCodeItem(OperandType.Int32); // jump delta

                        var done = true;
                    if (delta < 0)
                    {
                        // decreasing
                        done = index + delta >= endVal;
                    }
                    else
                    {
                        // increasing
                        done = index + delta <= endVal;
                    }

                    if (done)
                    {
                        WriteRam(forAddr, index+delta, "For loop update"); // write index back
                        ProgramCounter += jumpAddr;
                    }
                }
                    break;

                //bitwise
                case Opcode.Or:
                    PushStack(PopStack() | PopStack());
                    break;
                case Opcode.And:
                    PushStack(PopStack() & PopStack());
                    break;
                case Opcode.Xor:
                    PushStack(PopStack()%PopStack());
                    break;
                case Opcode.Not:
                    PushStack(~PopStack());
                    break;
                case Opcode.RightShift:
                    p1 = PopStack();
                    p2 = PopStack();
                    PushStack(p1 >> p2);
                    break;
                case Opcode.LeftShift:
                    p1 = PopStack();
                    p2 = PopStack();
                    PushStack(p1 << p2);
                    break;
                case Opcode.RightRotate:
                case Opcode.LeftRotate:
                    throw new NotImplementedException($"Opcode {opcode} not implemented");
                    break;

                // comparison
                case Opcode.NotEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 != p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f1 != f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.IsEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 == p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f1 == f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.GreaterThan:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 > p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f1 > f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.GreaterThanOrEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 >= p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f1 >= f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.LessThanOrEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 <= p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f1 <= f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.LessThan:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 < p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f1 < f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;

                //arithmetic
                case Opcode.Neg:
                    if (opType == OperandType.Int32)
                        PushStack(-PopStack());
                    else if (opType == OperandType.Float32)
                        PushStackF(-PopStackF());
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Add:
                    if (opType == OperandType.Int32)
                        PushStack(PopStack() + PopStack());
                    else if (opType == OperandType.Float32)
                        PushStackF(PopStackF() + PopStackF());
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Sub:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p1 - p2);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStackF(f1 - f2);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Mul:
                    if (opType == OperandType.Int32)
                        PushStack(PopStack()*PopStack());
                    else if (opType == OperandType.Float32)
                        PushStackF(PopStackF()*PopStackF());
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Div:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        if (p2 == 0) throw new InternalFailure("Division by 0");
                        PushStack(p1/p2);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        if (f2 == 0) throw new InternalFailure("Division by 0");
                        PushStackF(f1/f2);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Mod:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        if (p2 == 0) throw new InternalFailure("Division by 0");
                        PushStack(p1%p2);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                default:
                    throw new InternalFailure($"Unknown opcode {opcode}");
            }
            Trace(System.Environment.NewLine);
            return retval;
        }

        #endregion

        #region Support

        // tracing messages sent here
        void Trace(string message)
        {
            if (useTracing)
                env.Output.Write(message);
        }

        // Read 0 terminated UTF8 string
        string ReadImageString(int offset)
        {
            var sb = new StringBuilder();
            int b;
            do
            {
                b = ReadRom(offset++, "ReadImageString");
                if (b != 0)
                    sb.Append((char) b);
            } while (b != 0 && !error);
            return sb.ToString();
        }

        // read big endian bytes at given offset
        int ReadImageInt4(int offset, int count)
        {
            int value = 0, address = offset;
            while (count-- > 0 && !error)
                value = (value << 8) + ReadRom(address++, "ReadImage4 out of bounds");
            return value;
        }

        int ReadCodeItem(OperandType opType)
        {
            int value = 0;
            if (opType == OperandType.Byte)
            {
                value = ReadImageInt4(ProgramCounter + CodeStartOffset, 1);
                ProgramCounter += 1;
            }
            else if (opType == OperandType.Float32 || opType == OperandType.Int32)
            {
                // note we can read a 32 bit float with an int, and pass it back as one
                value = ReadImageInt4(ProgramCounter + CodeStartOffset, 4);
                ProgramCounter += 4;
            }
            else
                throw new InternalFailure("Unknown operand type in Runtime");
            Trace($" c:{value}");
            return value;
        }



        #region Stack

        void PushStack(int value)
        {
            WriteRam(StackPointer++,value,"Stack overflow");
        }

        int PopStack()
        {
            StackPointer--;
            return ReadRam(StackPointer,"Stack underflow");
        }

        void PushStackF(float value)
        {
            // copy float bits into int and push onto stack
            PushStack(BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }

        float PopStackF()
        {
            var i = PopStack();
            // copy int bits into float 
            return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
        }

        #endregion

        void CopyEntries(int srcStackIndex, int dstStackIndex, int returnEntryCount)
        {
            while (returnEntryCount-- > 0 && !error)
                WriteRam(dstStackIndex++, ReadRam(srcStackIndex++, ""), "Copy failed");
        }

        void DumpStack(int stackTop, int count)
        {
            var oldTracing = useTracing;
            env.Info("Stack top dump");
            for (var i = stackTop - count; i < stackTop; ++i)
            {
                if (i < 0 || ramImage1.Length < i)
                    continue;
                useTracing = false; // ignore for read
                var val = ReadRam(i, "");
                useTracing = oldTracing;
                env.Info($"{i}: {val}");
            }
        }


        #endregion
    }
}
