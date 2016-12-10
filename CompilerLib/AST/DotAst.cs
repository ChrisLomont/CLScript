﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class DotAst : Ast
    {
        public DotAst(Token token)
        {
            Token = token;
        }

        public SymbolEntry Symbol { get; set; }
    }
}
