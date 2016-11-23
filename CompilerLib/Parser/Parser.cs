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
            var decls = new DeclarationsAst();

            Ast decl = null;
            do
            {
                decl = ParseDeclaration();
                if (decl != null)
                    decls.Children.Add(decl);
            } while (decl != null);
            if (TokenStream.Current.TokenType != TokenType.EndOfFile)
                environment.Output.WriteLine($"Could not parse {TokenStream.Current}");
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
                ParseEnumDeclaration,
                ParseTypeDeclaration,
                ParseVariableDeclaration,
                ParseFunctionDeclaration
            );
        }

        Ast ParseOptionalConst()
        {
            if (TokenStream.Current.TokenValue == "const")
                return new HelperAst(TokenStream.Consume());
            return new HelperAst();
        }
        Ast ParseOptionalExport()
        {
            if (TokenStream.Current.TokenValue == "export")
                return new HelperAst(TokenStream.Consume());
            return new HelperAst();
        }
        Ast ParseOptionalImport()
        {
            if (TokenStream.Current.TokenValue == "import")
                return new HelperAst(TokenStream.Consume());
            return new HelperAst();
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
                        typeof (EnumValueAst),
                        Keep(Match(TokenType.Identifier)),
                        Match("="), 
                        Keep(ParseExpression),
                        OneOrMore(Match(TokenType.EndOfLine))
                        )
                    );
        }

        Ast ParseType()
        {
            var t = TokenStream.Current;
            if (
                t.TokenType == TokenType.Bool ||
                t.TokenType == TokenType.Int32 ||
                t.TokenType == TokenType.Float32 ||
                t.TokenType == TokenType.String ||
                t.TokenType == TokenType.Char ||
                t.TokenType == TokenType.Identifier
            )
            {
                TokenStream.Consume();
                return new TypeAst(t);
            }
            return null;
        }

        Ast ParseTypeDeclaration()
        {
            return MatchSequence(
                typeof(TypeDeclarationAst),
                Match("type"),
                Keep(Match(TokenType.Identifier)),
                ParseEoLs,
                Match(TokenType.Indent),
                Keep(ParseTypeMemberDefinitions),
                Match(TokenType.Undent)
                );
        }

        // one or more end of line
        Ast ParseEoLs()
        {
            return OneOrMore(Match(TokenType.EndOfLine))();
        }

        Ast ParseTypeMemberDefinitions()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(TypeMemberDefinitionAst),
                    Keep(ParseVariableTypeAndNames),
                    ParseEoLs,
                    ParseTypeMemberDefinitions
                    ),
                ()=>MatchSequence(
                    typeof(TypeMemberDefinitionAst),
                    Keep(ParseVariableTypeAndNames),
                    ParseEoLs
                    )
            );
        }

        Ast ParseVariableDeclaration()
        {
            return MatchOr(
                () =>MatchSequence(
                    typeof(VariableDeclarationAst),
                    Keep(ParseImportVariable),
                    Match(TokenType.EndOfLine)
                    ),
                () => MatchSequence(
                    typeof(VariableDeclarationAst),
                    Keep(ParseNormalVariable),
                    Match(TokenType.EndOfLine)
                    )
            );
        }

        Ast ParseImportVariable()
        {
            return MatchSequence(
                typeof(VariableDeclarationAst),
                Keep(Match("import")),
                Keep(ParseOptionalConst),
                Keep(ParseVariableTypeAndNames)
            );
        }

        Ast ParseNormalVariable()
        {
            return MatchSequence(
                typeof(VariableDeclarationAst),
                Keep(ParseOptionalExport),
                Keep(ParseOptionalConst),
                Keep(ParseVariableDefinition)
            );
        }

        Ast ParseVariableDefinition()
        {
            return MatchSequence(
                typeof(HelperAst),
                Keep(ParseVariableTypeAndNames),
                Keep(ParseVariableInitializer)
                );
        }

        Ast ParseVariableTypeAndNames()
        {
            return MatchSequence(
                typeof(VariableTypeAndNamesAst),
                Keep(ParseType),
                Keep(ParseIDList)
                );
        }

        Ast ParseIDList()
        {
            return MatchOr(
                () => MatchSequence(
                    typeof(TypeMemberDefinitionAst),
                    Keep(Match(TokenType.Identifier)),
                    Keep(ParseOptionalArray),
                    Match(","),
                    Keep(ParseIDList)
                    ),
                () => MatchSequence(
                    typeof(TypeMemberDefinitionAst),
                    Keep(Match(TokenType.Identifier)),
                    Keep(ParseOptionalArray)
                    )
            );
        }

        Ast ParseOptionalArray()
        {
            return ZeroOrOne(
                ()=> MatchSequence(
                    typeof(ArrayAst),
                    Match("["),
                    Keep(ParseExpressionList1),
                    Match("]")
                    )
                    )();
        }

        Ast ParseVariableInitializer()
        {
            return ZeroOrOne(
                    ()=>MatchSequence(
                        typeof(HelperAst),
                        Match("="),
                        Keep(ParseInitializerList)
                    ))();
        }

        Ast ParseInitializerList()
        {
            return ParseExpressionList1();
        }

        #region Function parsing statements

        Ast ParseFunctionDeclaration()
        {
            return MatchOr(
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match("import")),
                    Keep(ParseFunctionPrototype),
                    ParseEoLs
                ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseOptionalExport),
                    Keep(ParseFunctionPrototype),
                    ParseEoLs,
                    ParseBlock
                )
            );
        }

        Ast ParseFunctionPrototype()
        {
            return MatchSequence(
                typeof(FunctionPrototypeAst),
                Match("("),
                Keep(ParseReturnTypes),
                Match(")"),
                Keep(ParseFunctionName),
                Match("("),
                Keep(ParseFunctionParameters),
                Match(")")
                );
        }

        Ast ParseReturnTypes()
        {
            var ast = MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseType),
                    Match(","),
                    ParseReturnTypes
                    ),
                ParseType
            );
            return ast ?? new HelperAst();
        }

        Ast ParseFunctionName()
        {
            var t = TokenStream.Current;
            if (t.TokenType == TokenType.Identifier ||
                t.TokenValue == "op+" ||
                t.TokenValue == "op-" ||
                t.TokenValue == "op/" ||
                t.TokenValue == "op*" ||
                t.TokenValue == "op==" ||
                t.TokenValue == "op!="
                )
                return new HelperAst(TokenStream.Consume());
            return null;
        }

        Ast ParseFunctionParameters()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseParameter),
                    Match(","),
                    Keep(ParseFunctionParameters)
                    ),
                ParseParameter,
                AlwaysMatch
                );
        }

        Ast ParseParameter()
        {
            return MatchSequence(
                typeof(HelperAst),
                Keep(ParseType),
                Keep(OneOf("&","")), // optional reference
                Keep(Match(TokenType.Identifier)),
                Keep(ParseOptionalEmptyArray)
                );
        }

        Ast ParseOptionalEmptyArray()
        {
            if (TokenStream.Current.TokenType == TokenType.LSquareBracket)
            {
                // empty array denoting size
                return MatchSequence(
                    typeof(HelperAst),
                    Keep(ZeroOrMore(Match(",")))
                );
            }
            return new HelperAst(); // nothing
        }

        Ast ParseStatement()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(VariableDefinitionAst),
                    Keep(ParseVariableDefinition),
                    ParseEoLs
                    ),
                () => MatchSequence(
                    typeof(AssignStatementAst),
                    Keep(ParseAssignStatement),
                    ParseEoLs
                    ),
                () => MatchSequence(
                    typeof(IfStatementAst),
                    Keep(ParseIfStatement)
                    ),
                () => MatchSequence(
                    typeof(ForStatementAst),
                    Keep(ParseForStatement)
                    ),
                () => MatchSequence(
                    typeof(WhileStatementAst),
                    Keep(ParseWhileStatement)
                    ),
                () => MatchSequence(
                    typeof(FunctionCallAst),
                    Keep(ParseFunctionCall),
                    ParseEoLs
                    ),
                () => MatchSequence(
                    typeof(JumpStatementAst),
                    Keep(ParseJumpStatement),
                    ParseEoLs
                    )
                );
        }

        static string[] assignOperators = {"=","+=","-=","*=","/=","^=","&=","|=","%=",">>=","<<=",">>>=","<<<="};
        Ast ParseAssignStatement()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseAssignList),
                    Keep(OneOf(assignOperators)),
                    Keep(ParseExpressionList1)
                    ),
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseAssignList),
                    Keep(OneOf("++","--"))
                    )
            );
        }

        Ast ParseAssignList()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseAssignItem),
                    Match(","),
                    Keep(ParseAssignList)
                    ),
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseAssignItem)
                    )
                );
        }

        Ast ParseAssignItem()
        {
            return MatchOr(
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match(TokenType.Identifier)),
                    Keep(Match("[")),
                    Keep(ParseExpressionList1),
                    Keep(Match("]")),
                    Keep(Match(".")),
                    Keep(ParseAssignItem)
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match(TokenType.Identifier)),
                    Keep(Match("[")),
                    Keep(ParseExpressionList1),
                    Keep(Match("]"))
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match(TokenType.Identifier)),
                    Keep(Match(".")),
                    Keep(ParseAssignItem)
                    ),
                Match(TokenType.Identifier)
                );
        }

        Ast ParseIfStatement()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Match("if"),
                    Keep(ParseExpression),
                    ParseEoLs,
                    Keep(ParseBlock),
                    Match("else"),
                    Keep(ParseIfStatement)
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Match("if"),
                    Keep(ParseExpression),
                    ParseEoLs,
                    Keep(ParseBlock),
                    Match("else"),
                    ParseEoLs,
                    Keep(ParseBlock)
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Match("if"),
                    Keep(ParseExpression),
                    ParseEoLs,
                    Keep(ParseBlock)
                    )
            );
        }

        Ast ParseJumpStatement()
        {
            var t = TokenStream.Current.TokenValue;
            if (t == "break" || t == "continue" || t == "return")
            {
                var h = new HelperAst(TokenStream.Consume());
                if (t == "return")
                {
                    var retval = ParseExpressionList0();
                    if (retval != null)
                        h.Children.Add(retval);
                }
                return h;
            }
            return null;
        }

        Ast ParseForStatement()
        {
            return MatchSequence(
                typeof(HelperAst), 
                Match("for"),
                Keep(Match(TokenType.Identifier)),
                Match("in"),
                Keep(ParseForRange),
                ParseEoLs,
                Keep(ParseBlock)
            );
        }

        // a,b,c or a,b or array item
        Ast ParseForRange()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseExpression),
                    Match(","),
                    Keep(ParseExpression),
                    Match(","),
                    Keep(ParseExpression)
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseExpression),
                    Match(","),
                    Keep(ParseExpression)
                    ),
                ParseAssignItem
                );
        }

        Ast ParseWhileStatement()
        {
            return MatchSequence(
                typeof(HelperAst),
                Match("while"),
                Keep(ParseExpression),
                ParseEoLs,
                Keep(ParseBlock)
                );
        }

        Ast ParseBlock()
        {
            return MatchSequence(
                typeof(BlockAst),
                Match(TokenType.Indent),
                Keep(ZeroOrMore(() => ParseStatement())),
                Match(TokenType.Undent)
            );
        }

        #endregion

        Ast ParseExpressionList0()
        {
            return ZeroOrOne(
                ParseExpressionList1
            )();
        }

        Ast ParseExpressionList1()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseExpression),
                    Match(","),
                    Keep(ParseExpressionList1)
                    ),
                Keep(ParseExpression)
                );
        }

        #region Parse Expression

        Ast ParseExpression()
        {
            return ParseLogicalORExpression();
        }

        Ast ParseLogicalORExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(ParseLogicalANDExpression),
                        Keep(Match("||")),
                        Keep(ParseLogicalORExpression)
                    ),
                    ParseLogicalANDExpression
                );
        }
        Ast ParseLogicalANDExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(ParseInclusiveOrExpression),
                        Keep(Match("&&")),
                        Keep(ParseLogicalANDExpression)
                    ),
                    ParseInclusiveOrExpression
                );
        }
        Ast ParseInclusiveOrExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(ParseExclusiveOrExpression),
                        Keep(Match("|")),
                        Keep(ParseInclusiveOrExpression)
                    ),
                    ParseExclusiveOrExpression
                );
        }
        Ast ParseExclusiveOrExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(ParseAndExpression),
                        Keep(Match("&")),
                        Keep(ParseExclusiveOrExpression)
                    ),
                    ParseAndExpression
                );
        }
        Ast ParseAndExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(EqualityExpression),
                        Keep(Match("&")),
                        Keep(ParseAndExpression)
                    ),
                    EqualityExpression
                );
        }
        Ast EqualityExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(RelationalExpression),
                        Keep(OneOf("==","!=")),
                        Keep(EqualityExpression)
                    ),
                    RelationalExpression
                );
        }

        Ast RelationalExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(ShiftExpression),
                        Keep(OneOf("<=", ">=", "<", ">")),
                        Keep(RelationalExpression)
                    ),
                    ShiftExpression
                );
        }

        Ast ShiftExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(RotateExpression),
                        Keep(OneOf("<<", ">>")),
                        Keep(ShiftExpression)
                    ),
                    RotateExpression
                );
        }
        Ast RotateExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(AdditiveExpression),
                        Keep(OneOf("<<<", ">>>")),
                        Keep(RotateExpression)
                    ),
                    AdditiveExpression
                );
        }
        Ast AdditiveExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(MultiplicativeExpression),
                        Keep(OneOf("+", "-")),
                        Keep(AdditiveExpression)
                    ),
                    MultiplicativeExpression
                );
        }
        Ast MultiplicativeExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(UnaryExpression),
                        Keep(OneOf("*", "/","%")),
                        Keep(MultiplicativeExpression)
                    ),
                    UnaryExpression
                );
        }
        Ast UnaryExpression()
        {
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(HelperAst),
                        Keep(OneOf("++","--","+","-","~","!")),
                        Keep(UnaryExpression)
                    ),
                    PostfixExpression
                );
        }
        Ast PostfixExpression()
        {
            return
                MatchSequence(
                    typeof(HelperAst),
                    Keep(PrimaryExpression),
                    Keep(Postfix2));
        }
        Ast PrimaryExpression()
        {
            return
                MatchOr(
                    LiteralExpression,
                    ParseFunctionCall,
                    Match(TokenType.Identifier),
                    ()=>MatchSequence(
                            typeof(HelperAst),
                            Keep(Match("(")),
                            Keep(ParseExpression),
                            Keep(Match(")"))
                            )
                );
        }
        Ast Postfix2()
        {
            var ast = MatchSequence(
                          typeof(HelperAst),
                          Keep(Postfix3),
                          Keep(Postfix2)
                      ) ?? new HelperAst();
            return ast;
        }
        Ast Postfix3()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(Match("[")),
                    Keep(ParseExpressionList1),
                    Keep(Match("]"))
                    ),
