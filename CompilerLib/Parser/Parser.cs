﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Lexer;

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
            ignoreErrors.Clear();
            ignoreErrors.Push(false);
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
            if (TokenStream.Current.TokenType == TokenType.EndOfFile)
                return null;

                // todo - rewite using lookahead for prediction - 
                // avoid delegate passing for performance and ease of portability to C/C++
                if (Lookahead(TokenType.Import,TokenType.StringLiteral))
                return ParseImportDeclaration();
            else if (Lookahead(TokenType.Module))
                return ParseModuleDeclaration();
            else if (Lookahead(TokenType.LSquareBracket))
                return ParseAttributeDeclaration();
            else if (Lookahead(TokenType.Enum))
                return ParseEnumDeclaration();
            else if (Lookahead(TokenType.Type))
                return ParseTypeDeclaration();

            // var and func can have optional import or export, and var can have const
            var importToken = TryMatch(TokenType.Import);
            var exportToken = TryMatch(TokenType.Export);
            var constToken = TryMatch(TokenType.Const);

            if (!Lookahead(TokenType.OpenParen))
                return ParseVariableDefinition(importToken, exportToken, constToken);
            if (constToken != null)
            {
                ErrorMessage("Unknown 'const' token");
                return null;
            }
            return ParseFunctionDeclaration(importToken, exportToken);

            //return MatchOr(
                //ParseImportDeclaration,
                //ParseModuleDeclaration,
                //ParseAttributeDeclaration,
                //ParseEnumDeclaration,
                //ParseTypeDeclaration,
                //ParseVariableDeclaration,
                //ParseFunctionDeclaration
            //);
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

        Ast ParseImportDeclaration()
        {
            return MatchSequence2(
                typeof(ImportAst),
                Match2(TokenType.Import, "Missing 'import' token"),
                KeepNext(),
                Match2(TokenType.StringLiteral, "Missing import string"),
                Match2(TokenType.EndOfLine, "Missing EOL")
            );
        }

        Ast ParseModuleDeclaration()
        {
            return MatchSequence2(
                typeof(ModuleAst),
                Match2(TokenType.Module, "Missing 'module' token"),
                KeepNext(),
                Match2(TokenType.Identifier, "Missing module identifier"),
                Match2(TokenType.EndOfLine, "Missing EOL")
            );
        }

        Ast ParseAttributeDeclaration()
        {
            if (Match2(TokenType.LSquareBracket, "Attribute expected '['") != ParseAction.Matched)
                return null;

            if (!Lookahead(TokenType.Identifier))
            {
                ErrorMessage("Attribute expected an identifier");
                return null;
            }
            var id = TokenStream.Consume();

            var ast = ParseList<AttributeAst>(TokenType.StringLiteral, "Attribute expected string literal", 0, (a, n) => n.AddChild(a), TokenType.None);
            if (ast == null)
                return null;
            ast.Token = id;

            if (Match2(TokenType.RSquareBracket, "Attribute expected ']'") != ParseAction.Matched)
                return null;

            return ast;

//            return MatchSequence(
//                typeof(AttributeAst),
//                Match(TokenType.LSquareBracket),
//                Keep(Match(TokenType.Identifier)),
//                Keep(ZeroOrMore(Match(TokenType.StringLiteral))),
//                Match(TokenType.LSquareBracket),
//                Match(TokenType.EndOfLine)
//                );
        }

        Ast ParseEnumDeclaration()
        {
            var ast = MatchSequence2(
                typeof(EnumAst),
                Match2(TokenType.Enum, "Missing 'enum' token"),
                KeepNext(),
                Match2(TokenType.Identifier, "Missing enum identifier"),
                Match2(TokenType.EndOfLine, "Missing EOL"));
            if (ast == null)
                return null; // nothing more to do
            MatchZeroOrMore2(TokenType.EndOfLine);

            if (Match2(TokenType.Indent,"enum missing values") == ParseAction.NotMatched)
                return null;
            if (OneOrMore2(ast, ParseEnumValue, "enum missing values") == ParseAction.NotMatched)
                return null;
            if (Match2(TokenType.Undent, "enum values don't end") == ParseAction.NotMatched)
                return null;

            return ast;

//            return MatchSequence(
//                typeof(EnumAst),
//                Match(TokenType.Enum),
//                Keep(Match(TokenType.Identifier)),
//                OneOrMore(Match(TokenType.EndOfLine)),
//                Match(TokenType.Indent),
//                Keep(OneOrMore(ParseEnumValue)),
//                Match(TokenType.Undent)
//            );
        }

        Ast ParseEnumValue()
        {
            if (Lookahead(TokenType.Identifier, TokenType.Equals))
            {
                var id = TokenStream.Consume();
                TokenStream.Consume(); // '='
                var ast = ParseExpression();
                if (ast == null) return null;
                if (MatchOneOrMore2(TokenType.EndOfLine, "enum value missing EOL") != ParseAction.Matched)
                    return null;
                return Make2(typeof(EnumValueAst), id, ast);
            }
            else if (Lookahead(TokenType.Identifier))
            {
                var id = TokenStream.Consume();
                if (MatchOneOrMore2(TokenType.EndOfLine, "enum value missing EOL") != ParseAction.Matched)
                    return null;
                return Make2(typeof(EnumValueAst), id);
            }
            ErrorMessage("Cannot parse enum value");
            return null;
            //return MatchOr(
            //        ()=> MatchSequence(
            //            typeof (EnumValueAst),
            //            Keep(Match(TokenType.Identifier)),
            //            OneOrMore(Match(TokenType.EndOfLine))
            //            ),
            //        () => MatchSequence(
            //            typeof (EnumValueAst),
            //            Keep(Match(TokenType.Identifier)),
            //            Match("="), 
            //            Keep(ParseExpression),
            //            OneOrMore(Match(TokenType.EndOfLine))
            //            )
            //        );
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
                return new TypeAst(TokenStream.Consume());
            return null;
        }

        Ast ParseTypeDeclaration()
        {
            if (Match2(TokenType.Type, "Missing 'type' token") != ParseAction.Matched)
                return null;
            if (!Lookahead(TokenType.Identifier))
            {
                ErrorMessage("Missing type identifier");
                return null;
            }
            var idToken = TokenStream.Consume();
            if (MatchOneOrMore2(TokenType.EndOfLine,"Expected end of line(s)")!= ParseAction.Matched)
                return null;

            if (Match2(TokenType.Indent, "type missing values") == ParseAction.NotMatched)
                return null;

            var memberAsts = new List<Ast>();

            while (true)
            {
                var ast = TryParse(ParseVariableTypeAndNames);
                if (ast == null)
                    break;
                MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s)");
                memberAsts.Add(ast);
            }

            if (Match2(TokenType.Undent, "type values don't end") == ParseAction.NotMatched)
                return null;

            if (!memberAsts.Any())
            {
                ErrorMessage("Expected type members");
                return null;
            }
            
            return Make2(typeof(TypeDeclarationAst), idToken, memberAsts.ToArray());


