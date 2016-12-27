using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public class Imports
    {
        public bool HandleImport(Runtime runtime, int importIndex, string name, int parameterCount, int returnCount)
        {
            if (name == "ImportSquare" && parameterCount == 1 && returnCount == 1)
            {
                // int32 => int32
                var p = runtime.GetInt32Parameter();
                var sq = p*p;

                runtime.SetInt32Return(sq);
                return true;
            }

            if (name == "ImportSquareRoot" && parameterCount == 1 && returnCount == 1)
            {
                // r32 => r32
                var p = runtime.GetFloat32Parameter();
                var root = (float)Math.Sqrt(Math.Abs(p));
                runtime.SetFloat32Return(root);
                return true;
            }

           return false; // no matches
        }
    }
}
