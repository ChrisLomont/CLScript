using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Environment items for the compiler
    /// </summary>
    public class Environment
    {
        public Environment(TextWriter output)
        {
            Output = output;
        }
        public TextWriter Output { get; private set; }
    }
}
