using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // http://stackoverflow.com/questions/1649027/how-do-i-print-out-a-tree-structure
    class PrintVisitor
    {
        Environment environment;
        public PrintVisitor(Environment environment)
        {
            this.environment = environment;
        }

        public void Start(Ast ast)
        {
            Visit(ast, "" , true);
        }


        public void Visit(Ast ast, string indent, bool last)
        {
            environment.Output.Write(indent);
            if (last)
            {
                environment.Output.Write("\\-");
                indent += "  ";
            }
            else
            {
                environment.Output.Write("|-");
                indent += "| ";
            }
            environment.Output.WriteLine(ast.ToString());

            for (var i = 0; i < ast.Children.Count; i++)
                Visit(ast.Children[i],indent, i == ast.Children.Count - 1);
        }
    }
}