//            var ast = MatchSequence2(
//                typeof(TypeDeclarationAst),
//                Match2(TokenType.Type, "Missing 'type' token"),
//                KeepNext(),
//                Match2(TokenType.Identifier, "Missing type identifier"),
//                Match2(TokenType.EndOfLine, "Missing EOL"));
//            if (ast == null)
//                return null; // nothing more to do
//            MatchZeroOrMore2(TokenType.EndOfLine);
//
//            if (Match2(TokenType.Indent, "type missing values") == ParseAction.NotMatched)
//                return null;
//            if (OneOrMore2(ast, ParseTypeMemberDefinitions, "type missing values") == ParseAction.NotMatched)
//                return null;
//            if (Match2(TokenType.Undent, "type values don't end") == ParseAction.NotMatched)
//                return null;
//
//            return ast;


//            return MatchSequence(
//                typeof(TypeDeclarationAst),
//                Match("type"),
//                Keep(Match(TokenType.Identifier)),
//                ParseEoLs,
//                Match(TokenType.Indent),
//                Keep(ParseTypeMemberDefinitions),
//                Match(TokenType.Undent)
//                );
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

#if false
        Ast ParseVariableDeclaration()
        {
#if true

            Ast idsAst, initAst = null;
            var typeAst = ParseType();
            if (typeAst == null)
                return null;
            idsAst = ParseIDList();
            if (idsAst == null)
            {
                ErrorMessage("Missing variable names for variable declaration");
                return null;
            }

            if (importToken == null && Lookahead(TokenType.Equals))
            {
                TokenStream.Consume();
                todo
                initAst = ParseExpressionList1();
            }

            var varDeclAst = (VariableDefinitionAst)Make2(typeof(VariableDefinitionAst), typeAst.Token, idsAst, initAst);
            return varDeclAst;


#else
            return MatchOr(
                () =>MatchSequence(
                    typeof(VariableDefinitionAst),
#if false
                    Keep(ParseImportVariable),
#else
                    Keep(Match("import")),
                    Keep(ParseOptionalConst),
                    Keep(ParseType),
                    Keep(ParseIDList),

#endif
                    Match(TokenType.EndOfLine)
                    ),
                () => MatchSequence(
                    typeof(VariableDefinitionAst),
#if false
                    Keep(ParseNormalVariable),
#else
                    Keep(ParseOptionalExport),
                    Keep(ParseOptionalConst),
                    Keep(ParseType),
                    Keep(ParseIDList),
                    Keep(ParseVariableInitializer),

#endif
                    Match(TokenType.EndOfLine)
                    )
            );
#endif
        }
        Ast ParseImportVariable()
        {
            return MatchSequence(
                typeof(VariableDefinitionAst),
                Keep(Match("import")),
                Keep(ParseOptionalConst),
                Keep(ParseVariableTypeAndNames)
            );
        }

        Ast ParseNormalVariable()
        {
            return MatchSequence(
                typeof(VariableDefinitionAst),
                Keep(ParseOptionalExport),
                Keep(ParseOptionalConst),
                Keep(ParseVariableDefinition)
            );
        }
