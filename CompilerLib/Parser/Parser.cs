using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib.Parser
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
    // useful articles  http://onoffswitch.net/, code https://github.com/devshorts/LanguageCreator/tree/master/Lang

    public class Parser
    {
        ParseableTokenStream TokenStream { get; set; }

        public Parser(Lexer.Lexer lexer)
        {
            TokenStream = new ParseableTokenStream(lexer);
        }

        public Ast Parse(Environment environment)
        {
            this.environment = environment;
            var ast = ParseDeclarations(); // start here
            return ast;
        }



        Environment environment;

        #region Grammar

        // <Declarations> ::= <Declaration> <Declarations> | ! a file is a list of zero or more declarations
        Ast ParseDeclarations()
        {
            var decls = new DeclarationsAst(null);

            Ast decl = null;
            do
            {
                decl = ParseDeclaration();
                if (decl != null)
                    decls.Children.Add(decl);
            } while (decl != null);
            return decls;
        }

        // a declaration is one of many items
        Ast ParseDeclaration()
        {
            // eat any extra end of line
            while (TokenStream.Current.TokenType == TokenType.EndOfLine)
                TokenStream.Consume();
            return MatchOr(
                ParseImportDeclaration,
                ParseModuleDeclaration,
                ParseAttributeDeclaration,
                ParseEnumDeclaration
                // ParseTypeDeclaration, // todo
                // ParseVariableDeclaration,
                // ParseFunctionDeclaration
            );
        }

        Ast ParseImportDeclaration()
        {
            return MatchSequence(
                typeof(ImportAst),
                Match("import"),
                Keep(Match(TokenType.StringLiteral)),
                Match(TokenType.EndOfLine)
            );
        }

        Ast ParseModuleDeclaration()
        {
            return MatchSequence(
                typeof(ModuleAst),
                Match("module"),
                Keep(Match(TokenType.Identifier)),
                Match(TokenType.EndOfLine)
                );
        }

        Ast ParseAttributeDeclaration()
        {
            return MatchSequence(
                typeof(AttributeAst),
                Match(TokenType.LSquareBracket),
                Keep(Match(TokenType.Identifier)),
                Keep(ZeroOrMore(Match(TokenType.StringLiteral))),
                Match(TokenType.LSquareBracket),
                Match(TokenType.EndOfLine)
                );
        }

        Ast ParseEnumDeclaration()
        {
            return MatchSequence(
                typeof(EnumAst),
                Match("enum"),
                Keep(Match(TokenType.Identifier)),
                OneOrMore(Match(TokenType.EndOfLine)),
                Match(TokenType.Indent),
                Keep(OneOrMore(ParseEnumValue)),
                Match(TokenType.Undent)
            );
        }

        Ast ParseEnumValue()
        {
            return MatchOr(
                    ()=> MatchSequence(
                        typeof (EnumValueAst),
                        Keep(Match(TokenType.Identifier)),
                        OneOrMore(Match(TokenType.EndOfLine))
                        ),
                    () => MatchSequence(
                        typeof (EnumValueAst)),
                        Keep(Match(TokenType.Identifier)),
                        Match("="), 
                        //Keep(ParseExpression), todo
                        OneOrMore(Match(TokenType.EndOfLine))
                    );
        }


        #endregion 

        #region Helpers

        delegate Ast ParseDelegate();

        // match sequence of things, if all match, create type, and 
        // populate with items marked with keep
        Ast MatchSequence(Type astType, params ParseDelegate[] funcs)
        {
            PreCheck();
            var matched = true;
            var kept = new List<Ast>();
            foreach (var func in funcs)
            {
                var ast = func();
                if (ast == null)
                {
                    matched = false;
                    break;
                }
                if (ast is HelperAst && (ast as HelperAst).Keep)
                    kept.Add(ast);
            }
            PostCheck(matched);
            if (matched)
            {
                var ast = (Ast)Activator.CreateInstance(astType, kept);
                return ast;
            }
            return null;
        }

        // return first parse function that returns a tree, else return null
        Ast MatchOr(params ParseDelegate[] parseFunctions)
        {
            foreach (var func in parseFunctions)
            {
                var ast = TryParse(func);
                if (ast != null) return ast;
            }
            return null;
        }

        // try to parse, if nothing, rollback internals
        Ast TryParse(ParseDelegate func)
        {
            PreCheck();
            var ast = func();
            PostCheck(ast != null);
            return ast;
        }

        void PreCheck()
        {
            TokenStream.TakeSnapshot();
        }

        void PostCheck(bool commit)
        {
            if (!commit)
                TokenStream.RollbackSnapshot();
            else
                TokenStream.CommitSnapshot();
        }

        // wrap a token, ast, or other item to keep when parsing
        ParseDelegate Keep(ParseDelegate func)
        {
            var ast = func();
            if (ast == null)
                return () => null; // nothing to see here
            (ast as HelperAst).Keep = true;
            return () => ast;
        }

        ParseDelegate ZeroOrMore(ParseDelegate func)
        {
            var asts = new List<Ast>();
            var ast = func();
            while (ast != null)
            {
                asts.Add(ast);
                ast = func();
            }
            return () => new HelperAst(null, asts);
        }

        ParseDelegate OneOrMore(ParseDelegate func)
        {
            var asts = new List<Ast>();
            var ast = func();
            while (ast != null)
            {
                asts.Add(ast);
                ast = func();
            }
            if (!asts.Any())
                return () => null; // failed
            return () => new HelperAst(null, asts);
        }

        ParseDelegate Match(string match)
        {
            return () =>
            {
                if (TokenStream.Current.TokenValue == match)
                {
                    var token = TokenStream.Current;
                    TokenStream.Consume();
                    return new HelperAst(token);
                }
                return null;
            };
        }

        ParseDelegate Match(TokenType tokenType)
        {
            return () =>
            {
                if (TokenStream.Current.TokenType == tokenType)
                {
                    var token = TokenStream.Current;
                    TokenStream.Consume();
                    return new HelperAst(token);
                }
                return null;
            };
        }

        class HelperAst : Ast
        {
            public HelperAst(Token token, List<Ast> asts = null) : base(token)
            {
                if (asts != null)
                    Children.AddRange(asts);
            }

            public bool Keep { get; set; }
        }
        #endregion

    }
}

