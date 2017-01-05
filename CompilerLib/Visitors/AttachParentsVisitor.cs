using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    class AttachParentsVisitor
    {
        public static void AttachParents(Ast ast, Ast parent = null)
        {
            ast.Parent = parent;
            foreach (var child in ast.Children)
                AttachParents(child, ast);
        }
    }
}