#endif

        Ast ParseVariableDefinition(Token importToken, Token exportToken, Token constToken)
        {
            if (importToken != null && exportToken != null)
            {
                ErrorMessage("Variable declaration cannot have both import and export");
                return null;
            }

            var typeAndNames = ParseVariableTypeAndNames();
            if (typeAndNames == null)
            {
                ErrorMessage("Expected variable type and names");
                return null;
            }

            Ast va = null;
            if (Lookahead(TokenType.Equals))
            {
                TokenStream.Consume();
                va = ParseExpressionList1();
                if (va == null)
                {
                    ErrorMessage("Expected variable initializer");
                    return null;
                }
            }

            if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;
            var vd = new VariableDefinitionAst();
            vd.AddChild(typeAndNames);
            vd.AddChild(va);
            vd.ImportToken = importToken;
            vd.ExportToken = exportToken;
            vd.ConstToken = constToken;
            return vd;

//            return MatchSequence(
//                typeof(HelperAst),
//                Keep(ParseVariableTypeAndNames),
//                Keep(ParseVariableInitializer)
//                );
        }

        Ast ParseVariableTypeAndNames()
        {
            var typeAst = ParseType();
            if (typeAst == null)
            {
                ErrorMessage("Expected type");
                return null;
            }
            var idList = ParseIDList();
            if (idList == null)
                return null;
            return Make2(typeof(VariableTypeAndNamesAst),typeAst.Token,idList);


//            return MatchSequence(
//                typeof(VariableTypeAndNamesAst),
//                Keep(ParseType),
//                Keep(ParseIDList)
//                );
        }

        Ast ParseIDList()
        {
#if true
            var idAsts = new List<Ast>();
            while (true)
            {
                var token = TokenStream.Consume();
                if (token.TokenType != TokenType.Identifier)
                {
                    ErrorMessage("Invalid token in identifier list:");
                    return null;
                }
                idAsts.Add(new HelperAst(token));

                if (Lookahead(TokenType.LSquareBracket))
                {
                    // parse array node, make as child
                    TokenStream.Consume(); // '['
                    var ast = ParseExpressionList1();
                    Match2(TokenType.RSquareBracket, "variable declaration missing closing ']'");
                    idAsts.Last().AddChild(ast);
                }

                if (!Lookahead(TokenType.Comma))
                    break; // done
                TokenStream.Consume();
            } 
            return Make2(typeof(TypeMemberDefinitionAst), null, idAsts.ToArray());

#else
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
#endif
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


        Ast ParseInitializerList()
        {
            return ParseExpressionList1();
        }

#region Function parsing statements

        Ast ParseFunctionDeclaration(Token importToken, Token exportToken)
        {
            if (importToken != null && exportToken != null)
            {
                ErrorMessage("Function declaration cannot have both import and export");
                return null;
            }

            // prototype (ret vals) func name (params)
            if (Match2(TokenType.OpenParen,"function expected return values in '(' and ')'") != ParseAction.Matched)
                return null;
            var returnTypes = ParseList<ReturnValuesAst>(ParseType, "Expected type", 0, (a,n)=>n.Types.Add(a.Token), TokenType.Comma); 
            if (Match2(TokenType.CloseParen, "function expected return values in '(' and ')'") != ParseAction.Matched)
                return null;

            if (!Lookahead(TokenType.Identifier))
            {
                ErrorMessage("Expected identifier as function name");
                return null;
            }

            var name = ParseFunctionName();
            if (name == null)
            {
                ErrorMessage("Expected function name");
                return null;
            }
            var idToken = name.Token;

            if (Match2(TokenType.OpenParen, "function expected parameter values in '(' and ')'") != ParseAction.Matched)
                return null;
            var parameters = ParseList<ParameterListAst>(ParseParameter, "Expected parameter", 0, (a,n)=>n.AddChild(a),TokenType.Comma);
            if (Match2(TokenType.CloseParen, "function expected parameter values in '(' and ')'") != ParseAction.Matched)
                return null;

            if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;
            Ast block  = null;
            if (importToken == null)
                block = ParseBlock();

            var funcDecl = (FunctionDeclarationAst)Make2(typeof(FunctionDeclarationAst), idToken, returnTypes,parameters, block);

            funcDecl.ImportToken = importToken;
            funcDecl.ExportToken = exportToken;
            return funcDecl;

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

        // generic list parsing function
        // parses items, with optional delimiter tokens
        // TokenType none means no delimiter
        Ast ParseList<T>(
            TokenType matchToken, 
            string errorMessage, int minItemCount, Action<Ast, T> storeItem,
            TokenType delimiter
            ) where T : Ast, new()
        {
            return ParseList<T>(
                Match(matchToken),
                errorMessage,minItemCount,storeItem,delimiter
            );
        }

        // generic list parsing function
        // parses items, with optional delimiter tokens
        // TokenType none means no delimiter
        Ast ParseList<T>(
            ParseDelegate itemFunc, 
            string errorMessage, 
            int minItemCount, 
            Action<Ast,T> storeItem, 
            TokenType delimiter
            ) where T : Ast, new()
        {
            var items = new List<Ast>();

            var item = TryParse(itemFunc);
            while (item != null)
            {
                items.Add(item);
                if (delimiter != TokenType.None)
                { // check delimiter
                    if (!Lookahead(delimiter))
                        break;
                    TokenStream.Consume();
                }
                item = TryParse(itemFunc);
                if (item == null && delimiter != TokenType.None)
                {
                    // was a delimiter, needs to be another item
                    ErrorMessage(errorMessage);
                    return null;
                }
            }
            if (items.Count < minItemCount)
            {
                ErrorMessage(errorMessage);
                return null;
            }
            var h = new T();
            foreach (var t in items)
                storeItem(t,h);
            return h;
        }


        Ast ParseParameter()
        {
            var type = ParseType();
            if (type == null)
            {
                ErrorMessage("Expected type for parameter");
                return null;
            }
            
            // todo - add optional address here for ref vars
            // Keep(OneOf("&","")), // optional reference

            if (!Lookahead(TokenType.Identifier))
            {
                ErrorMessage("Expected identifier after parameter type");
                return null;
            }
            var id = TokenStream.Consume();
            var arrDepth = ParseOptionalEmptyArrayDepth();
            if (arrDepth < 0)
            {
                ErrorMessage("Error in array parsing");
                return null;
            }

            return new ParameterAst(type.Token,id, arrDepth);


//            return MatchSequence(
//                typeof(HelperAst),
//                Keep(ParseType),
//                // Keep(OneOf("&","")), // optional reference - todo parse fails
//                Keep(Match(TokenType.Identifier)),
//                Keep(ParseOptionalEmptyArray)
//                );
        }

        // return array count, or -1 on error
        int ParseOptionalEmptyArrayDepth()
        {
            var count = 0;
            if (TokenStream.Current.TokenType == TokenType.LSquareBracket)
            {
                TokenStream.Consume();
                count = 1;
                while (Lookahead(TokenType.Comma))
                {
                    TokenStream.Consume();
                    count++;
                }
                if (TokenStream.Current.TokenType == TokenType.RSquareBracket)
                    TokenStream.Consume();
                else
                {
                    ErrorMessage("Array closing required ']'");
                    return -1;
                }
            }
            return count;
        }

        Ast ParseStatement()
        {
            if (Lookahead(TokenType.If))
                return ParseIfStatement();
            if (Lookahead(TokenType.For))
                return ParseForStatement();
            if (Lookahead(TokenType.While))
                return ParseWhileStatement();
            if (Lookahead(TokenType.Identifier, TokenType.OpenParen))
            {
                var ast = ParseFunctionCall();
                if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                    return null;
                return ast;
            }
            if (IsJumpToken())
                return ParseJumpStatement();

            var vd = TryParse(()=>ParseVariableDefinition(null,null,null));
            if (vd != null) return vd;
            return ParseAssignStatement();

            //return MatchOr(
                //()=>MatchSequence(
                //    typeof(VariableDefinitionAst),
                //    Keep(ParseVariableDefinition),
                //    ParseEoLs
                //    ),
                //() => MatchSequence(
                //    typeof(AssignStatementAst),
                //    Keep(ParseAssignStatement),
                //    ParseEoLs
                //    ),
                //() => MatchSequence(
                //    typeof(IfStatementAst),
                //    Keep(ParseIfStatement)
                //    ),
                //() => MatchSequence(
                //    typeof(ForStatementAst),
                //    Keep(ParseForStatement)
                //    ),
                //() => MatchSequence(
                //    typeof(WhileStatementAst),
                //    Keep(ParseWhileStatement)
                //    ),
                //() => MatchSequence(
                //    typeof(FunctionCallAst),
                //    Keep(ParseFunctionCall),
                //    ParseEoLs
                //    ),
                //() => MatchSequence(
                //    typeof(JumpStatementAst),
                //    Keep(ParseJumpStatement),
                //    ParseEoLs
                //    )
                //);
        }

        static string[] assignOperators = {"=","+=","-=","*=","/=","^=","&=","|=","%=",">>=","<<=",">>>=","<<<="};
        Ast ParseAssignStatement()
        {
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseAssignList),
                    Keep(OneOf(assignOperators)),
                    Keep(ParseExpressionList1),
                    ParseEoLs
                    ),
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseAssignList),
                    Keep(OneOf("++","--")),
                    ParseEoLs
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

        // return true if token is a jump statement
        bool IsJumpToken()
        {
            var t = TokenStream.Current.TokenValue;
            return t == "break" || t == "continue" || t == "return";
        }

        Ast ParseJumpStatement()
        {
            if (IsJumpToken())
            {
                var t = TokenStream.Current.TokenValue;
                var h = new HelperAst(TokenStream.Consume());
                if (t == "return")
                {
                    var retval = ParseExpressionList0();
                    if (retval != null)
                        h.Children.Add(retval);
                }
                if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                    return null;
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
            if (Match2(TokenType.Indent,"Expected indented block") != ParseAction.Matched)
                return null;

            var block = ParseList<BlockAst>(ParseStatement, "Expected statement", 1, (a, n) => n.AddChild(a), TokenType.None);
            if (block == null)
            {
                ErrorMessage("Indented block parse failed");
                return null;
            }
            // eat any extra end of lines
            MatchZeroOrMore2(TokenType.EndOfLine);

            if (Match2(TokenType.Undent, "Expected indented block to end") != ParseAction.Matched)
                return null;
            return block;

//            return MatchSequence(
//                typeof(BlockAst),
//                Match(TokenType.Indent),
//                Keep(ZeroOrMore(() => ParseStatement())),
//                Match(TokenType.Undent)
//            );
        }

#endregion

        // zero or more expressions, comma separated
        Ast ParseExpressionList0()
        {
            return ZeroOrOne(
                ParseExpressionList1
            )();
        }

        // one or more expressions, comma separated
        Ast ParseExpressionList1()
        {
#if false
            var exprs = new List<Ast>();
            while (true)
            {
                var ast = ParseExpression();
                exprs.Add(ast);
                if (!Lookahead(TokenType.Comma))
                    break;
                TokenStream.Consume();
            }
            return Make2(typeof(ExpressionListAst),null,exprs.ToArray());
#else
            return MatchOr(
                ()=>MatchSequence(
                    typeof(HelperAst),
                    Keep(ParseExpression),
                    Match(","),
                    Keep(ParseExpressionList1)
                    ),
                Keep(ParseExpression)
                );
#endif
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                        typeof(ExpressionAst),
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
                    typeof(ExpressionAst),
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
                            typeof(ExpressionAst),
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
            ignoreErrors.Push(true);
        }

        void PostCheck(bool commit)
        {
            if (!commit)
                TokenStream.RollbackSnapshot();
            else
                TokenStream.CommitSnapshot();
            ignoreErrors.Pop();
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

        // return true if the next lookahead has the following tokens, else false
        // eats no tokens
        bool Lookahead(params TokenType[] tokenTypes)
        {
            PreCheck();
            var matches = true;
            for (var i =0; i < tokenTypes.Length && matches; ++i)
            {
                var t = tokenTypes[i];
                matches &= t == TokenStream.Current.TokenType;
                TokenStream.Consume();
            }
            PostCheck(false);
            return matches;
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

#region New helpers - error methods
        // match sequence of things, if all match, create type, and 
        // populate with items marked with keep
        Ast MatchSequence2(Type astType, params ParseAction [] parseActions)
        {
            var matched = true;
            var kept = new List<Token>();
            for (var i = 0; i < parseActions.Length; ++i)
            {
                var action = parseActions[i];
                switch (action)
                {
                    case ParseAction.Matched:
                        break;
                    case ParseAction.NotMatched:
                        matched = false;
                        break;
                    case ParseAction.KeepNext:
                        kept.Add(TokenStream.Peek(-(parseActions.Length-1-i)));
                        break;
                    default: throw new InternalFailure($"Unknown parse action {action}");
                }
            }

            if (matched)
            {
                var ast = (Ast)Activator.CreateInstance(astType);
                if (kept.Count > 1)
                    throw new InternalFailure($"MatchSequence2 Only allows 0 or 1 kept items!");
                if (kept.Any())
                    ast.Token = kept[0];
                return ast;
            }
            return null;
        }

        enum ParseAction
        {
            Matched,
            NotMatched,
            KeepNext
        }

        // match next token, show error if present on mismatch, eat token
        // return Matched on match, else NotMatched
        ParseAction Match2(TokenType tokenType, string errorMessage)
        {
            if (TokenStream.Consume().TokenType == tokenType)
                return ParseAction.Matched;
            if (!String.IsNullOrEmpty(errorMessage))
                ErrorMessage(errorMessage);
            return ParseAction.NotMatched;
        }

        ParseAction KeepNext()
        {
            return ParseAction.KeepNext;
        }

        Stack<bool> ignoreErrors = new Stack<bool>();
        // write error message out and current token (position, line)
        void ErrorMessage(string error)
        {
            if (!ignoreErrors.Peek())
                environment.Output.WriteLine($"ERROR: {error} at {TokenStream.Peek(-1)}");
        }

        // while next token matches, eat them
        // return Matched
        ParseAction MatchZeroOrMore2(TokenType tokenType)
        {
            while (TokenStream.Current.TokenType == tokenType)
                TokenStream.Consume();
            return ParseAction.Matched;
        }
        // while next token matches, eat them
        // return Matched if at least one
        ParseAction MatchOneOrMore2(TokenType tokenType, string errorString)
        {
            var matched = TokenStream.Current.TokenType == tokenType;
            while (TokenStream.Current.TokenType == tokenType)
                TokenStream.Consume();
            if (matched)
                return ParseAction.Matched;
            ErrorMessage(errorString);
            return ParseAction.NotMatched;
        }

        // parse one or more of the following, add to parent if not null.
        // if none present, show error message
        // return Matched if some present, else NotMatched
        ParseAction OneOrMore2(Ast parent, ParseDelegate parseFunc, string errorMessage)
        {
            Ast ast = null;
            var someSeen = false;
            do
            {
                PreCheck();
                ast = parseFunc();
                PostCheck(ast != null);
                if (ast != null)
                {
                    someSeen = true;
                    parent?.Children.Add(ast);
                }
            } while (ast != null);
            if (someSeen)
                return ParseAction.Matched;
            ErrorMessage(errorMessage);
            return ParseAction.NotMatched;
        }

        // make an ast node of the given type, set the token, and add any children
        Ast Make2(Type astType, Token token, params Ast[] children)
        {
            var ast = (Ast) Activator.CreateInstance(astType);
            ast.Token = token;
            foreach (var c in children)
            {
                if (c != null)
                    ast.Children.Add(c);
            }
            return ast;
        }


        // if next token is given kind, consume and return, else return null
        Token TryMatch(TokenType tokenType)
        {
            if (Lookahead(tokenType))
                return TokenStream.Consume();
            return null;
        }

#endregion


#endregion

        public List<Token> GetTokens()
        {
            return TokenStream.GetTokens();
        }
    }
}

