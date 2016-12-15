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

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }

        /// <summary>
        /// Output an informational message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="items"></param>
        public void Info(string format, params object[] items)
        {
            WriteLine(String.Format(format, items));
        }

        /// <summary>
        /// Output a warning message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="items"></param>
        public void Warning(string format, params object[] items)
        {
            ++WarningCount;
            var msg = String.Format(format, items);
            WriteLine($"WARNING: {msg}");

        }

        /// <summary>
        /// Output an error message
        /// </summary>
        /// <param name="format"></param>
        /// <param name="items"></param>
        public void Error(string format, params object[] items)
        {
            ++ErrorCount;
            var msg = String.Format(format,items);
            WriteLine($"ERROR: {msg}");
        }

        void WriteLine(string message)
        {
            Output.WriteLine(message);
        }
    }
}
