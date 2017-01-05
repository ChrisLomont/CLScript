namespace Lomont.ClScript.CompilerLib.AST
{
    class VariableDefinitionAst:Ast
    {
        /// <summary>
        /// Number of stack slots used to push all right hand side of assignment
        /// </summary>
        public int StackCount { get; set; } = -1;

    }
}
