﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class AssignItemAst : Ast
    {
        // todo - merge assign items with typed items, as well as parent lists
        public SymbolEntry Symbol;
    }
}
