﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.AST
{
    class VariableDefinitionAst:Ast
    {
        public Token ImportToken { get; set; }
        public Token ExportToken { get; set; }
        public Token ConstToken { get; set; }
        public override string ToString()
        {
            return $"{base.ToString()} : {ImportToken} {ExportToken} {ConstToken}";
        }
    }
}
