namespace Lomont.ClScript.CompilerLib.AST
{
    class LiteralAst : ExpressionAst
    {
        public LiteralAst(Token token)
        {
            Token = token;
        }
    }
}
