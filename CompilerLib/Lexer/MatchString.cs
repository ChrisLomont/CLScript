using System.Text;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class MatchString : MatchBase
    {
        public const char Quote = '\"';

        public const char Tic = '\'';

        char StringDelim { get; }

        public MatchString(char delim)
        {
            StringDelim = delim;
        }


        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            var str = new StringBuilder();

            var matched = false;
            if (characterStream.Current == StringDelim)
            {
                matched = true;
                characterStream.Consume();

                while (!characterStream.End && characterStream.Current != StringDelim)
                {
                    str.Append(characterStream.Current);
                    characterStream.Consume();
                }

                if (characterStream.Current == StringDelim)
                {
                    characterStream.Consume();
                }
            }

            if (matched)
            {
                if (StringDelim == Quote) // allow 0 length strings
                    return new Token(TokenType.StringLiteral, str.ToString());
                else if (str.Length == 1)
                    return new Token(TokenType.ByteLiteral, str.ToString());
            }

            return null;
        }
    }
}
