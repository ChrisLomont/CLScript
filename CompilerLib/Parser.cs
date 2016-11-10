using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    class Parser
    {

        // hand crafted recursive descent parser
        // http://www.cs.engr.uky.edu/~lewis/essays/compilers/rec-des.html
        // 
        // http://matt.might.net/articles/grammars-bnf-ebnf/
        //
        // good resources
        // http://stackoverflow.com/questions/2245962/is-there-an-alternative-for-flex-bison-that-is-usable-on-8-bit-embedded-systems/2336769#2336769
        // says for each rule of form X = A B C 
        // subroutine X()
        //     if ~(A()) return false;
        //     if ~(B()) { error(); return false; }
        //     if ~(C()) { error(); return false; }
        //     // insert semantic action here: generate code, do the work, ....
        //     return true;
        // end X;
        // 
        // rule T  =  '('  T  ')' becomes
        // subroutine T()
        //     if ~(left_paren()) return false
        //     if ~(T()) { error(); return false; }
        //     if ~(right_paren()) { error(); return false; }
        //     // insert semantic action here: generate code, do the work, ....
        //     return true;
        // end T
        //
        // P = Q | R  becomes
        // subroutine P()
        //     if ~(Q)
        //         {if ~(R) return false;
        //          return true;
        //         }
        //     return true;
        // end P;
        // 
        // L  =  A |  L A  becomes
        // subroutine L()
        //     if ~(A()) then return false;
        //     while (A()) do // loop
        //     return true;
        // end L;
        //
        //
        // ref http://stackoverflow.com/questions/25049751/constructing-an-abstract-syntax-tree-with-a-list-of-tokens/25106688#25106688
        // AST generation 




        Tokenizer tokenizer;

        void DumpTokenizer(TextWriter output)
        {
            while (true)
            {
                var t = tokenizer.TakeToken();
                output.WriteLine(t);
                if (t.Type == TokenType.EndOfFile)
                    break;
            }
        }

        /// <summary>
        /// Parse the lines, return the syntax tree, throw on error
        /// </summary>
        /// <returns></returns>
        public AstNode Parse(string[] lines, TextWriter output)
        {
            tokenizer = new Tokenizer(lines);


            return null;
        }

        #region Productions

        AstNode ParseEnumVal()
        {
            if (tokenizer.PeekToken().Type == TokenType.Identifier)
            {
                var idToken = tokenizer.TakeToken();

                Token literalToken = null;
                if (tokenizer.PeekToken().Value == "=")
                {
                    tokenizer.TakeToken();
                    literalToken = tokenizer.TakeToken();
                    if (literalToken.Type != TokenType.BinaryLiteral &&
                        literalToken.Type != TokenType.BinaryLiteral &&
                        literalToken.Type != TokenType.BinaryLiteral)
                        throw new Exception($"Invalid enum literal {literalToken}");
                }
                if (tokenizer.TakeToken().Type != TokenType.EndOfLine)
                    throw new Exception($"enum entry not terminated");
                return AstNode.Make(idToken,literalToken);
            }
            return null;
        }

        #endregion


    }
}
