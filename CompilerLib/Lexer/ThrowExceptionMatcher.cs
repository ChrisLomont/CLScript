namespace Lomont.ClScript.CompilerLib.Lexer
{
    class ThrowExceptionMatcher : MatchBase
    {
        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            throw new InvalidSyntax($"Unsupported syntax character : '{characterStream.Current}'");
        }
    }
}
