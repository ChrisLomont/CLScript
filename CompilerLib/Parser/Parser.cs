using System;
using System.Collections.Generic;
using System.Linq;
using Lomont.ClScript.CompilerLib.AST;

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

        public Parser(Environment environment, Lexer.Lexer lexer)
        {
            env = environment;
            TokenStream = new ParseableTokenStream(lexer);
        }

        public Ast Parse()
        {
            ignoreErrors.Clear();
            ignoreErrors.Push(false);
            var ast = ParseDeclarations(); // start here
            return ast;
        }

        public List<Token> GetTokens()
        {
            return TokenStream.GetTokens();
        }

        Environment env;

        #region Grammar

        // <Declarations> ::= <Declaration> <Declarations> | ! a file is a list of zero or more declarations
        // parsing starts here
        Ast ParseDeclarations()
        {
            var decls = new DeclarationsAst();

            Ast decl;
            do
            {
                decl = ParseDeclaration();
                if (decl != null)
                    decls.Children.Add(decl);
            } while (decl != null);
            if (TokenStream.Current.TokenType != TokenType.EndOfFile)
                env.Error($"Could not parse {TokenStream.Current}");
            return decls;
        }

        // a declaration is one of many items
        Ast ParseDeclaration()
        {
            // eat any extra end of line(s)
            while (TokenStream.Current.TokenType == TokenType.EndOfLine)
                TokenStream.Consume();
            if (TokenStream.Current.TokenType == TokenType.EndOfFile)
                return null;

            if (Lookahead("", TokenType.Import, TokenType.StringLiteral))
                return ParseImportDeclaration();
            else if (Lookahead("", TokenType.Module))
                return ParseModuleDeclaration();
            else if (Lookahead("", TokenType.LSquareBracket))
                return ParseAttributeDeclaration();
            else if (Lookahead("", TokenType.Enum))
                return ParseEnumDeclaration();
            else if (Lookahead("", TokenType.Type))
                return ParseTypeDeclaration();

            // var and func can have optional import or export, and var can have const
            var importToken = TryMatch(TokenType.Import);
            var exportToken = TryMatch(TokenType.Export);
            var constToken = TryMatch(TokenType.Const);

            if (!Lookahead("", TokenType.OpenParen))
                return ParseVariableDefinition(importToken, exportToken, constToken,true);
            if (constToken != null)
            {
                ErrorMessage("Unknown 'const' token");
                return null;
            }
            return ParseFunctionDeclaration(importToken, exportToken);
        }

        Ast ParseImportDeclaration()
        {
            return MatchSequence(
                typeof(ImportAst),
                Match(TokenType.Import, "Missing 'import' token"),
                KeepNext(),
                Match(TokenType.StringLiteral, "Missing import string"),
                Match(TokenType.EndOfLine, "Missing EOL")
            );
        }

        Ast ParseModuleDeclaration()
        {
            return MatchSequence(
                typeof(ModuleAst),
                Match(TokenType.Module, "Missing 'module' token"),
                KeepNext(),
                Match(TokenType.Identifier, "Missing module identifier"),
                Match(TokenType.EndOfLine, "Missing EOL")
            );
        }

        Ast ParseAttributeDeclaration()
        {
            if (Match(TokenType.LSquareBracket, "Attribute expected '['") != ParseAction.Matched)
                return null;

            if (!Lookahead("Attribute expected an identifier", TokenType.Identifier))
                return null;
            var id = TokenStream.Consume();

            var ast = ParseList<AttributeAst>(TokenType.StringLiteral, "Attribute expected string literal", 0,
                (a, n) => n.AddChild(new LiteralAst(a.Token)), TokenType.None);
            if (ast == null)
                return null;
            ast.Token = id;

            if (Match(TokenType.RSquareBracket, "Attribute expected ']'") != ParseAction.Matched)
                return null;

            return ast;
        }

        Ast ParseEnumDeclaration()
        {
            var ast = MatchSequence(
                typeof(EnumAst),
                Match(TokenType.Enum, "Missing 'enum' token"),
                KeepNext(),
                Match(TokenType.Identifier, "Missing enum identifier"),
                Match(TokenType.EndOfLine, "Missing EOL"));
            if (ast == null)
                return null; // nothing more to do
            MatchZeroOrMore(TokenType.EndOfLine);

            if (Match(TokenType.Indent, "enum missing values") == ParseAction.NotMatched)
                return null;
            if (OneOrMore(ast, ParseEnumValue, "enum missing values") == ParseAction.NotMatched)
                return null;
            if (Match(TokenType.Undent, "enum values don't end") == ParseAction.NotMatched)
                return null;

            return ast;
        }

        Ast ParseEnumValue()
        {
            // todo - make cleaner
            if (Lookahead("", TokenType.Identifier, TokenType.Equals))
            {
                var id = TokenStream.Consume();
                TokenStream.Consume(); // '='
                var ast = ParseExpression();
                if (ast == null) return null;
                if (MatchOneOrMore(TokenType.EndOfLine, "enum value missing EOL") != ParseAction.Matched)
                    return null;
                return MakeAst(typeof(EnumValueAst), id, ast);
            }
            else if (Lookahead("", TokenType.Identifier))
            {
                var id = TokenStream.Consume();
                if (MatchOneOrMore(TokenType.EndOfLine, "enum value missing EOL") != ParseAction.Matched)
                    return null;
                return MakeAst(typeof(EnumValueAst), id);
            }
            ErrorMessage("Cannot parse enum value");
            return null;
        }

        /// <summary>
        /// Get token with type, else null
        /// </summary>
        /// <returns></returns>
        Token ParseType()
        {
            var t = TokenStream.Current;
            if (
                t.TokenType == TokenType.Bool ||
                t.TokenType == TokenType.Int32 ||
                t.TokenType == TokenType.Float32 ||
                t.TokenType == TokenType.String ||
                t.TokenType == TokenType.Byte ||
                t.TokenType == TokenType.Identifier
            )
                return TokenStream.Consume();
            return null;
        }

        Ast ParseTypeDeclaration()
        {
            if (Match(TokenType.Type, "Missing 'type' token") != ParseAction.Matched)
                return null;
            if (!Lookahead("Missing type identifier", TokenType.Identifier))
                return null;
            var idToken = TokenStream.Consume();
            if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;

            if (Match(TokenType.Indent, "type missing values") == ParseAction.NotMatched)
                return null;

            var memberAsts = new List<Ast>();

            while (true)
            {

                var ast = TryParse(ParseTypeMember);
                if (ast == null)
                    break;
                memberAsts.AddRange(ast.Children[0].Children);
            }

            if (Match(TokenType.Undent, "type values don't end") == ParseAction.NotMatched)
                return null;

            if (!memberAsts.Any())
            {
                ErrorMessage("Expected type members");
                return null;
            }

            return MakeAst(typeof(TypeDeclarationAst), idToken, memberAsts.ToArray());
        }

        Ast ParseTypeMember()
        { // todo - compact...
            return ParseVariableDefinition(null,null,null,false);
        }

        Ast ParseVariableDefinition(Token importToken, Token exportToken, Token constToken, bool allowAssignment)
        {
            var startToken = TokenStream.Current; 
            if (importToken != null && exportToken != null)
            {
                ErrorMessage("Variable declaration cannot have both import and export");
                return null;
            }

            var typeToken = ParseType();
            if (typeToken == null)
            {
                ErrorMessage("Expected type");
                return null;
            }

            var idList = ParseIdList(typeToken, true,true);
            if (idList == null)
                return null;

            Ast assignments = null;
            if (allowAssignment)
            {
                if (Lookahead("", TokenType.Equals))
                {
                    TokenStream.Consume();
                    assignments = ParseExpressionList(1);
                    if (assignments == null)
                    {
                        ErrorMessage("Expected variable initializer");
                        return null;
                    }
                }
            }

            if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;
            var vd = new VariableDefinitionAst {Token = startToken};
            vd.Children.Add(idList);
            if (assignments != null)
                vd.AddChild(assignments);
            foreach (var item1 in idList.Children)
            {
                var item = item1 as TypedItemAst;
                if (item == null)
                    throw new InternalFailure("Expected IdAst children");
                item.ImportToken = importToken;
                item.ExportToken = exportToken;
                item.ConstToken = constToken;
            }
            return vd;
        }

        // parse one array expression
        // parses '[' an optional expression, then ']', then repeats
        Ast ParseArray(bool requireSize)
        {
            if (!Lookahead("Expected '[' for array", TokenType.LSquareBracket))
                return null;
            TokenStream.Consume(); // '['

            var arrayAst = new ArrayAst();

            // parse array node, make as child
            if (requireSize)
            {
                var expr = ParseExpression();
                if (expr == null)
                {
                    ErrorMessage("Expected expression for array sizes");
                    return null;
                }
                arrayAst.AddChild(expr);
            }

            if (Match(TokenType.RSquareBracket, "array declaration missing closing ']'") !=
                ParseAction.Matched)
                return null;
            return arrayAst;
        }

        // parse comma separated list of types with optional ids, array sizes, etc
        // if type is null, types are read from the list
        Ast ParseIdList(Token type1, bool requireNames, bool requireSizes)
        {
            var idAsts = new List<Ast>();
            while (true)
            {
                Token typeToken = type1;
                if (type1 == null)
                {
                    // read a type
                    typeToken = ParseType();
                    if (typeToken == null)
                    {
                        ErrorMessage("Expected type");
                        return null;
                    }
                }
                Token nameToken = null;
                if (requireNames)
                {
                    nameToken = TokenStream.Consume();
                    if (nameToken.TokenType != TokenType.Identifier)
                    {
                        ErrorMessage("Invalid token in identifier list:");
                        return null;
                    }
                }
                idAsts.Add(new TypedItemAst(nameToken,typeToken));

                while (Lookahead("", TokenType.LSquareBracket))
                {
                    var arrAst = ParseArray(requireSizes);
                    if (arrAst == null)
                    {
                        ErrorMessage("Expected array");
                        return null;
                    }
                    idAsts.Last().AddChild(arrAst);
                }

                if (!Lookahead("", TokenType.Comma))
                    break; // done
                TokenStream.Consume();
            }
            return MakeAst(typeof(TypedItemsAst), null, idAsts.ToArray());

        }

        #region Function parsing statements

        // parse parenthesized list of variables (type, name optional)
        List<Ast> FunctionHelper(string itemName, bool requireNames)
        {
            // prototype (ret vals) func name (params)
            if (Match(TokenType.OpenParen, $"function expected {itemName} in '(' and ')'") != ParseAction.Matched)
                return null;

            Ast items = null;
            if (!Lookahead("", TokenType.CloseParen))
            {
                // get types
                items = ParseIdList(null, requireNames, false);
                if (items == null)
                {
                    ErrorMessage($"Expected {itemName}");
                    return null;
                }
            }

            if (Match(TokenType.CloseParen, $"function expected {itemName} in '(' and ')'") != ParseAction.Matched)
                return null;
            if (items == null)
                return new List<Ast>();
            return items.Children;
        }

        Ast ParseFunctionDeclaration(Token importToken, Token exportToken)
        {
            if (importToken != null && exportToken != null)
            {
                ErrorMessage("Function declaration cannot have both import and export");
                return null;
            }

            var returnTypes = new ReturnValuesAst();
            var retItems = FunctionHelper("return values",false);
            if (retItems == null)
                return null;
            returnTypes.Children.AddRange(retItems);

            if (!Lookahead("Expected identifier as function name", TokenType.Identifier))
                return null;

            var name = ParseFunctionName();
            if (name == null)
            {
                ErrorMessage("Expected function name");
                return null;
            }
            var idToken = name.Token;

            var parameters = new ParameterListAst();
            var pItems = FunctionHelper("parameters",true);
            if (pItems == null)
                return null;
            parameters.Children.AddRange(pItems);

            if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;

            Ast block = null;
            if (importToken == null)
                block = ParseBlock();

            var funcDecl =
                (FunctionDeclarationAst) MakeAst(typeof(FunctionDeclarationAst), idToken, returnTypes, parameters, block);

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

        Ast ParseStatement()
        {
            if (Lookahead("", TokenType.If))
                return ParseIfStatement();
            if (Lookahead("", TokenType.For))
                return ParseForStatement();
            if (Lookahead("", TokenType.While))
                return ParseWhileStatement();

            if (PredictFunctionCall())
            {
                var ast = ParseFunctionCall();
                if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                    return null;
                return ast;
            }

            if (IsJumpToken())
                return ParseJumpStatement();

            var vd = TryParse(() => ParseVariableDefinition(null, null, null,true));
            if (vd != null) return vd;
            return ParseAssignStatement();

        }

        // if identifier(.identifier)*'(' then function call
        bool PredictFunctionCall()
        {
            if (!Lookahead("", TokenType.Identifier))
                return false;
            var i = 1;
            while (TokenStream.Peek(i).TokenType == TokenType.Dot &&
                   TokenStream.Peek(i + 1).TokenType == TokenType.Identifier)
                i += 2;
            return TokenStream.Peek(i).TokenType == TokenType.OpenParen;
        }

        static string[] assignOperators =
        {
            "=", "+=", "-=", "*=", "/=", "^=", "&=", "|=", "%=", ">>=", "<<=", ">>>=",
            "<<<="
        };

        Ast ParseAssignStatement()
        {
            var assignItems = ParseList<HelperAst>(
                ParseAssignItem, "Expected assign item", 1,
                (a, n) => n.AddChild(a),
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
                exprList = ParseExpressionList(1);
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

            if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                return null;

            var h = new AssignStatementAst(op);
            h.Children.Add(new TypedItemsAst(assignItems.Children));
            if (exprList != null)
                h.AddChild(exprList);
            return h;
        }

        bool NextIsOneOf(params string[] items)
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
            // ID[10]
            // ID[10].f
            // ID.a
            // ID[10].a[10]...
            // ID[1][2]

            // initial item
            if (!Lookahead("Expected identifier in assignment", TokenType.Identifier))
                return null;
            var ast = new TypedItemAst(TokenStream.Consume(), null);

            while (NextTokenOneOf(TokenType.LSquareBracket,TokenType.Dot))
            {
                if (Lookahead("", TokenType.LSquareBracket))
                {
                    var arr = ParseArray(true);
                    ast.AddChild(arr);
                }

                else if (Lookahead("", TokenType.Dot))
                {
                    TokenStream.Consume();
                    // needs identifier
                    if (!Lookahead("Expected identifier in assignment", TokenType.Identifier))
                        return null;
                    var id = TokenStream.Consume();
                    ast.AddChild(new DotAst(id));
                }
            }
            return ast;
        }

        Ast ParseIfStatement()
        {
            // get sequence of expr,block for if/else if run, final odd block is final else
            var items = new List<Ast>();

            while (true)
            {

                if (Match(TokenType.If, "Expected 'if' statement") != ParseAction.Matched)
                    return null;
                var expr = ParseExpression();
                if (expr == null)
                {
                    ErrorMessage("Expected expression after the 'if'");
                    return null;
                }
                if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s) after 'if' expression") !=
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
                if (!Lookahead("", TokenType.Else, TokenType.If))
                    break;

                TokenStream.Consume(); // consume 'else', go to top for 'if'
            }

            Ast finalBlock = null;
            if (Lookahead("", TokenType.Else))
            {
                TokenStream.Consume(); // else
                if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s) after 'else'") != ParseAction.Matched)
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
                var t = TokenStream.Consume();
                var h = new JumpStatementAst(t);
                if (t.TokenType == TokenType.Return)
                {
                    var retval = ParseExpressionList(0);
                    if (retval != null)
                        h.Children.Add(retval);
                }
                if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s)") != ParseAction.Matched)
                    return null;
                return h;
            }
            return null;
        }

        //
        Ast ParseForStatement()
        {
            if (Match(TokenType.For, "Expected 'for'") != ParseAction.Matched)
                return null;
            if (!Lookahead("Expected identifier after 'for'", TokenType.Identifier))
                return null;
            var id = TokenStream.Consume();
            if (Match(TokenType.In, "Expected 'in' in 'for' statement after identifier") != ParseAction.Matched)
                return null;

            // next comes either an array expression, or things of form "a..b" followed by optional "by id"
            // to decide: which comes first, '..' or end of line?

            var peekIndex = 0;
            var eolFirst = false;
            while (true)
            {
                var tt = TokenStream.Peek(peekIndex).TokenType;
                if (tt == TokenType.Range)
                    break;
                if (tt == TokenType.EndOfLine)
                {
                    eolFirst = true;
                    break;
                }
                peekIndex++;
            }

            Ast rangeDef;
            if (eolFirst)
                rangeDef = ParseAssignItem();
            else
            { // create expression list of 2 or 3 items
                Ast e1, e2, e3 = null;
                if ((e1 = ParseOrError(ParseExpression,"'for' expected range expression")) == null)
                    return null;
                if (Match(TokenType.Range, "Expected range '..' in 'for' statement after identifier") != ParseAction.Matched)
                    return null;
                if ((e2 = ParseOrError(ParseExpression, "'for' expected range expression")) == null)
                    return null;
                if (Lookahead("", TokenType.By))
                {
                    TokenStream.Consume(); // skip 'by'
                    if ((e3 = ParseOrError(ParseExpression, "'for' expected range expression")) == null)
                        return null;
                }

                rangeDef = new ExpressionListAst();
                rangeDef.AddChild(e1);
                rangeDef.AddChild(e2);
                rangeDef.AddChild(e3);
            }
            if (rangeDef == null)
            {
                ErrorMessage("Invalid 'for' limits");
                return null;
            }

            if (rangeDef.Children.Count > 3)
            {
                ErrorMessage("Too many expressions in 'for' limits");
                return null;
            }
            if (MatchOneOrMore(TokenType.EndOfLine, "Expected") != ParseAction.Matched)
                return null;
            Ast block;
            if ((block = ParseOrError(ParseBlock, "Expected block after 'for'")) == null)
                return null;

            var forAst = new ForStatementAst {Token = id};
            forAst.Children.Add(rangeDef);
            forAst.AddChild(block);
            return forAst;
        }

        Ast ParseWhileStatement()
        {
            var t = TokenStream.Consume();
            if (t.TokenType != TokenType.While)
            {
                ErrorMessage($"Expected 'while' but got {t}");
                return null;
            }
            var expr = ParseExpression();
            if (expr == null)
            {
                ErrorMessage("Expected expression following 'while'");
                return null;
            }
            if (MatchOneOrMore(TokenType.EndOfLine, "Expected end of line(s) after 'while' expression") !=
                ParseAction.Matched)
                return null;

            Ast block;
            if ((block = ParseOrError(ParseBlock, "Expected block after 'while'")) == null)
                return null;

            var whileStatement = new WhileStatementAst {Token = t};
            whileStatement.Children.Add(expr);
            whileStatement.Children.Add(block);
            return whileStatement;
        }

        Ast ParseBlock()
        {
            if (Match(TokenType.Indent, "Expected indented block") != ParseAction.Matched)
                return null;

            var block = ParseList<BlockAst>(ParseStatement, "Expected statement", 1, (a, n) => n.AddChild(a),
                TokenType.None);
            if (block == null)
            {
                ErrorMessage("Indented block parse failed");
                return null;
            }
            // eat any extra end of lines
            MatchZeroOrMore(TokenType.EndOfLine);

            if (Match(TokenType.Undent, "Expected indented block to end") != ParseAction.Matched)
                return null;
            return block;
        }

        #endregion

        // comma separated expression list
        Ast ParseExpressionList(int minCount, int maxCount = Int32.MaxValue)
        {
            var ast = ParseList<ExpressionListAst>(ParseExpression, "Expected expression", minCount,
                (a, n) => n.AddChild(a), TokenType.Comma);
            if (ast != null && maxCount != Int32.MaxValue && ast.Children.Count > maxCount)
            {
                ErrorMessage($"too many expressions. Expected at most {maxCount}");
                return null;
            }
            return ast;
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
            params TokenType[] midTokens)
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
        }

        Ast ParseLogicalAndExpression()
        {
            return ExpressionHelper(
                ParseInclusiveOrExpression,
                "inclusive OR",
                ParseLogicalAndExpression,
                "logical AND", TokenType.LogicalAnd);
        }

        Ast ParseInclusiveOrExpression()
        {
            return ExpressionHelper(
                ParseExclusiveOrExpression,
                "exclusive OR",
                ParseInclusiveOrExpression,
                "inclusive OR", TokenType.Pipe);
        }

        Ast ParseExclusiveOrExpression()
        {
            return ExpressionHelper(
                ParseAndExpression,
                "AND",
                ParseExclusiveOrExpression,
                "exclusive OR", TokenType.Caret);
        }

        Ast ParseAndExpression()
        {
            return ExpressionHelper(
                EqualityExpression,
                "equality",
                ParseAndExpression,
                "AND", TokenType.Ampersand);
        }

        Ast EqualityExpression()
        {
            return ExpressionHelper(
                RelationalExpression,
                "relational",
                EqualityExpression,
                "equality",
                TokenType.Compare, TokenType.NotEqual
            );
        }

        Ast RelationalExpression()
        {
            return ExpressionHelper(
                ShiftExpression,
                "shift",
                RelationalExpression,
                "relational",
                TokenType.LessThanOrEqual, TokenType.GreaterThanOrEqual, TokenType.LessThan, TokenType.GreaterThan
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
        }

        Ast MultiplicativeExpression()
        {
            return ExpressionHelper(
                UnaryExpression,
                "unary",
                MultiplicativeExpression,
                "multiplicative",
                TokenType.Asterix, TokenType.Slash, TokenType.Percent
            );
        }

        static TokenType[] UnaryOperatorTokens = {TokenType.Increment, TokenType.Decrement, TokenType.Plus, TokenType.Minus,
                TokenType.Tilde, TokenType.Exclamation};

        Ast UnaryExpression()
        {
            if (NextTokenOneOf(UnaryOperatorTokens))
            {
                var token = TokenStream.Consume();
                var ast = UnaryExpression();
                if (ast == null)
                {
                    ErrorMessage("Expected unary expression");
                    return null;
                }
                if (ast.Token != null)
                { // wrap it
                    var expr = new ExpressionAst();
                    expr.Children.Add(ast);
                    ast = expr;
                    //throw new InternalFailure("Token already set in UnaryExpression");
                }
                ast.Token = token;
                return ast;
            }
            return PostfixExpression();
        }

        Ast PostfixExpression()
        {
            var ast = PrimaryExpression();
            if (ast == null)
            {
                ErrorMessage("Expected primary expression");
                return null;
            }

            // suffixes: [..] +
            //
            var suffixes = new List<Ast>();
            while (true)
            {
                if (Lookahead("", TokenType.LSquareBracket))
                {
                    var arrAst = ParseArray(true);
                    suffixes.Add(arrAst);
                }
                else if (Lookahead("", TokenType.Dot))
                {
                    var tok = TokenStream.Consume();
                    if (!Lookahead("expected identifier after '.'", TokenType.Identifier))
                        return null;
                    var id = TokenStream.Consume();
                    var h1 = new HelperAst(tok);
                    h1.AddChild(new HelperAst(id));
                    suffixes.Add(h1);
                }
                break;
            }

            if (NextTokenOneOf(TokenType.Increment, TokenType.Decrement))
                suffixes.Add(new HelperAst(TokenStream.Consume()));

            ast.Children.AddRange(suffixes);

            return ast;
        }

        Ast PrimaryExpression()
        {
            var ast = TryParse(LiteralExpression);
            if (ast != null)
                return ast;
            ast = TryParse(ParseFunctionCall);
            if (ast != null)
                return ast;
            if (Lookahead("", TokenType.Identifier))
                return new IdentifierAst(TokenStream.Consume());
            // must be ( expr )
            if (Match(TokenType.OpenParen, "expected '(' followed by expression") != ParseAction.Matched)
                return null;
            ast = ParseExpression();
            if (ast == null)
            {
                ErrorMessage("Expected expression after '('");
                return null;
            }
            if (Match(TokenType.CloseParen, "expected ')' following expression") != ParseAction.Matched)
                return null;
            return ast;
        }

        static readonly TokenType[] LiteralTokenTypes =
        {
            TokenType.BinaryLiteral, TokenType.HexadecimalLiteral, TokenType.DecimalLiteral,
            TokenType.StringLiteral, TokenType.ByteLiteral, TokenType.FloatLiteral,
            TokenType.True, TokenType.False
        };

        Ast LiteralExpression()
        {
            if (NextTokenOneOf(LiteralTokenTypes))
                return new LiteralAst(TokenStream.Consume());
            return null;
        }

        Ast ParseFunctionCall()
        {
            Ast idAst;
            if ((idAst = ParseOrError(ParseScopedId, "Expected function call")) == null)
                return null;
            if (!Lookahead("Expected '(' for function call",TokenType.OpenParen))
                return null;
            TokenStream.Consume(); // '('
            var parameters = ParseExpressionList(0);
            if (Match(TokenType.CloseParen, "Expected ')' to close function call") != ParseAction.Matched)
                return null;
            return new FunctionCallAst(idAst.Token, parameters);
        }

        Ast ParseScopedId()
        {
            return ParseList<HelperAst>(TokenType.Identifier, "Expected identifier", 
                1, 
                (a, n) =>
                {
                    if (n.Token == null)
                        n.Token = a.Token;
                    else
                        n.Token = new Token(
                            n.Token.TokenType, 
                            n.Token.TokenValue + "." + a.Token.TokenValue,
                            n.Token.Position);
                },
                TokenType.Dot);
        }

        #endregion

        #endregion

        #region Helpers

        delegate Ast ParseDelegate();

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

        // return true if the next lookahead has the following tokens, else false
        // eats no tokens
        // outputs error message if not empty or null and look fails
        bool Lookahead(string errorMessage, params TokenType[] tokenTypes)
        {
            PreCheck();
            var matches = true;
            for (var i = 0; i < tokenTypes.Length && matches; ++i)
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
        Ast MatchSequence(Type astType, params ParseAction[] parseActions)
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
                        kept.Add(TokenStream.Peek(-(parseActions.Length - 1 - i)));
                        break;
                    default:
                        throw new InternalFailure($"Unknown parse action {action}");
                }
            }

            if (matched)
            {
                var ast = (Ast) Activator.CreateInstance(astType);
                if (kept.Count > 1)
                    throw new InternalFailure($"MatchSequence Only allows 0 or 1 kept items!");
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
        ParseAction Match(TokenType tokenType, string errorMessage)
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
                env.Error($"{error} at {TokenStream.Peek(-1)}");
        }

        // while next token matches, eat them
        // return Matched
        void MatchZeroOrMore(TokenType tokenType)
        {
            while (TokenStream.Current.TokenType == tokenType)
                TokenStream.Consume();
        }

        // while next token matches, eat them
        // return Matched if at least one
        ParseAction MatchOneOrMore(TokenType tokenType, string errorString)
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
        ParseAction OneOrMore(Ast parent, ParseDelegate parseFunc, string errorMessage)
        {
            Ast ast;
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
        Ast MakeAst(Type astType, Token token, params Ast[] children)
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
            if (Lookahead("", tokenType))
                return TokenStream.Consume();
            return null;
        }

        // todo - rewrite above with this - smaller, cleaner code
        Ast ParseOrError(ParseDelegate func, string errorMessage)
        {
            var ast = func();
            if (ast == null)
            {
                if (!String.IsNullOrEmpty(errorMessage))
                    ErrorMessage(errorMessage);
                return null;
            }
            return ast;
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
            ParseDelegate tokenMatcher = () =>
            {
                if (TokenStream.Current.TokenType == matchToken)
                {
                    var h = new HelperAst(TokenStream.Current);
                    TokenStream.Consume();
                    return h;
                }
                return null;
            };

            return ParseList(
                tokenMatcher,
                errorMessage, minItemCount, storeItem, delimiter
            );
        }


        // generic list parsing function
        // parses items, with optional delimiter tokens
        // TokenType none means no delimiter
        Ast ParseList<T>(
            ParseDelegate itemFunc,
            string errorMessage,
            int minItemCount,
            Action<Ast, T> storeItem,
            TokenType delimiter
        ) where T : Ast, new()
        {
            var items = new List<Ast>();

            var item = TryParse(itemFunc);
            while (item != null)
            {
                items.Add(item);
                if (delimiter != TokenType.None)
                {
                    // check delimiter
                    if (!Lookahead("", delimiter))
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
                storeItem(t, h);
            return h;
        }



        #endregion


        #endregion

    }
}

