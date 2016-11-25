﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
