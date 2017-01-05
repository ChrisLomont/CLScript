namespace Lomont.ClScript.CompilerLib.Lexer
{
    public abstract class MatchBase
    {
        public Token IsMatch(CharacterStream characterStream)
        {
            if (characterStream.End)
                return new Token(TokenType.EndOfFile);

            characterStream.TakeSnapshot();
            var pos = new CharacterPosition(characterStream.Position);

            var match = IsMatchImpl(characterStream);

            if (match == null)
                characterStream.RollbackSnapshot();
            else
            {
                characterStream.CommitSnapshot();
                match.Position = new CharacterPosition(pos);
                var endIndex = characterStream.Position.TextIndex;
                match.TokenValue = characterStream.Text.Substring(pos.TextIndex, endIndex - pos.TextIndex);
            }

            return match;
        }

        protected abstract Token IsMatchImpl(CharacterStream characterStream);

    }
}
