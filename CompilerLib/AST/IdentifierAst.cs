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
