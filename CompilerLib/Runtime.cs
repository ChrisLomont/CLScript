using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Lomont.ClScript.CompilerLib
{
    // run a compiled blob :)
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

        byte[] bcode;
        public bool Run(byte [] bytecode, string entryAttribute)
        {
            bcode = bytecode;

            // min size
            if (bytecode.Length < 12)
            {
                env.Error("bytecode too short to be valid");
                return false;
            }
            // read header
            if (bytecode[0] != 'C' || bytecode[1] != 'L' || bytecode[2] != 'S' || bytecode[3] != GenVersion)
            {
                env.Error($"Invalid bytecode header");
                return false;
            }

            var length = ReadI(4,4);      // assembly length
            var offset = ReadI(8, 4);     // code offset
            var linkCount = ReadI(12, 4); // number of link entries
            env.Info($"Length {length}, offset {offset}, link count {linkCount}");

            // find entry point
            var entryPoint = FindAttributeAddress(entryAttribute);
            if (entryPoint < 0)
            {
                env.Error($"Cannot find entry point attribute {entryAttribute}");
                return false;
            }
            
            return Process(offset,entryPoint);
        }

        // find the attribute with the given name
        // return the associated address
        // return -1 if cannot be found
        int FindAttributeAddress(string attributeName)
        {
            var linkCount = ReadI(12, 4); // number of link entries
            var index = 16; // start here
            for (var i = 0; i < linkCount; ++i)
            {
                // LinkEntry is:
                // 2 byte length
                // 4 byte address of item from start of bytecode
                // 0 terminated UTF-8 strings
                var len = ReadI(index, 2);
                var addr = ReadI(index+2, 4);
                var txt = ReadS(index+6);
                if (txt == attributeName)
                    return addr;
                index += len;
            }
            return -1;
        }

        // run the code, with the code at the given code offset in the assembly
        // and the entry point address into that
        bool Process(int offset, object entryPoint)
        {
            env.Info($"Processing code, offset {offset}, entry address {entryPoint}");
            return true;
        }


        // Read 0 terminated UTF8 string
        string ReadS(int offset)
        {
            var sb = new StringBuilder();
            while (bcode[offset] != 0)
                sb.Append((char)bcode[offset++]);
            return sb.ToString();
        }

        int ReadI(int offset, int count)
        {
            int val = 0;
            while (count-- > 0)
                val = (val << 8) + bcode[offset++];
            return val;
        }
    }
}
