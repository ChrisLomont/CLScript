using System;
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

        public List<Token> GetTokens()
        {
            return TokenStream.GetTokens();
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
            if (Lookahead("",TokenType.Import, TokenType.StringLiteral))
                return ParseImportDeclaration();
            else if (Lookahead("",TokenType.Module))
                return ParseModuleDeclaration();
            else if (Lookahead("",TokenType.LSquareBracket))
                return ParseAttributeDeclaration();
            else if (Lookahead("",TokenType.Enum))
                return ParseEnumDeclaration();
            else if (Lookahead("",TokenType.Type))
                return ParseTypeDeclaration();

            // var and func can have optional import or export, and var can have const
            var importToken = TryMatch(TokenType.Import);
            var exportToken = TryMatch(TokenType.Export);
            var constToken = TryMatch(TokenType.Const);

            if (!Lookahead("",TokenType.OpenParen))
                return ParseVariableDefinition(importToken, exportToken, constToken);
            if (constToken != null)
            {
                ErrorMessage("Unknown 'const' token");
                return null;
            }
            return ParseFunctionDeclaration(importToken, exportToken);
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

            if (!Lookahead("Attribute expected an identifier",TokenType.Identifier))
                return null;
            var id = TokenStream.Consume();

            var ast = ParseList<AttributeAst>(TokenType.StringLiteral, "Attribute expected string literal", 0, (a, n) => n.AddChild(a), TokenType.None);
            if (ast == null)
                return null;
            ast.Token = id;

            if (Match2(TokenType.RSquareBracket, "Attribute expected ']'") != ParseAction.Matched)
                return null;

            return ast;
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
        }

        Ast ParseEnumValue()
        {
            // todo - make cleaner
            if (Lookahead("",TokenType.Identifier, TokenType.Equals))
            {
                var id = TokenStream.Consume();
                TokenStream.Consume(); // '='
                var ast = ParseExpression();
                if (ast == null) return null;
                if (MatchOneOrMore2(TokenType.EndOfLine, "enum value missing EOL") != ParseAction.Matched)
                    return null;
                return Make2(typeof(EnumValueAst), id, ast);
            }
            else if (Lookahead("",TokenType.Identifier))
            {
                var id = TokenStream.Consume();
                if (MatchOneOrMore2(TokenType.EndOfLine, "enum value missing EOL") != ParseAction.Matched)
                    return null;
                return Make2(typeof(EnumValueAst), id);
            }
            ErrorMessage("Cannot parse enum value");
            return null;
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
            if (!Lookahead("Missing type identifier",TokenType.Identifier))
                return null;
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


        }

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
            if (Lookahead("",TokenType.Equals))
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
        }

        Ast ParseVariableTypeAndNames()
        {
            var typeAst = ParseType();
            if (typeAst == null)
            {
                ErrorMessage("Expected type");
                return null;
            }
            var idList = ParseIdList();
            if (idList == null)
                return null;
            return Make2(typeof(VariableTypeAndNamesAst),typeAst.Token,idList);
        }

        Ast ParseIdList()
        {
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

                if (Lookahead("",TokenType.LSquareBracket))
                {
                    // parse array node, make as child
                    TokenStream.Consume(); // '['
                    var ast = ParseExpressionList1();
                    Match2(TokenType.RSquareBracket, "variable declaration missing closing ']'");
                    idAsts.Last().AddChild(ast);
                }

                if (!Lookahead("",TokenType.Comma))
                    break; // done
                TokenStream.Consume();
            } 
            return Make2(typeof(TypeMemberDefinitionAst), null, idAsts.ToArray());

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

            if (!Lookahead("Expected identifier as function name",TokenType.Identifier))
                return null;

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
                    if (!Lookahead("",delimiter))
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

            if (!Lookahead("Expected identifier after parameter type",TokenType.Identifier))
                return null;
            var id = TokenStream.Consume();
            var arrDepth = ParseOptionalEmptyArrayDepth();
            if (arrDepth < 0)
            {
                ErrorMessage("Error in array parsing");
                return null;
            }

            return new ParameterAst(type.Token,id, arrDepth);


        }

        // return array count, or -1 on error
        int ParseOptionalEmptyArrayDepth()
        {
            var count = 0;
            if (TokenStream.Current.TokenType == TokenType.LSquareBracket)
            {
                TokenStream.Consume();
                count = 1;
                while (Lookahead("",TokenType.Comma))
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
            if (Lookahead("",TokenType.If))
                return ParseIfStatement();
            if (Lookahead("",TokenType.For))
                return ParseForStatement();
            if (Lookahead("",TokenType.While))
                return ParseWhileStatement();
            if (Lookahead("",TokenType.Identifier, TokenType.OpenParen))
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

        }

        static string[] assignOperators = {"=","+=","-=","*=","/=","^=","&=","|=","%=",">>=","<<=",">>>=","<<<="};
        Ast ParseAssignStatement()
        {
            var assignItems = ParseList<HelperAst>(
                ParseAssignItem,"Expected assign item",1,
                (a,n)=>n.AddChild(a),
                TokenType.Comma
            );
                
            if (assignItems == null)
            {
                ErrorMessage("Expected assignment list");
                return null;
            }
            Token op = null;
            Ast exprList = null;
            if (NextIsOneOf(assignOperators))
            {
                op = TokenStream.Consume();
                exprList = ParseExpressionList1();
                if (exprList == null)
                {
                    ErrorMessage("Expected expression list for variable assignment");
                    return null;
                }
            }
            else if (NextIsOneOf("++", "--"))
            {
                op = TokenStream.Consume();
            }

            if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;

            var h = new HelperAst(op);
            h.AddChild(assignItems);
            if (exprList != null)
                h.AddChild(exprList);
            return h;
        }

        bool NextIsOneOf(params string [] items)
        {
            var tt = TokenStream.Current.TokenValue;
            foreach (var t in items)
                if (t == tt)
                    return true;
            return false;
        }

        Ast ParseAssignItem()
        {
            // ID, then possible array or dot. If dot, repeat
            // ID
            // ID[10,a]
            // ID[10].f
            // ID.a
            // ID[10,b].a[10]...
            var ast = new HelperAst();
            while (true)
            {
                if (!Lookahead("Expected identifier in assignment",TokenType.Identifier))
                    return null;
                var id = TokenStream.Consume();
                Ast array = null;
                if (Lookahead("",TokenType.LSquareBracket))
                {
                    TokenStream.Consume();
                    array = ParseExpressionList1();
                    if (array == null)
                    {
                        ErrorMessage("Expected expressions in array");
                        return null;
                    }
                    if (Match2(TokenType.RSquareBracket, "Missing array closure ']'") != ParseAction.Matched)
                        return null;
                }
                Token dot = null;
                if (Lookahead("",TokenType.Dot))
                    dot = TokenStream.Consume();

                // save any needed, then loop if necessary
                ast.AddChild(new HelperAst(id));
                if (array != null)
                    ast.AddChild(array);
                if (dot != null)
                    ast.AddChild(new HelperAst(dot));

                if (array == null && dot == null)
                    break;
            }
            return ast;
        }

        Ast ParseIfStatement()
        {
            // get sequence of expr,block for if/else if run, final odd block is final else
            var items = new List<Ast>();

            while (true)
            {

                if (Match2(TokenType.If, "Expected 'if' statement") != ParseAction.Matched)
                    return null;
                var expr = ParseExpression();
                if (expr == null)
                {
                    ErrorMessage("Expected expression after the 'if'");
                    return null;
                }
                if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s) after 'if' expression") !=
                    ParseAction.Matched)
                    return null;
                var block = ParseBlock();
                if (block == null)
                {
                    ErrorMessage("Expected block for 'if' statement");
                    return null;
                }

                items.Add(expr);
                items.Add(block);

                // if there is an 'else if' loop more, else break
                if (!Lookahead("",TokenType.Else, TokenType.If))
                    break;

                TokenStream.Consume(); // consume 'else', go to top for 'if'
            }

            Ast finalBlock = null;
            if (Lookahead("",TokenType.Else))
            {
                TokenStream.Consume(); // else
                if (MatchOneOrMore2(TokenType.EndOfLine,"Expected end of line(s) after 'else'") != ParseAction.Matched)
                    return null;
                finalBlock = ParseBlock();
                if (finalBlock == null)
                {
                    ErrorMessage("Expected final block after 'else'");
                    return null;
                }
            }

            var ifAst = new IfStatementAst();
            ifAst.Children.AddRange(items);
            if (finalBlock != null)
                ifAst.AddChild(finalBlock);
            return ifAst;
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
            if (Match2(TokenType.For, "Expected 'for'") != ParseAction.Matched)
                return null;
            if (!Lookahead("Expected identifier after 'for'",TokenType.Identifier))
                return null;
            var id = TokenStream.Consume();
            if (Match2(TokenType.In, "Expected 'in' in 'for' statement after identifier") != ParseAction.Matched)
                return null;
            // next comes either an array expression, or 2 or 3 expressions comma delimited.
            // to decide, which comes first, ',' or end of line?

            var peekIndex = 0;
            var eolFirst = false;
            while (true)
            {
                var tt = TokenStream.Peek(peekIndex).TokenType;
                if (tt == TokenType.Comma)
                    break;
                if (tt == TokenType.EndOfLine)
                {
                    eolFirst = true;
                    break;
                }
                peekIndex++;
            }

            Ast expr = null;
            if (eolFirst)
                expr = ParseAssignItem();
            else
                expr = ParseList<HelperAst>(ParseExpression, "Expected expression", 2, (a, n) => n.Children.Add(a), TokenType.Comma);
            if (expr == null)
            {
                ErrorMessage("Invalid 'for' limits");
                return null;
            }

            if (expr.Children.Count > 3)
            {
                ErrorMessage("Too many expressions in 'for' limits");
                return null;
            }

            var forAst = new ForStatementAst {Token = id};
            forAst.Children.Add(expr);
            return forAst;
        }

        Ast ParseWhileStatement()
        {
            if (Match2(TokenType.While, "Expected 'while'") != ParseAction.Matched)
                return null;
            var expr = ParseExpression();
            if (expr == null)
            {
                ErrorMessage("Expected expression following 'while'");
                return null;
            }
            if (MatchOneOrMore2(TokenType.EndOfLine, "Expected end of line(s) after 'while' expression") !=
                ParseAction.Matched)
                return null;
            var block = ParseBlock();
            if (block == null)
            {
                ErrorMessage("Expected block after 'while'");
                return null;
            }
            var whileStatement = new WhileStatementAst();
            whileStatement.Children.Add(expr);
            whileStatement.Children.Add(block);
            return whileStatement;
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
        }

        #endregion

        // zero or more expressions, comma separated
        // todo - remove this? merge them?
        Ast ParseExpressionList0()
        {
            return ParseList<ExpressionAst>(ParseExpression, "Expected expression", 0, (a, n) => n.AddChild(a), TokenType.Comma);
        }

        // one or more expressions, comma separated
        // todo - remove this? merge them?
        Ast ParseExpressionList1()
        {
            return ParseList<ExpressionAst>(ParseExpression, "Expected expression", 1, (a, n) => n.AddChild(a), TokenType.Comma);
        }