//                () => MatchSequence( // todo - what is this for?
//                    typeof(HelperAst),
//                    Keep(Match("(")),
//                    Keep(ParseExpressionList0),
//                    Keep(Match(")"))
//                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match(".")),
                    Keep(Match(TokenType.Identifier))
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match("++"))
                    ),
                () => MatchSequence(
                    typeof(HelperAst),
                    Keep(Match("--"))
                    )
                );
        }

        Ast LiteralExpression()
        {
            if (TokenStream.Current.TokenType == TokenType.BinaryLiteral)
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenType == TokenType.HexadecimalLiteral)
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenType == TokenType.DecimalLiteral)
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenType == TokenType.StringLiteral)
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenType == TokenType.CharacterLiteral)
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenType == TokenType.FloatLiteral)
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenValue == "true")
                return new HelperAst(TokenStream.Consume());
            if (TokenStream.Current.TokenValue == "false")
                return new HelperAst(TokenStream.Consume());
            return null;
        }

        Ast ParseFunctionCall()
        {
            return MatchSequence(
                    typeof(FunctionCallAst),
                    Keep(Match(TokenType.Identifier)),
                    Keep(Match("(")),
                    Keep(ParseExpressionList0),
                    Keep(Match(")"))
                );
        }

        #endregion

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
                if (!(ast is HelperAst))
                    kept.Add(ast);
            }
            PostCheck(matched);
            if (matched)
            {
                var ast = (Ast)Activator.CreateInstance(astType);
                ast.Children.AddRange(kept);
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
            return () =>
            {
                var ast = func();
                if (ast == null)
                    return null; // nothing to see here
                // note - all nodes kept, except HelperAst nodes, unless they are marked
                // so those we need to mark specially
                if (ast is HelperAst)
                    (ast as HelperAst).Keep = true;
                return ast;
            };
        }

        ParseDelegate ZeroOrOne(ParseDelegate func)
        {
            return () =>
            {
                var asts = new List<Ast>();
                var ast = func();
                while (ast != null)
                {
                    asts.Add(ast);
                    ast = func();
                }
                if (asts.Count > 1)
                    return null; // too many
                return new HelperAst(asts);
            };
        }

        Ast AlwaysMatch()
        {
            return new HelperAst();
        }

        ParseDelegate ZeroOrMore(ParseDelegate func)
        {
            return () =>
            {
                var asts = new List<Ast>();
                var ast = func();
                while (ast != null)
                {
                    asts.Add(ast);
                    ast = func();
                }
                return new HelperAst(asts);
            };
        }

        ParseDelegate OneOrMore(ParseDelegate func)
        {
            return () =>
            {
                var asts = new List<Ast>();
                var ast = func();
                while (ast != null)
                {
                    asts.Add(ast);
                    ast = func();
                }
                if (!asts.Any())
                    return null; // failed
                return new HelperAst(asts);
            };
        }

        ParseDelegate OneOf(params string[] matchStrings)
        {
            return () =>
            {
                foreach (var match in matchStrings)
                    if (TokenStream.Current.TokenValue == match)
                    {
                        var h = new HelperAst(TokenStream.Current);
                        TokenStream.Consume();
                        return h;
                    }
                return null;
            };
        }

        ParseDelegate Match(string match)
        {
            return () =>
            {
                if (TokenStream.Current.TokenValue == match)
                {
                    var h = new HelperAst(TokenStream.Current);
                    TokenStream.Consume();
                    return h;
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
                    var h = new HelperAst(TokenStream.Current);
                    TokenStream.Consume();
                    return h;
                }
                return null;
            };
        }


        internal class HelperAst : Ast
        {
            public bool Keep { get; set; }

            // needs parameterless or exception on reflected construction
            public HelperAst()
            {

            }


            public HelperAst(Token token)
            {
                Token = token;
            }
            public HelperAst(List<Ast> asts = null)
            {
                if (asts != null)
                    Children.AddRange(asts);
            }
        }

        #endregion

        public List<Token> GetTokens()
        {
            return TokenStream.GetTokens();
        }
    }
}

