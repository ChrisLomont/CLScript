using System;
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
         *        data is 4 byte: "CLS " "CLS" ASCII
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
         *        Chunk "img0" : global vars for the assembly (todo)
         *            one byte 0 (no chunk padding) or 1 (chunk padding)
         *            initialization image (copied into RAM, stack pointer points past it to start
         *            
         *        Chunk "text" : string table (todo)
         * 
         * todo - types?, RAM/ROM distinction, item type (var or func)
         * 
         */

        public Runtime(Environment environment)
        {
            env = environment;
        }

        /// <summary>
        /// Run an image, starting at the given attributed entry point
        /// with the given parameters, and given space for return values
        /// </summary>
        /// <param name="image"></param>
        /// <param name="entryAttribute"></param>
        /// <param name="parameters"></param>
        /// <param name="returnValues"></param>
        /// <returns></returns>
        public bool Run(byte[] image, string entryAttribute, int [] parameters, int [] returnValues)
        {
            useTracing = true;

            error = false;
            romImage1 = image;

            try
            {
                var index = 0;
                string name;
                int length;
                if (!ReadChunkHeader(ref index, out name, out length) || name != "RIFF" || length != image.Length-8 ||
                    !ReadImageString(ref index, out name, 4) || name != "CLS " || ReadImageInt(ref index, 2) != GenVersion || error)
                {
                    env.Error($"Invalid bytecode header");
                    return false;
                }

                // walk chunks
                while (index < image.Length)
                {
                    if (!ReadChunkHeader(ref index, out name, out length))
                    {
                        env.Error($"Invalid bytecode chunk {name}");
                        return false;
                    }
                    if (name == "code")
                    {
                        CodeStartOffset = (int)index;
                        index += length;
                    }
                    else if (name == "link")
                    {
                        LinkStartOffset = (int) index;
                        index += length;
                    }
                    else
                    {
                        env.Warning($"Unknown bytecode chunk {name}");
                        index += length;
                    }
                }


                env.Info($"Code offset {CodeStartOffset}, link offset {LinkStartOffset}");

                // find entry point
                int address, retCount, paramCount, uniqueId;

                if (!FindAttributeAddress(entryAttribute, out address, out retCount, out paramCount, out uniqueId))
                {
                    env.Error($"Cannot find entry point attribute {entryAttribute}. Ensure exported.");
                    return false;
                }

                if (returnValues.Length != retCount)
                {
                    env.Error($"Entry function has wrong number of return values");
                    return false;
                }
                if (parameters.Length != paramCount)
                {
                    env.Error($"Entry function has wrong number of parameters");
                    return false;
                }

                return Process(address, parameters, returnValues);
            }
            catch (Exception ex)
            {
                env.Error("Exception: " + ex);
                return false;
            }
        }

        bool ReadChunkHeader(ref int index, out string name, out int length)
        {
            length = 0;
            if (!ReadImageString(ref index, out name, 4))
                return false;
            length = ReadImageInt(ref index, 4);
            return !error;
        }

        public static ushort GenVersion   = 1; // major.minor bytes of code
        public static int ArrayHeaderSize = 2; // in stack entries, these are before array address

        // all locals - minimize memory
        #region Locals

        Environment env;

        int StackPointer;
        int BasePointer;
        int ProgramCounter;
        int CodeStartOffset;
        int LinkStartOffset;

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

        // find the attribute with the given name
        // return the associated address
        // return false if cannot be found
        bool FindAttributeAddress(string attributeName, out int address, out int retCount, out int paramCount, out int uniqueId)
        {
            address = retCount = paramCount = uniqueId = -1;
            var index = LinkStartOffset;
            var importCount = ReadImageInt(ref index, 4);
            var exportCount = ReadImageInt(ref index, 4);
            if (error) return false;

            for (var i = 0; i < importCount + exportCount; ++i)
            {
                var offset = ReadImageInt(ref index, 4);
                if (LinkCheckAttribute(offset, attributeName, out address, out retCount, out paramCount, out uniqueId) || error)
                    return !error;
            }
            return false;
        }

        // given offset from LinkStartOffset, if has given attribute, return true, else false
        // get address
        bool LinkCheckAttribute(int offset, string attributeName, out int address, out int retCount, out int paramCount, out int id)
        {
            string name;

            var index      = offset + LinkStartOffset;
            id             = ReadImageInt(ref index, 4);
            address        = ReadImageInt(ref index, 4);
            retCount       = ReadImageInt(ref index, 4);
            paramCount     = ReadImageInt(ref index, 4);
            var attrCount  = ReadImageInt(ref index, 4);

            if (attrCount == 0 || error) return false;

            // entry name
            if (!ReadImageString(ref index, out name))
                return false;

            // parse attributes
            for (var i = 0; i < attrCount; ++i)
            {
                if (!ReadImageString(ref index, out name))
                    return false;
                if (name == attributeName)
                    return true; // found it
                
                // param count, read them
                var attrParamCount = ReadImageInt(ref index, 4);
                for (var j = 0; j < attrParamCount; ++j)
                    if (!ReadImageString(ref index, out name))
                        return false;
            }

            return false;
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


            // return values
            var retCount = returnValues.Length;
            for (var i = 0; i < retCount; ++i)
                returnValues[i] = ReadRam(StackPointer - (retCount - i),"Stack dump");

            return !error;
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
            var inst = ReadCodeItem(OperandType.Byte);

            Opcode opcode = (Opcode) (inst/6); //  ReadCodeItem(OperandType.Byte);
            OperandType opType = (OperandType) (inst%6);//ReadCodeItem(OperandType.Byte);
            useTracing = oldTrace; // restore tracing setting

            // trace address, instruction here
            Trace($"TRACE: {p1:X5}: {opcode,-10} {opType,-6} SP:{StackPointer:X4} BP:{BasePointer:X4}  : ");

            // handle parameters
            switch (opcode)
            {

                // stack
                case Opcode.Push:
                    PushStack(ReadCodeItem(opType,true));
                    break;
                case Opcode.Pop:
                    PopStack();
                    break;
                case Opcode.Pick:
                    p1 = Unpack();
                    PushStack(ReadRam(StackPointer - p1 - 1,$"Pick {p1}"));
                    break;
                case Opcode.Dup:
                    p1 = PopStack();
                    PushStack(p1);
                    PushStack(p1);
                    break;
                case Opcode.ClearStack:
                    p1 = Unpack();
                    for (var i =0 ; i < p1; ++i)
                        PushStack(0);
                    break;
                case Opcode.PopStack:
                    p1 = Unpack();
                    StackPointer -= p1;
                    break;

                // mem
                case Opcode.Load:
                    p1 = Unpack();
                    if (opType == OperandType.Global)
                        PushStack(ReadRam(p1,"Load"));
                    else if (opType == OperandType.Local)
                        PushStack(ReadRam(p1 + BasePointer, "Load"));
                    else
                        throw new RuntimeException($"Write optype {opType} unsupported");
                    break;
                case Opcode.Read:
                    p1 = PopStack(); // address
                    if (opType == OperandType.Global)
                        PushStack(ReadRam(p1, "Read"));
                    else if (opType == OperandType.Local)
                        PushStack(ReadRam(p1 + BasePointer, "Read"));
                    else
                        throw new RuntimeException($"Read optype {opType} unsupported");
                    break;
                case Opcode.Write:
                    p1 = PopStack(); // value
                    p2 = PopStack(); // address
                    // todo - store byte if byte sized
                    if (opType == OperandType.Float32 || opType == OperandType.Int32)
                        WriteRam(p2, p1, "Write");
                    else throw new RuntimeException($"Write optype {opType} unsupported");
                    break;
                case Opcode.Addr:
                    p1 = Unpack();
                    if (opType == OperandType.Local)
                        PushStack(BasePointer + p1);
                    else if (opType == OperandType.Global)
                        PushStack(p1);
                    else
                        throw new RuntimeException($"Illegal operand type {opType} in Addr");
                    break;
                case Opcode.MakeArr:
                {
                    var p = Unpack(); // address
                    if (opType == OperandType.Local)
                        p += BasePointer;
                    else if (opType != OperandType.Global)
                        throw new RuntimeException($"MakeArr optype {opType} unsupported");
                    var n = Unpack(); // dimension, dims are x1,x2,...,xn
                    var si = Unpack();
                    var m = 1;
                    for (var i = 0; i < n; ++i)
                    {
                        var xi = Unpack();
                        if (xi == 0 || ((si - ArrayHeaderSize)%xi) != 0)
                            throw new RuntimeException($"Array sizes invalid, not divisible {si}/{xi}");
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
                    var k = Unpack(); // number of levels to dereference
                    var n = Unpack(); // total dimension of array
                    if (k < 1 || n < k)
                        throw new RuntimeException($"Array called with non-positive number of indices {k} or too small dimensionality {n}");
                    for (var i = 0; i < k; ++i)
                    {
                        var bi = PopStack(); // index entry
                        var maxSize = ReadRam(addr - 1, "Error accessing array size");
                        if (bi < 0 || maxSize <= bi)
                            throw new RuntimeException(
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
                    if (opType == OperandType.Local)
                    {
                        p1 = ProgramCounter + ReadCodeItem(OperandType.Int32); // new program counter 
                        PushStack(ProgramCounter); // return to here
                        PushStack(BasePointer); // save this
                        ProgramCounter = p1; // jump to here
                        BasePointer = StackPointer; // base pointer now points here
                    }
                    else if (opType == OperandType.Const)
                    {
                        p1 = ReadCodeItem(OperandType.Int32); // import index
                        PushStack(ProgramCounter); // return to here
                        PushStack(BasePointer); // save this
                        BasePointer = StackPointer; // base pointer now points here

                        var e = HandleImport;
                        if (e == null)
                            throw new RuntimeException("No imports attached to runtime");
                        int returnCount;
                        string name;
                        GetImport(p1, out name, out p2, out returnCount);
                        StartImportCall(p2);
                        if (e(this, p1, name, p2, returnCount))
                            EndImportCall();
                        else
                            throw new RuntimeException("Runtime import call failed");
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Return:
                    p1 = Unpack(); // number of parameters on stack
                    p2 = Unpack(); // number of locals on stack
                    ExecuteReturn(p1,p2);
                    if (ProgramCounter == returnExitAddress)
                        retval = false; // done executing entry function
                    break;
                case Opcode.BrFalse:
                    p1 = ReadCodeItem(OperandType.Int32);
                    if (PopStack() == 0)
                        ProgramCounter += p1-4;
                    break;
                case Opcode.BrAlways:
                    ProgramCounter += ReadCodeItem(OperandType.Int32);
                    break;


                case Opcode.ForStart:
                {
                    var deltaIndex = PopStack(); // delta
                    var endIndex = PopStack();   // end
                    var startIndex = PopStack(); // start
                    if (deltaIndex== 0) deltaIndex= (startIndex< endIndex) ? 1 : -1;
                    var forAddr = BasePointer + Unpack(); // address to for stack, local to base pointer
                    WriteRam(forAddr, startIndex, "FOR start"); // start address
                    WriteRam(forAddr + 1, deltaIndex, "FOR delta"); // delta
                }
                    break;
                case Opcode.ForLoop:
                {
                    // [   ] update for stack frame, branch if more to do
                    //       Stack has end value, code has local offset to for frame (counter, delta), then delta address to jump on loop
                    //       pops end value after comparison
                    var forAddr = BasePointer + Unpack(); // address to for stack, local to base pointer
                    var index = ReadRam(forAddr, "For loop index"); // index
                    var delta = ReadRam(forAddr+1, "For loop delta"); // delta
                    var endVal = PopStack(); // end value
                    var jumpAddr = ReadCodeItem(OperandType.Int32); // jump delta
                    if ((0 < delta && index + delta <= endVal) ||
                        (delta < 0 && index + delta >= endVal))
                    {
                        WriteRam(forAddr, index+delta, "For loop update"); // write index back
                        ProgramCounter += jumpAddr-4; // todo - this -4 fixup should be cleaned at generation
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
                    PushStack(PopStack()^PopStack());
                    break;
                case Opcode.Not:
                    PushStack(~PopStack());
                    break;
                case Opcode.RightShift:
                    p1 = PopStack();
                    p2 = PopStack();
                    PushStack(p2 >> p1);
                    break;
                case Opcode.LeftShift:
                    p1 = PopStack();
                    p2 = PopStack();
                    PushStack(p2 << p1);
                    break;
                case Opcode.RightRotate:
                    p1 = PopStack(); // right
                    p2 = PopStack(); // left
                    PushStack(RotateRight(p2, p1));
                    break;
                case Opcode.LeftRotate:
                    p1 = PopStack(); // right
                    p2 = PopStack(); // left
                    PushStack(RotateRight(p2, -p1));
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
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
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
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.GreaterThan:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p2 > p1 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f2 > f1 ? 1 : 0);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.GreaterThanOrEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p2 >= p1 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f2 >= f1 ? 1 : 0);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.LessThanOrEqual:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p2 <= p1 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f2 <= f1 ? 1 : 0);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.LessThan:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p2 < p1 ? 1 : 0);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStack(f2 < f1 ? 1 : 0);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;

                //arithmetic
                case Opcode.Neg:
                    if (opType == OperandType.Int32)
                        PushStack(-PopStack());
                    else if (opType == OperandType.Float32)
                        PushStackF(-PopStackF());
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Add:
                    if (opType == OperandType.Int32)
                        PushStack(PopStack() + PopStack());
                    else if (opType == OperandType.Float32)
                        PushStackF(PopStackF() + PopStackF());
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Sub:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        PushStack(p2 - p1);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        PushStackF(f2 - f1);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Mul:
                    if (opType == OperandType.Int32)
                        PushStack(PopStack()*PopStack());
                    else if (opType == OperandType.Float32)
                        PushStackF(PopStackF()*PopStackF());
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Div:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        if (p1 == 0) throw new RuntimeException("Division by 0");
                        PushStack(p2/p1);
                    }
                    else if (opType == OperandType.Float32)
                    {
                        f1 = PopStackF();
                        f2 = PopStackF();
                        if (f1 == 0) throw new RuntimeException("Division by 0");
                        PushStackF(f2/f1);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                case Opcode.Mod:
                    if (opType == OperandType.Int32)
                    {
                        p1 = PopStack();
                        p2 = PopStack();
                        if (p1 == 0) throw new RuntimeException("Division by 0");
                        PushStack(p2%p1);
                    }
                    else throw new RuntimeException($"Unknown op type {opType} in {opcode}");
                    break;
                default:
                    throw new RuntimeException($"Unknown opcode {opcode}");
            }
            Trace(System.Environment.NewLine);
            return retval;
        }

        // execute a return statement
        void ExecuteReturn(int parameterCount, int localCount)
        {
            var returnEntryCount = StackPointer - (BasePointer + localCount);
            var srcStackIndex = StackPointer - returnEntryCount;
            var dstStackIndex = BasePointer - 2 - parameterCount - returnEntryCount;
            CopyEntries(srcStackIndex, dstStackIndex, returnEntryCount);
            StackPointer = BasePointer;
            BasePointer = PopStack();
            var retAddress = PopStack();
            StackPointer -= parameterCount; // pop this many parameter entries
            ProgramCounter = retAddress;
        }

        /// <summary>
        /// Rotate the value right through (rotation) bits, where
        /// rotation can be any integer. Negative rotates is essentially a left shift
        /// </summary>
        /// <param name="value"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        int RotateRight(int value, int rotation)
        {   // get rotation in 0-31
            if (rotation >= 0)
                rotation &= 31;
            else
                rotation = (32 - ((-rotation) & 31)) & 31;
            if (rotation == 0) return value;
            uint t = (uint) value;
            return (int) ((t >> rotation) | (t << (32 - rotation)));
        }

        #endregion

        #region Import

        // call to handle imports, return true on success.
        public delegate bool ImportHandler(
            Runtime runtime, int importIndex, string name, int parameterCount, int returnCount);
        public ImportHandler HandleImport { get; set; }


        public void GetImport(int importIndex, out string name, out int parameterCount, out int returnCount)
        {
            var index = importIndex * 4 + LinkStartOffset + 8;
            var itemStart = ReadImageInt(ref index, 4);
            index = itemStart + LinkStartOffset+4;

            var address = ReadImageInt(ref index, 4);
            var retCount = ReadImageInt(ref index, 4);
            var paramCount = ReadImageInt(ref index, 4);
            var attrCount = ReadImageInt(ref index, 4);

            // entry name
            if (!ReadImageString(ref index, out name))
                throw new RuntimeException($"Cannot find import call {importIndex}");
            parameterCount = paramCount;
            returnCount = retCount;
        }

        int importParameterCount = -1, importParameterIndex = -1;

        // call right before calling external functions, resets counters, etc.
        void StartImportCall(int parameterCount)
        {
            importParameterCount = parameterCount;
            importParameterIndex = 0;
        }

        void EndImportCall()
        {
            ExecuteReturn(importParameterCount, 0);
            importParameterCount = -1;
        }

        public Int32 GetInt32Parameter()
        {
            var index = BasePointer - 1 - 1 - importParameterCount + importParameterIndex;
            importParameterIndex++;
            return ReadRam(index, "Failed to read import Int32");
        }

        public void SetInt32Return(Int32 value)
        {
            PushStack(value);
        }

        public float GetFloat32Parameter()
        {
            return Int32ToFloat32(GetInt32Parameter());
        }

        public void SetFloat32Return(float value)
        {
            PushStackF(value);
        }

        #endregion

        #region Support

        // tracing messages sent here
        void Trace(string message)
        {
            if (useTracing)
                env.Output.Write(message);
        }

        // Read a UTF8 string from the rom image
        // if numBytes > 0, get that many bytes, else it's 0 terminated
        // return true on success
        bool ReadImageString(ref int index, out string name, int numBytes = -1)
        {
            var sb = new StringBuilder();
            int b;
            while (true)
            {
                b = ReadRom(index++, "ReadImageString");
                if (error)
                    break;
                if (numBytes <= 0 && b == 0)
                    break;
                sb.Append((char) b);
                if (numBytes > 0 && sb.Length >= numBytes)
                    break;
            }
            name = sb.ToString();
            return !error;
        }

        // read big endian bytes at given offset of given length
        // sets error flag on failure
        int ReadImageInt(ref int index, int count)
        {
            int value = 0, address = index;
            while (count-- > 0 && !error)
                value = (value << 8) + ReadRom(address++, "ReadImageInt out of bounds");
            index = address;
            return value;
        }

        // read big endian bytes at given offset
        int ReadImageInt(int offset, int count)
        {
            return ReadImageInt(ref offset, count);
        }

        int Unpack()
        {
#if false
            return ReadCodeItem(OperandType.Int32);
#else
            // simple encoding: -127 to 127 stored as byte in 0-254, else 255 stored then 4 byte int
            var v = ReadCodeItem(OperandType.Byte);
            if (v != 255)
                return v - 127;
            return ReadCodeItem(OperandType.Int32);
#endif
        }

        int ReadCodeItem(OperandType opType, bool packed = false)
        {
            int value = 0;
            if (opType == OperandType.Byte)
            {
                value = ReadImageInt(ProgramCounter + CodeStartOffset, 1);
                ProgramCounter += 1;
            }
            else if (opType == OperandType.Int32 && packed)
                value = Unpack();
            else if (opType == OperandType.Float32 || opType == OperandType.Int32)
            {
                // note we can read a 32 bit float with an int, and pass it back as one
                value = ReadImageInt(ProgramCounter + CodeStartOffset, 4);
                ProgramCounter += 4;
            }
            else
                throw new RuntimeException("Unknown operand type in Runtime");
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
            // convert float bits into int bits and push onto stack
            PushStack(Float32ToInt32(value));
        }

        float PopStackF()
        {
            return Int32ToFloat32(PopStack());
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
                var f = Int32ToFloat32(val);
                env.Info($"{i}: {val}  ({f})");
            }
        }

        // convert a 32 bit float to equivalent 32 bit int (same bit pattern)
        public static Int32 Float32ToInt32(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }

        // convert a 32 bit int to equivalent 32 bit float (same bit pattern)
        public static float Int32ToFloat32(int value)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        }


#endregion

    }
}
