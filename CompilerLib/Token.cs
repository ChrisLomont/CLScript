using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.Lexer;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Store a token
    /// </summary>
    public class Token
    {
        public Token(TokenType type, string value = "????", CharacterPosition position = null)
        {
            TokenValue = value;
            Position = position == null ? new CharacterPosition() : new CharacterPosition(position);
            TokenType = type;
        }
        public TokenType TokenType;
        public string TokenValue;
        public CharacterPosition Position;

        public override string ToString()
        {
            var v = TokenValue;
            if (TokenType == TokenType.EndOfLine)
                v = "\\n";
            var msg = $"[{TokenType}, {Position.LineNumber}:{Position.LinePosition}-{Position.LinePosition + TokenValue.Length}]";
            return $"({v}) : {msg}";
        }
    }

    public enum TokenType
    {
        None,
        Unknown, 
        
        // keywords
        Import,
        Export,
        Module,
        Enum,
        Type,
        Const,

        Bool,
        Int32,
        Float32,
        String,
        Byte,

        True,
        False,

        If,
        Else,
        While,
        For,
        In,
        By,
        Return,
        Break,
        Continue,

        // overload functions
        OpAdd,
        OpSub,
        OpDiv,
        OpMul,
        OpEq,
        OpLessThan,
        OpGreaterThan,

        // literals
        DecimalLiteral,
        BinaryLiteral,
        HexadecimalLiteral,
        FloatLiteral,
        StringLiteral,
        ByteLiteral,

        // single word identifier
        Identifier,

        // single chars
        LSquareBracket,
        RSquareBracket,
        OpenParen,
        CloseParen,

        Equals,
        GreaterThan,
        LessThan,

        Plus,
        Minus,
        Asterix,
        Slash,
        Percent,

        Ampersand,
        Caret,
        Comma,
        Dot,
        Exclamation,
        Pipe,
        Tilde,

        // batches of characters
        WhiteSpace,
        Comment,

        // multi char symbols
        AddEq,
        SubEq,
        MulEq,
        DivEq,
        XorEq,
        AndEq,
        OrEq,
        ModEq,
        RightShiftEq,
        LeftShiftEq,
        RightRotateEq,
        LeftRotateEq,

        NotEqual,
        Compare,
        GreaterThanOrEqual,
        LessThanOrEqual,
        RightRotate,
        LeftRotate,
        RightShift,
        LeftShift,
        LogicalOr,
        LogicalAnd,
        Increment,
        Decrement,
        Range,

        // formatting/blocks
        EndOfLine,
        Indent,
        Undent,
        EndOfFile
    }
}
