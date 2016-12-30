using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class Lexer
    {
        public Lexer(Environment environment, string source, string filename)
        {
            CharStream = new CharacterStream(source);
            env = environment;
            this.filename = filename;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filterTokens">Filter out uneeded tokens, simplifies stream</param>
        /// <returns></returns>
        public IEnumerable<Token> Lex(bool filterTokens = true)
        {
            Matchers = InitializeMatchList();
            var current = Next();
            while (current != null && current.TokenType != TokenType.EndOfFile)
            {

                // see if token should be skipped
                var skipToken = false;

                if (filterTokens)
                {
                    // do filtering

                    // skip whitespace
                    if (current.TokenType == TokenType.WhiteSpace)
                        skipToken = true;
                    if (current.TokenType == TokenType.Comment)
                        skipToken = true;


                }

                if (!skipToken)
                {
                    // process any indent/unindent
                    var indentTokens = indenter.ProcessToken(current);
                    foreach (var token in indentTokens)
                    {
                        token.Filename = filename;
                        yield return token;
                    }

                    current.Filename = filename;
                    yield return current;
                }
                current = Next();
            }

            // finish file by End of Line, then if needed, unindents and another End of Line
            yield return new Token(TokenType.EndOfLine,filename: filename);
            if (indenter.Indented)
            {
                while (indenter.Indented)
                {
                    yield return new Token(TokenType.Undent, filename: filename);
                    indenter.Unindent();
                }
                yield return new Token(TokenType.EndOfLine, filename: filename);
            }

        }

        readonly Indenter indenter = new Indenter();

        #region Implementation
        CharacterStream CharStream { get; set; }
        List<MatchBase> Matchers { get; set; }
        Environment env;
        string filename;

        // is not an identifier character
        Func<char, bool> notId = c => !Char.IsLetterOrDigit(c) && c != '_';

            // create an ordered list of items to match
        List<MatchBase> InitializeMatchList()
        {
            // the order here matters because it defines token precedence
            var keywords = new List<MatchBase>
            {
                new MatchKeyword(TokenType.Import, "import",notId),
                new MatchKeyword(TokenType.Export, "export",notId),
                new MatchKeyword(TokenType.Module, "module",notId),
                new MatchKeyword(TokenType.Enum, "enum",notId),
                new MatchKeyword(TokenType.Type, "type",notId),
                new MatchKeyword(TokenType.Const, "const",notId),

                new MatchKeyword(TokenType.Bool, "bool",notId),
                new MatchKeyword(TokenType.Int32, "i32",notId),
                new MatchKeyword(TokenType.Float32, "r32",notId),
                new MatchKeyword(TokenType.String, "string",notId),
                new MatchKeyword(TokenType.Byte, "byte",notId),

                new MatchKeyword(TokenType.True, "true",notId),
                new MatchKeyword(TokenType.False, "false",notId),

                new MatchKeyword(TokenType.If, "if",notId),
                new MatchKeyword(TokenType.Else, "else",notId),
                new MatchKeyword(TokenType.While, "while",notId),
                new MatchKeyword(TokenType.For, "for",notId),
                new MatchKeyword(TokenType.In, "in",notId),
                new MatchKeyword(TokenType.By, "by",notId),

                new MatchKeyword(TokenType.Return, "return",notId),
                new MatchKeyword(TokenType.Break, "break",notId),
                new MatchKeyword(TokenType.Continue, "continue",notId),

                new MatchKeyword(TokenType.OpAdd, "op+"),
                new MatchKeyword(TokenType.OpSub, "op-"),
                new MatchKeyword(TokenType.OpMul, "op*"),
                new MatchKeyword(TokenType.OpDiv, "op/"),
                new MatchKeyword(TokenType.OpEq,  "op=="),
                new MatchKeyword(TokenType.OpLessThan, "op<"),
                new MatchKeyword(TokenType.OpGreaterThan, "op>"),

            };

            // ordered by greedy match
            var specialCharacters = new List<MatchBase>
            {
                new MatchKeyword(TokenType.AddEq, "+="),
                new MatchKeyword(TokenType.SubEq, "-="),
                new MatchKeyword(TokenType.MulEq, "*="),
                new MatchKeyword(TokenType.DivEq, "/="),
                new MatchKeyword(TokenType.XorEq, "^="),
                new MatchKeyword(TokenType.AndEq, "&="),
                new MatchKeyword(TokenType.OrEq, "|="),
                new MatchKeyword(TokenType.ModEq, "%="),
                new MatchKeyword(TokenType.RightShiftEq, ">>="),
                new MatchKeyword(TokenType.LeftShiftEq, "<<="),
                new MatchKeyword(TokenType.RightRotateEq, ">>>="),
                new MatchKeyword(TokenType.LeftRotateEq, "<<<="),

                new MatchKeyword(TokenType.NotEqual, "!="),
                new MatchKeyword(TokenType.Compare, "=="),
                new MatchKeyword(TokenType.GreaterThanOrEqual, ">="),
                new MatchKeyword(TokenType.LessThanOrEqual, "<="),
                new MatchKeyword(TokenType.RightRotate, ">>>"),
                new MatchKeyword(TokenType.LeftRotate, "<<<"),
                new MatchKeyword(TokenType.RightShift, ">>"),
                new MatchKeyword(TokenType.LeftShift, "<<"),
                new MatchKeyword(TokenType.LogicalOr, "||"),
                new MatchKeyword(TokenType.LogicalAnd, "&&"),
                new MatchKeyword(TokenType.Increment, "++"),
                new MatchKeyword(TokenType.Decrement, "--"),
                new MatchKeyword(TokenType.Range, ".."),


                // single char
                new MatchKeyword(TokenType.LeftBracket, "["),
                new MatchKeyword(TokenType.RightBracket, "]"),
                new MatchKeyword(TokenType.LeftParen, "("),
                new MatchKeyword(TokenType.RightParen, ")"),

                new MatchKeyword(TokenType.Equals, "="),
                new MatchKeyword(TokenType.GreaterThan, ">"),
                new MatchKeyword(TokenType.LessThan, "<"),

                new MatchKeyword(TokenType.Plus, "+"),
                new MatchKeyword(TokenType.Minus, "-"),
                new MatchKeyword(TokenType.Asterix, "*"),
                new MatchKeyword(TokenType.Slash, "/"),
                new MatchKeyword(TokenType.Percent, "%"),

                new MatchKeyword(TokenType.Ampersand, "&"),
                new MatchKeyword(TokenType.Caret, "^"),
                new MatchKeyword(TokenType.Comma, ","),
                new MatchKeyword(TokenType.Dot, "."),
                new MatchKeyword(TokenType.Exclamation, "!"),
                new MatchKeyword(TokenType.Pipe, "|"),
                new MatchKeyword(TokenType.Tilde, "~"),
            };

            // assemble things to match in order
            var matchers = new List<MatchBase>(64);

            matchers.Add(new MatchComment());
            matchers.Add(new MatchEndOfLine());

            matchers.Add(new MatchString(MatchString.QUOTE));
            matchers.Add(new MatchString(MatchString.TIC));
            // this must come before operators to prevent '.1' being DOT ONE as opoosed to floating point 0.1
            matchers.Add(new MatchNumber());

            matchers.AddRange(specialCharacters);
            matchers.AddRange(keywords);
            matchers.AddRange(new List<MatchBase>
            {
                new MatchWhiteSpace(),
                new MatchIdentifier()
            });

            // this matcher goes last, and fires an error for unrecognized text
            matchers.Add(new ThrowExceptionMatcher());

            return matchers;
        }

        // this does all the work in making tokens for the stream
        Token Next()
        {
            try
            {
                if (CharStream.End)
                    return new Token(TokenType.EndOfFile, filename: filename);
                var t =
                (from match in Matchers
                    let token = match.IsMatch(CharStream)
                    where token != null
                    select token).FirstOrDefault();
                return t;
            }
            catch (Exception ex)
            {
                throw new Exception($"{CharStream}",ex);
            }
        }
        #endregion
    }
}
