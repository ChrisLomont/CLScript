namespace Lomont.ClScript.CompilerLib.AST
{
    public class FunctionDeclarationAst : Ast
    {
        public Token ImportToken { get; set; }
        public Token ExportToken { get; set; }

        public SymbolTable SymbolTable { get; set; }
        public SymbolEntry Symbol { get; set; }

        public override string ToString()
        {
            return $"{base.ToString()} :: :: ({ImportToken?.Format(false)} {ExportToken?.Format(false)})";
        }
    }
}
