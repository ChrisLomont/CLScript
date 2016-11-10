using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Store a token
    /// </summary>
    public class Token
    {
        public Token(string value, int lineNumber, int startIndex, TokenType type)
        {
            Value = value;
            LineNumber = lineNumber;
            StartIndex = startIndex;
            Type = type;
        }
        public string Value;
        public int LineNumber;
        public int StartIndex;
        public TokenType Type;

        public override string ToString()
        {
            return $"{Value}:{Type} [{LineNumber}:{StartIndex}]";
        }
    }

    enum TokenType
    {
        DecimalLiteral,
        BinaryLiteral,
        HexadecimalLiteral,
        FloatLiteral,
        StringLiteral,
        CharacterLiteral,
        Identifier,
        TypeName,
        Other, // keyword, operator, etc

        EndOfLine,
        Indent,
        Dedent,
        EndOfFile
    }
}