#region Parse Expression

        Ast ParseExpression()
        {
            return ParseLogicalOrExpression();
        }

        bool NextTokenOneOf(params TokenType[] tokenTypes)
        {
            var matched = false;
            foreach (var tokenType in tokenTypes)
                matched |= Lookahead("", tokenType);
            return matched;
        }

        Ast ExpressionHelper(
            ParseDelegate leftFunc, 
            string leftMessage, 
            ParseDelegate rightFunc, 
            string rightMessage, 
            params TokenType [] midTokens)
        {
            var ast = leftFunc();
            if (ast == null)
            {
                ErrorMessage($"Expected {leftMessage} expression");
                return null;
            }

            // see if lookahead is ok
            var matched = NextTokenOneOf(midTokens);

            if (matched)
            {
                var token = TokenStream.Consume();
                var rightAst = rightFunc();
                if (rightAst == null)
                {
                    ErrorMessage($"Expected {rightMessage} expression");
                    return null;
                }

                var leftAst = ast;
                ast = new ExpressionAst {Token = token};
                ast.AddChild(leftAst);
                ast.AddChild(rightAst);
            }
            return ast;

        }

        Ast ParseLogicalOrExpression()
        {
            return ExpressionHelper(
                ParseLogicalAndExpression,
                "logical AND",
                ParseLogicalOrExpression,
                "logical OR", TokenType.LogicalOr);
//            return
//                MatchOr(
//                    () => MatchSequence(
//                        typeof(ExpressionAst),
//                        Keep(ParseLogicalANDExpression),
//                        Keep(Match("||")),
//                        Keep(ParseLogicalORExpression)
//                    ),
//                    ParseLogicalANDExpression
//                );
        }

        Ast ParseLogicalAndExpression()
        {
            return ExpressionHelper(
                ParseInclusiveOrExpression,
                "inclusive OR",
                ParseLogicalAndExpression,
                "logical AND", TokenType.LogicalAnd);
//            return
//                MatchOr(
//                    () => MatchSequence(
//                        typeof(ExpressionAst),
//                        Keep(ParseInclusiveOrExpression),
//                        Keep(Match("&&")),
//                        Keep(ParseLogicalANDExpression)
//                    ),
//                    ParseInclusiveOrExpression
//                );
        }
        Ast ParseInclusiveOrExpression()
        {
            return ExpressionHelper(
                ParseExclusiveOrExpression,
                "exclusive OR",
                ParseInclusiveOrExpression,
                "inclusive OR", TokenType.Pipe);
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
            return ExpressionHelper(
                ParseAndExpression,
                "AND",
                ParseExclusiveOrExpression,
                "exclusive OR", TokenType.Caret);
            return
                MatchOr(
                    () => MatchSequence(
                        typeof(ExpressionAst),
                        Keep(ParseAndExpression),
                        Keep(Match("^")),
                        Keep(ParseExclusiveOrExpression)
                    ),
                    ParseAndExpression
                );
        }
        Ast ParseAndExpression()
        {
            return ExpressionHelper(
                EqualityExpression,
                "equality",
                ParseAndExpression,
                "AND", TokenType.Ampersand);
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
            return ExpressionHelper(
                RelationalExpression,
                "relational",
                EqualityExpression,
                "equality", 
                TokenType.Equals,TokenType.NotEqual
                );
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
            return ExpressionHelper(
                ShiftExpression,
                "shift",
                RelationalExpression,
                "relational",
                TokenType.LessThanOrEqual, TokenType.GreaterThanOrEqual,TokenType.LessThan,TokenType.GreaterThan
                );
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
            return ExpressionHelper(
                RotateExpression,
                "rotate",
                ShiftExpression,
                "shift",
                TokenType.LeftShift, TokenType.RightShift
                );
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
            return ExpressionHelper(
                AdditiveExpression,
                "additive",
                RotateExpression,
                "rotate",
                TokenType.LeftRotate, TokenType.RightRotate
                );
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
            return ExpressionHelper(
                MultiplicativeExpression,
                "multiplicative",
                AdditiveExpression,
                "additive",
                TokenType.Plus, TokenType.Minus
                );
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
            return ExpressionHelper(
                UnaryExpression,
                "unary",
                MultiplicativeExpression,
                "multiplicative",
                TokenType.Asterix, TokenType.Slash,TokenType.Percent
                );
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
            if (NextTokenOneOf(TokenType.Increment,TokenType.Decrement,TokenType.Plus,TokenType.Minus,TokenType.Tilde,TokenType.Exclamation))
            {
                var token = TokenStream.Consume();
                var ast = UnaryExpression();
                if (ast == null)
                {
                    return null;
                }
                if (ast.Token != null)
                    throw new InternalFailure("Token already set in UnaryExpression");
                ast.Token = token;
                return ast;
            }
            return PostfixExpression();

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
            todo
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
        // outputs error message if not empty or null and look fails
        bool Lookahead(string errorMessage, params TokenType[] tokenTypes)
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
            if (!matches && !String.IsNullOrEmpty(errorMessage))
                ErrorMessage(errorMessage);
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
            if (Lookahead("",tokenType))
                return TokenStream.Consume();
            return null;
        }

#endregion


#endregion

    }
}

