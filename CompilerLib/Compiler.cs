using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Lomont.ClScript.CompilerLib
{
    public class Compiler
    {
        public void Compile(string[] lines, TextWriter output)
        {
            output.WriteLine($"Compiling {lines.Length} lines");
            try
            {
                var parser = new Parser();
                var syntaxTree = parser.Parse(lines, output);
            }
            catch (Exception ex)
            {
                output.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
