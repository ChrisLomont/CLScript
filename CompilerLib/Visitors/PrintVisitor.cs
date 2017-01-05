using System;
using System.Collections.Generic;
using System.IO;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // http://stackoverflow.com/questions/1649027/how-do-i-print-out-a-tree-structure
    class PrintVisitor
    {
        TextWriter output;
        public PrintVisitor(TextWriter outut)
        {
            output = outut;
        }

        public void Start(Ast ast)
        {
            // remap for pretty printing
            var sw = new StringWriter();
            var temp = output;
            output = sw;

            // header
            output.WriteLine("AST node :: token :: TokenType :: Type :: Value/Base type :: symbol :: const/import/export");

            // recurse tree
            Visit(ast, "" , true);
            output = temp;
            
            // align items
            Align(sw.ToString().Split('\n'));
        }

        void Align(string[] lines)
        {
            // get column sizes
            var cols = new List<int>();
            foreach (var line in lines)
            {
                var words = line.Split(new[] {"::"}, StringSplitOptions.None);
                while (cols.Count < words.Length)
                    cols.Add(0);
                for (var i = 0; i < words.Length; ++i)
                    cols[i] = Math.Max(cols[i], words[i].Length);
            }

            // output
            foreach (var line in lines)
            {
                var words = line.Split(new[] {"::"}, StringSplitOptions.None);
                for (var i = 0; i < words.Length; ++i)
                {
                    var w = words[i].Replace("\r", "");
                    var format = $"{{0,-{cols[i] + 2}}}";
                    output.Write(format, w);
                }
                output.WriteLine();
            }
        }


        public void Visit(Ast ast, string indent, bool last)
        {
            output.Write(indent);
            if (last)
            {
                output.Write("\\-");
                indent += "  ";
            }
            else
            {
                output.Write("|-");
                indent += "| ";
            }
            output.WriteLine(ast.ToString());
            for (var i = 0; i < ast.Children.Count; i++)
                Visit(ast.Children[i],indent, i == ast.Children.Count - 1);

        }
    }
}
