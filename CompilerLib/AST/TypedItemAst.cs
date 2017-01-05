namespace Lomont.ClScript.CompilerLib.AST
{
    class TypedItemAst : ExpressionAst
    {
        public Token ImportToken { get; set; }
        public Token ExportToken { get; set; }
        public Token ConstToken { get; set; }

        public TypedItemAst(Token nameToken, Token baseTypeToken)
        {
            Token = nameToken;
            BaseTypeToken = baseTypeToken;
        }

        public Token BaseTypeToken;

        public override string ToString()
        {
            var bt = "";
            if (BaseTypeToken != null)
                bt = $"({BaseTypeToken.TokenValue})";
            return Format($"{bt} :: {FormatSymbol()} :: ({ImportToken?.Format(false)} {ExportToken?.Format(false)} {ConstToken?.Format(false)})");
        }
    }
}
