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
         * 
         */

        Environment env;

        public static int GenVersion = 1; // major.minor nibbles of code

        public Runtime(Environment environment)
        {
            env = environment;
        }

        // abstraction to make protection and proof of security easier
        Model model;

        public bool Run(byte [] image, string entryAttribute)
        {
            try
            {
                model = new Model(image);

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

                model.ImageLength = model.ReadImageInt4(4, 4); // assembly length
                model.CodeStartOffset = model.ReadImageInt4(8, 4); // code offset
                model.LinkCount = model.ReadImageInt4(12, 4); // number of link entries
                env.Info($"Length {model.ImageLength}, offset {model.CodeStartOffset}, link count {model.LinkCount}");

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
                env.Error("Exception: "+ex);
                return false;
            }
        }

        // find the attribute with the given name
        // return the associated address
        // return -1 if cannot be found
        int FindAttributeAddress(string attributeName)
        {
            var linkCount = model.ReadImageInt4(12, 4); // number of link entries
            var index = 16; // start here
            for (var i = 0; i < linkCount; ++i)
            {
                // LinkEntry is:
                // 2 byte length
                // 4 byte address of item from start of bytecode
                // 0 terminated UTF-8 strings
                var len  = model.ReadImageInt4(index, 2);
                var addr = model.ReadImageInt4(index+2, 4);
                var txt  = model.ReadImageString(index+6);
                if (txt == attributeName)
                    return addr;
                index += len;
            }
            return -1;
        }



        // run the code, with the code at the given code offset in the assembly
        // and the entry point address into that
        bool Process(int startAddress)
        {
            env.Info($"Processing code, offset {model.CodeStartOffset}, entry address {startAddress}");

            model.SetStartAddress(startAddress);

            while (true)
                Execute();
            return true;
        }

        // simple protected execution model
        class Model
        {

            public Model(byte[] image)
            {
                this.romImage = image;
            }

            byte[] romImage;
            int[] ramImage = new int[10000];


            #region Rom access
            // Read 0 terminated UTF8 string
            public string ReadImageString(int offset)
            {
                var sb = new StringBuilder();
                int b;
                do
                {
                    if (!ReadChecked(romImage, ref offset, 1, out b))
                        throw new InternalFailure("Out of bounds");
                    if (b != 0)
                        sb.Append((char) b);
                } while (b != 0);
                return sb.ToString();
            }

            // read bytes at given offset
            public int ReadImageInt4(int offset, int count)
            {

                int value, address = offset;
                if (ReadChecked(romImage, ref address, 4, out value))
                    return value;
                return 0;
            }

            #region Code access
            /// <summary>
            /// Read code byte, increment program counter
            /// </summary>
            /// <returns></returns>
            public byte ReadCodeByte()
            {
                int value, address = ProgramCounter;
                if (!ReadChecked(romImage, ref address, 1, out value))
                    return 0; // failed
                ProgramCounter = address;
                return (byte)value;
            }
            public int ReadCodeItem(OperandType opType)
            {
                int value = 0;
                if (opType == OperandType.Byte)
                {
                    value = ReadImageInt4(ProgramCounter, 1);
                    ProgramCounter += 1;
                }
                else if (opType == OperandType.Float32 || opType == OperandType.Int32)
                { // note we can read a 32 bit float with an int, and pass it back as one
                    value = ReadImageInt4(ProgramCounter, 4);
                    ProgramCounter += 4;
                }
                else
                    throw new InternalFailure("Unknown operand type in Runtime");
                return value;
            }
            #endregion

            #endregion

            /// <summary>
            /// Read integer from data, starting at address, read given length, return in value
            /// Return true on success, else false
            /// </summary>
            /// <param name="data"></param>
            /// <param name="address"></param>
            /// <param name="length"></param>
            /// <param name="value"></param>
            /// <returns></returns>
            static bool ReadChecked(byte [] data, ref int address, int length, out int value)
            {
                value = 0;
                while (length-- > 0)
                {
                    if (address < 0 || data.Length <= address)
                        return false;
                    value = (value << 8) + data[address];
                    ++address;
                }
                return true;
            }

            /// <summary>
            /// Write integer to data, starting at address, of given length
            /// Return true on success, else false
            /// </summary>
            /// <param name="data"></param>
            /// <param name="address"></param>
            /// <param name="value"></param>
            /// <returns></returns>
            static bool WriteChecked(int[] data, int address, int value)
            {
                if (address < 0 || data.Length <= address)
                    return false;
                data[address] = value;
                return true;
            }


            public int StackPointer { get; private set; }
            public int BasePointer { get; set; }
            public int ProgramCounter { get; private set; }

            public int ImageLength;
            public int LinkCount;
            public int CodeStartOffset;

            public void SetStartAddress(int startAddress)
            {
                ProgramCounter = /*startAddress + */CodeStartOffset;
            }

            #region Stack
            public void PushStack(int value)
            {
                if (StackPointer >= ramImage.Length)
                    throw new InternalFailure("Runtime stack overflow");
                ramImage[StackPointer++] = value;
            }

            public int PopStack()
            {
                if (StackPointer <= 0)
                    throw new InternalFailure("Runtime stack underflow");
                return ramImage[--StackPointer];
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
                    return ramImage[BasePointer+i];
                else
                    return ramImage[i];
            }


            /// <summary>
            /// Add offset to requested register. 
            /// </summary>
            /// <param name="offset"></param>
            /// <param name="register"></param>
            public void AddRegister(int offset, RegisterName register)
            {
                switch (register)
                {
                    case RegisterName.BasePointer:
                        BasePointer += offset;
                        if (BasePointer < 0 || ramImage.Length <= BasePointer)
                            throw new InternalFailure($"Base pointer {BasePointer} out of bounds");
                        break;
                    case RegisterName.ProgramCounter:
                        ProgramCounter += offset;
                        if (ProgramCounter < CodeStartOffset || romImage.Length <= ProgramCounter)
                            throw new InternalFailure($"Program counter {ProgramCounter} out of bounds");
                        break;
                    case RegisterName.StackPointer:
                        StackPointer += offset;
                        if (StackPointer < 0 || ramImage.Length <= StackPointer)
                            throw new InternalFailure($"Stack pointer {StackPointer} out of bounds");
                        break;
                    default:
                        throw new InternalFailure($"Register {register} not handled in AddRegister");
                }
            }

            public enum RegisterName
            {
                StackPointer,
                ProgramCounter,
                BasePointer
            }

            public void WriteMemory(int address, int value)
            {
                if (!WriteChecked(ramImage,address, value))
                    throw new InternalFailure("Memory write exception");
            }

        }

        // execute the instruction at the current program counter
        void Execute()
        {
            int p1, p2;
            float f1, f2;

            // read instruction
            Opcode opcode = (Opcode)model.ReadCodeByte();
            OperandType opType = (OperandType)model.ReadCodeByte();

            // trace address, instruction here
            env.Info($"{model.ProgramCounter}: {opcode}");

            // handle parameters
            switch (opcode)
            {

                case Opcode.Nop:
                    break; // do nothing
                
                // stack
                case Opcode.Push:
                    model.PushStack(model.ReadCodeItem(opType));
                    break;
                case Opcode.Pop:
                    model.PopStack();
                    break;
                case Opcode.Pick:
                    p1 = model.ReadCodeItem(OperandType.Int32);
                    model.PushStack(model.ReadMemory(model.StackPointer - p1));
                    break;
                case Opcode.Dup:
                    p1 = model.PopStack();
                    model.PushStack(p1);
                    model.PushStack(p1);
                    break;
                case Opcode.Swap:
                    p1 = model.PopStack();
                    p2 = model.PopStack();
                    model.PushStack(p1);
                    model.PushStack(p2);
                    break;
                case Opcode.AddStack:
                    model.AddRegister(model.ReadCodeItem(OperandType.Int32), Model.RegisterName.StackPointer);
                    break;

                // mem
                case Opcode.Load:
                    p1 = model.ReadCodeItem(OperandType.Int32);
                    model.PushStack(model.ReadMemory(p1, OperandType.Local == opType));
                    break;
                case Opcode.Store:
                    p1 = model.PopStack();
                    p2 = model.PopStack();
                    model.WriteMemory(p1,p2);
                    break;
                case Opcode.Addr:
                    p1 = model.PopStack();
                    if (opType == OperandType.Local)
                        model.PushStack(model.BasePointer + p1 );
                    else if (opType == OperandType.Global)
                        model.PushStack(p1);
                    else
                        throw new InternalFailure("Illegal operand type in Addr");
                    break;

                // label/branch/call/ret
                case Opcode.Call:
                    model.PushStack(model.ProgramCounter);  // return to here
                    model.PushStack(model.BasePointer);     // save this
                    model.BasePointer = model.StackPointer; // base pointer now points here
                    p1 = model.ReadCodeItem(OperandType.Int32);
                    model.AddRegister(p1, Model.RegisterName.ProgramCounter); // jump to here
                    break;
                case Opcode.Return:
                    throw new NotImplementedException($"Opcode {opcode} not implemented");
                    break;
                case Opcode.BrFalse:
                    p1 = model.ReadCodeItem(OperandType.Int32);
                    p2 = model.PopStack();
                    if (p2 == 0)
                        model.AddRegister(p1, Model.RegisterName.ProgramCounter);
                    break;
                case Opcode.BrAlways:
                    p1 = model.ReadCodeItem(OperandType.Int32);
                    model.AddRegister(p1, Model.RegisterName.ProgramCounter);
                    break;
                case Opcode.ForStart:
                case Opcode.ForLoop:
                    throw new NotImplementedException($"Opcode {opcode} not implemented");
                    break;

                //bitwise
                case Opcode.Or:
                    model.PushStack(model.PopStack() | model.PopStack());
                    break;
                case Opcode.And:
                    model.PushStack(model.PopStack() & model.PopStack());
                    break;
                case Opcode.Xor:
                    model.PushStack(model.PopStack() % model.PopStack());
                    break;
                case Opcode.Not:
                    model.PushStack(~model.PopStack());
                    break;
                case Opcode.RightShift:
                    p1 = model.PopStack();
                    p2 = model.PopStack();
                    model.PushStack(p1 >> p2);
                    break;
                case Opcode.LeftShift:
                    p1 = model.PopStack();
                    p2 = model.PopStack();
                    model.PushStack(p1 << p2);
                    break;
                case Opcode.RightRotate:
                case Opcode.LeftRotate:
                    throw new NotImplementedException($"Opcode {opcode} not implemented");
                    break;

                // comparison
                case Opcode.NotEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1 != p2 ? 1:0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStack(f1 != f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.IsEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1 == p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStack(f1 == f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.GreaterThan:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1 > p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStack(f1 > f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.GreaterThanOrEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1 >= p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStack(f1 >= f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.LessThanOrEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1 <= p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStack(f1 <= f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.LessThan:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1 < p2 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStack(f1 < f2 ? 1 : 0);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;

                //arithmetic
                case Opcode.Neg:
                    if (opType == OperandType.Int32)
                        model.PushStack(-model.PopStack());
                    else if (opType == OperandType.Float32)
                        model.PushStackF(-model.PopStackF());
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Add:
                    if (opType == OperandType.Int32)
                        model.PushStack(model.PopStack()+ model.PopStack());
                    else if (opType == OperandType.Float32)
                        model.PushStackF(model.PopStackF() + model.PopStackF());
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Sub:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        model.PushStack(p1-p2);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        model.PushStackF(f1 - f2);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Mul:
                    if (opType == OperandType.Int32)
                        model.PushStack(model.PopStack() * model.PopStack());
                    else if (opType == OperandType.Float32)
                        model.PushStackF(model.PopStackF() * model.PopStackF());
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Div:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        if (p2 == 0) throw new InternalFailure("Division by 0");
                        model.PushStack(p1 / p2);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = model.PopStackF();
                        f2 = model.PopStackF();
                        if (f2 == 0) throw new InternalFailure("Division by 0");
                        model.PushStackF(f1 / f2);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Mod:
                    if (opType == OperandType.Int32)
                    {
                        p1 = model.PopStack();
                        p2 = model.PopStack();
                        if (p2 == 0) throw new InternalFailure("Division by 0");
                        model.PushStack(p1 % p2);
                    }
                    else throw new InternalFailure($"Unknown op type {opType} in {opcode}");
                    break;
                default:
                    throw new InternalFailure($"Unknown opcode {opcode}");
            }
        }

    }
}
