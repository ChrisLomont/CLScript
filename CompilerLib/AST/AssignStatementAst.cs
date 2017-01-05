namespace Lomont.ClScript.CompilerLib.AST
{
    class AssignStatementAst :Ast
    {
        /// <summary>
        /// Number of stack slots used to push all right hand side of assignment
        /// </summary>
        public int StackCount { get; set; } = -1;

        public AssignStatementAst(Token token)
        {
            Token = token;
        }
    }
}
