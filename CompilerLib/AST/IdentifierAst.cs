﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class IdentifierAst : ExpressionAst
    {
        public IdentifierAst(Token token)
        {
            Token = token;
        }
    }
}
