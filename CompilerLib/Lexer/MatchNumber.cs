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


        // numbers: integers in decimal, hex, binary
        protected override Token IsMatchImpl(CharacterStream characterStream)
        {
            if (!Char.IsDigit(characterStream.Current) && characterStream.Current != '.')
                return null;
            if (characterStream.StartsWith("0x") || characterStream.StartsWith("0X"))
            {
                characterStream.Consume(2);
                GetIntegers(characterStream);
                return new Token(TokenType.HexadecimalLiteral);
            }
            if (characterStream.StartsWith("0b") || characterStream.StartsWith("0B"))
            {
                characterStream.Consume(2);
                GetIntegers(characterStream);
                return new Token(TokenType.BinaryLiteral);
            }

            var leftOperand = GetIntegers(characterStream);
            if (leftOperand != null || characterStream.Current == '.')
            {
                if (characterStream.Current == '.')
                {
                    // found a float
                    characterStream.Consume();
                    var rightOperand = GetIntegers(characterStream);
                    if (leftOperand == null && rightOperand == null)
                        return null; // nothing here

                    var exponent = GetExponent(characterStream);

                    return new Token(TokenType.FloatLiteral);
                }

                return new Token(TokenType.DecimalLiteral);
            }

            return null;
        }

        string GetExponent(CharacterStream characterStream)
        {
            if (characterStream.Current == 'e')
            {
                characterStream.Consume();
                var c = characterStream.Current;
                if (c=='-' || c== '+')
                    characterStream.Consume();
                var exp = GetIntegers(characterStream);
                if (String.IsNullOrEmpty(exp))
                    throw new InvalidSyntax("Invalid floating point exponent");
            }
            return "";
        }

        string GetIntegers(CharacterStream characterStream)
        {
            string num = null;

            if (characterStream.Current != CharacterStream.EndOfFile && !Char.IsDigit(characterStream.Current))
                return null;

            while (characterStream.Current != CharacterStream.EndOfFile && (Char.IsDigit(characterStream.Current)|| characterStream.Current == '_'))
            {
                num += characterStream.Current;
                characterStream.Consume();
            }

            return num; // possibly null
        }
    }
}
