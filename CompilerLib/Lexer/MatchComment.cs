namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class MatchComment : MatchBase
    {
        public static string LineComment  = "//";
        public static string CommentOpen  = "/*";
        public static string CommentClose = "*/";
        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            if (characterStream.StartsWith(LineComment))
            {
                characterStream.Consume(LineComment.Length);
                while (!characterStream.End && !MatchEndOfLine.IsEndOfLineChar(characterStream.Current))
                    characterStream.Consume();
                return new Token(TokenType.Comment);
            }
            if (characterStream.StartsWith(CommentOpen))
            {
                characterStream.Consume(CommentOpen.Length);
                var commentDepth = 1; // nested comments

                while (!characterStream.End && commentDepth > 0)
                {
                    if (characterStream.StartsWith(CommentOpen))
                    {
                        characterStream.Consume(CommentOpen.Length);
                        commentDepth++;
                    }
                    else if (characterStream.StartsWith(CommentClose))
                    {
                        characterStream.Consume(CommentClose.Length);
                        commentDepth--;
                    }
                    else
                        characterStream.Consume();
                }
                return new Token(TokenType.Comment);
            }
            return null;
        }

    }
}
