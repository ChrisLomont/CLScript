using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    class CollapseHelperVisitor
    {
        Environment environment;
        public CollapseHelperVisitor(Environment environment)
        {
            this.environment = environment;
        }

        public void Start(Ast ast)
        {
            Visit(ast);
        }


        List<Ast> removeList = new List<Ast>();
        public void Visit(Ast ast)
        {
            var collapsed = false;
            do
            {
                removeList.Clear();
                // find offenders
                foreach (var ch in ast.Children)
                {
                    if (ch.GetType() == typeof(Parser.Parser.HelperAst))
                        removeList.Add(ch);
                }

                collapsed = removeList.Any(); // were ther any added? If so, we will have to repeat this step

                // bring grand children up, copy children tokens
                foreach (var ch in removeList)
                {
                    // collapse child to parent
                    if (ast.Token != null && ch.Token != null)
                        throw new InternalFailure("Both parent and child have token, cannot compress");
                    if (ast.Token == null)
                        ast.Token = ch.Token;
                    ast.Children.AddRange(ch.Children);
                }

                // remove children
                foreach (var a in removeList)
                    ast.Children.Remove(a);
            } while (collapsed); 

            // recurse
            foreach (var c in ast.Children)
                Visit(c);
        }
    }
}
