using System;
using System.ComponentModel.DataAnnotations;
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
        public bool Run(byte[] image, string entryAttribute)
        {
            return RunImage(image, entryAttribute);
        }


        public static int GenVersion = 1; // major.minor nibbles of code

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

        bool RunImage(byte[] image, string entryAttribute)
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

                return Process(entryPoint);
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
        bool Process(int startAddress)
        {
            env.Info($"Processing code, offset {CodeStartOffset}, entry address {startAddress}");

            ProgramCounter = startAddress;
            BasePointer = -1; // out of bounds
            StackPointer = 0; // todo - start past globals, load them as block

            // todo - get return size to start with
            int returnEntries = 2, parameters = 4;
            // create call stack
            // 1. Push space for return values
            for (var i = 0; i < returnEntries; ++i)
                PushStack(0);
            // 2. push parameters
            for (var i = 0; i < parameters; ++i)
                PushStack(i+1);
            // 3. push ret code (special code to exit), and base pointer, set bp
            PushStack(returnExitAddress);
            PushStack(BasePointer);
            BasePointer = StackPointer; // frame start

            // now entry looks like a Call instruction to the code

            while (!error)
            {
                if (!Execute())
                    break;
            }

            DumpStack(StackPointer,10);

            return error;
        }


        // when a return jumps here, execution is done
        const int returnExitAddress = -1; 

        // execute the instruction at the current program counter
        // return true if not ending
        bool Execute()
        {
            int p1, p2;
            float f1, f2;

            // read instruction
            Opcode opcode = (Opcode) ReadCodeItem(OperandType.Byte);
            OperandType opType = (OperandType)ReadCodeItem(OperandType.Byte);

            // trace address, instruction here
            env.Info($"{ProgramCounter}: {opcode}");

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
                    PushStack(ReadMemory(StackPointer - p1));
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
                case Opcode.AddStack:
                    StackPointer += ReadCodeItem(OperandType.Int32);
                    break;

                // mem
                case Opcode.Load:
                    p1 = ReadCodeItem(OperandType.Int32);
                    PushStack(ReadMemory(p1, OperandType.Local == opType));
                    break;
                case Opcode.Store:
                    p1 = PopStack();
                    p2 = PopStack();
                    if (opType == OperandType.Global)
                        WriteRam(p1, p2, "Store opcode");
                    else if (opType == OperandType.Local)
                        WriteRam(p1+BasePointer,p2,"Store opcode");
                    else throw new InternalFailure("Store optype {opType} unsupported");
                    break;
                case Opcode.Addr:
                    p1 = PopStack();
                    if (opType == OperandType.Local)
                        PushStack(BasePointer + p1);
                    else if (opType == OperandType.Global)
                        PushStack(p1);
                    else
                        throw new InternalFailure("Illegal operand type in Addr");
                    break;

                // label/branch/call/ret
                case Opcode.Call:
                    ProgramCounter += ReadCodeItem(OperandType.Int32); // jump to here
                    PushStack(ProgramCounter); // return to here
                    PushStack(BasePointer); // save this
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
                    StackPointer -= p1; // pop this many
                    ProgramCounter = retAddress;
                    if (ProgramCounter == returnExitAddress)
                        return false; // done executing entry function
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
                case Opcode.ForLoop:
                    throw new NotImplementedException($"Opcode {opcode} not implemented");
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
            return true;
        }

        #endregion

        #region Support

        // Read 0 terminated UTF8 string
        public string ReadImageString(int offset)
        {
            var sb = new StringBuilder();
            int b;
            do
            {
                b = ReadRom(offset++, "ReadImagString");
                if (b != 0)
                    sb.Append((char) b);
            } while (b != 0 && !error);
            return sb.ToString();
        }

        // read big endian bytes at given offset
        public int ReadImageInt4(int offset, int count)
        {
            int value = 0, address = offset;
            while (count-- > 0 && !error)
                value = (value << 8) + ReadRom(address++, "ReadImage4 out of bounds");
            return value;
        }

        public int ReadCodeItem(OperandType opType)
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
            return value;
        }



        #region Stack

        void PushStack(int value)
        {
            WriteRam(StackPointer++,value,"Stack overflow");
        }

        public int PopStack()
        {
            StackPointer--;
            return ReadRam(StackPointer,"Stack underflow");
        }

        public void PushStackF(float value)
        {
            // copy float bits into int and push onto stack
            PushStack(BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }

        public float PopStackF()
        {
            var i = PopStack();
            // copy int bits into float 
            return BitConverter.ToSingle(BitConverter.GetBytes(i), 0);
        }

        #endregion


        /// <summary>
        /// Read 32 bit value at address
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public int ReadMemory(int i, bool local = false)
        {
            if (local)
                return ReadRam(BasePointer + i, "");
            else
                return ReadRam(i, "");
        }

        public enum RegisterName
        {
            StackPointer,
            ProgramCounter,
            BasePointer
        }

        public void CopyEntries(int srcStackIndex, int dstStackIndex, int returnEntryCount)
        {
            while (returnEntryCount-- > 0 && !error)
                WriteRam(dstStackIndex++, ReadRam(srcStackIndex++, ""), "Copy failed");
        }

        void DumpStack(int stackTop, int count)
        {
            env.Info("Stack top dump");
            for (var i = stackTop - count; i < stackTop; ++i)
            {
                if (i < 0 || ramImage1.Length < i)
                    continue;
                var val = ReadRam(i, "");
                env.Info($"{i}: {val}");
            }
        }


        #endregion
    }
}
