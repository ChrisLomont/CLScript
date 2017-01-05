using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public static class Imports
    {
        // example handler for imported functions
        // todo - make the chain much more efficient, perhaps let imports tag an index into a table for speed?

        public static bool HandleImport(
            Runtime runtime, 
            int importIndex, 
            string importName, 
            int parameterCount,
            int returnCount)
        {
            if (importName == "SquareInt" && parameterCount == 1 && returnCount == 1)
            {
                // int32 => int32
                var p = runtime.GetInt32Parameter();
                runtime.SetInt32Return(p*p);
                return true;
            }
            if (importName == "SquareFloat" && parameterCount == 1 && returnCount == 1)
            {
                // float32 => float32
                var p = runtime.GetFloat32Parameter();
                runtime.SetFloat32Return(p*p);
                return true;
            }

            if (importName == "SquareRootInt" && parameterCount == 1 && returnCount == 1)
            {
                // int32 => int32
                var p = runtime.GetInt32Parameter();
                var root = (float) Math.Sqrt(Math.Abs(p));
                runtime.SetInt32Return((int) root);
                return true;
            }

            if (importName == "SquareRootFloat" && parameterCount == 1 && returnCount == 1)
            {
                // float32 => float32
                var p = runtime.GetFloat32Parameter();
                var root = (float) Math.Sqrt(Math.Abs(p));
                runtime.SetFloat32Return(root);
                return true;
            }
            if (importName == "MakeColor" && parameterCount == 1 && returnCount == 1)
            {
                // pick color 0-7 as binary expansion of selector
                // int32 => RGB(i32,i32,i32)
                var select = runtime.GetInt32Parameter();
                runtime.SetInt32Return((select >> 2) & 1);
                runtime.SetInt32Return((select >> 1) & 1);
                runtime.SetInt32Return((select >> 0) & 1);
                return true;
            }
            if (importName == "CycleRGB" && parameterCount == 1 && returnCount == 0)
            {
                // modify color R->G->B->R
                // RGB(i32,i32,i32) => ()
                var address = runtime.GetInt32Parameter();
                var r = runtime.GetInt32Address(address);
                var g = runtime.GetInt32Address(address+1);
                var b = runtime.GetInt32Address(address+2);

                runtime.SetInt32Address(b,address);
                runtime.SetInt32Address(r,address + 1);
                runtime.SetInt32Address(g,address + 2);

                return true;
            }

            return false; // no matches
        }
    }
}
