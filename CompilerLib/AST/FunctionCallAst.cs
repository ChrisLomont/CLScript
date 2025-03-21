﻿namespace Lomont.ClScript.CompilerLib.AST
{
    class FunctionCallAst : ExpressionAst
    {
        public FunctionCallAst(Token identifier, Ast parameters)
        {
            Token = identifier;
            Children.AddRange(parameters.Children);
        }
    }
}
