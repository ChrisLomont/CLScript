using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class MatchNumber : MatchBase
    {
        // numbers: integers in decimal, hex, binary, float
        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            if (!Char.IsDigit(characterStream.Current) && characterStream.Current != '.')
                return null;
            if (characterStream.StartsWith("0x") || characterStream.StartsWith("0X"))
            {
                characterStream.Consume(2);
                GetIntegers(characterStream,"0123456789ABCDEFabcdef");
                return new Token(TokenType.HexadecimalLiteral);
            }
            if (characterStream.StartsWith("0b") || characterStream.StartsWith("0B"))
            {
                characterStream.Consume(2);
                GetIntegers(characterStream,"01");
                return new Token(TokenType.BinaryLiteral);
            }

            var leftOperand = GetIntegers(characterStream,"0123456789");
            if (leftOperand != null || IsDecimalDot(characterStream))
            {
                if (IsDecimalDot(characterStream))
                {
                    // found a float
                    characterStream.Consume();
                    var rightOperand = GetIntegers(characterStream, "0123456789");
                    if (leftOperand == null && rightOperand == null)
                        return null; // nothing here

                    var exponent = GetExponent(characterStream);

                    return new Token(TokenType.FloatLiteral);
                }

                return new Token(TokenType.DecimalLiteral);
            }

            return null;
        }

        // decimal dot is differentiated from the range token '..'
        bool IsDecimalDot(CharacterStream characterStream)
        {
            return characterStream.Current == '.' && characterStream.Peek(1) != '.';
        }

        string GetExponent(CharacterStream characterStream)
        {
            if (characterStream.Current == 'e')
            {
                characterStream.Consume();
                var c = characterStream.Current;
                if (c=='-' || c== '+')
                    characterStream.Consume();
                var exp = GetIntegers(characterStream, "0123456789");
                if (String.IsNullOrEmpty(exp))
                    throw new InvalidSyntax("Invalid floating point exponent");
            }
            return "";
        }

        

        string GetIntegers(CharacterStream characterStream, string charsToMatch)
        {
            string num = null;
            if (characterStream.Current != CharacterStream.EndOfFile && charsToMatch.IndexOf(characterStream.Current) < 0)
                return null;

            while (characterStream.Current != CharacterStream.EndOfFile && (charsToMatch.IndexOf(characterStream.Current) >= 0 || characterStream.Current == '_'))
            {
                num += characterStream.Current;
                characterStream.Consume();
            }

            return num; // possibly null
        }
    }
}
