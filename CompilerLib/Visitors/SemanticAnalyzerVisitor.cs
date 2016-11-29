using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    class SemanticAnalyzerVisitor 
    {
        /* things to check
         * 
         * Scope resolution
         * Type checking
         * Array bounds checking
         * Reserved identifier misuse
         * Multiple declaration of identifier in scope (done in symbol table build)
         * Accessing out of scope variable
         * Actual and formal parameter mismatch
         * Resolving types of unknown items - such as for loop variables, etc...
         */

        class State
        {
            public Environment env;
            public SymbolTableManager mgr;
        }

        public static void Check(SymbolTableManager symbolTable, Ast ast, Environment environment)
        {
            symbolTable.Start();
            var state = new State {mgr = symbolTable, env = environment};
            Recurse(ast,state);
        }

        static void Recurse(Ast node, State state)
        {
            // if (node is ExpressionAst)
            // {
            //     //ProcessExpression(node as ExpressionAst, state);
            //     return; // do no children - they are done
            // }


            state.mgr.EnterAst(node);

            // recurse
            foreach (var child in node.Children)
                Recurse(child, state);

            // adds type info, does type checking
            ProcessType(node,state);

            state.mgr.ExitAst(node);
        }


        // adds type info, does type checking
        static void ProcessType(Ast node, State state)
        {
            if (node is IdentifierAst)
            {
                var name = ((IdentifierAst) node).Name;
                var symbol = state.mgr.Lookup(name);
                if (symbol == null)
                    state.env.Error($"Cannot find symbol definition {node}");
                else
                    node.Type = symbol.Type.ToString();
            }
            else if (node is LiteralAst)
            {
                switch (node.Token.TokenType)
                {
                    case TokenType.True:
                    case TokenType.False:
                        node.Type = "bool";
                        break;
                    case TokenType.BinaryLiteral:
                    case TokenType.DecimalLiteral:
                    case TokenType.HexadecimalLiteral:
                        node.Type = "i32";
                        break;
                    case TokenType.FloatLiteral:
                        node.Type = "r32";
                        break;
                    case TokenType.CharacterLiteral:
                        node.Type = "byte";
                        break;
                    case TokenType.StringLiteral:
                        node.Type = "string";
                        break;
                    default:
                        state.env.Error($"Unknown literal type {node.Token.TokenType}");
                        break;
                }
            }

        }


#if false
        static Int32 ParseInt(State state, string text, int b, Ast node)
        {
            text = text.Replace("_", ""); // remove underscores
            text = text.ToLower();
            var error = false;
            error |= b == 2 && !text.StartsWith("0b");
            error |= b == 16 && !text.StartsWith("0x");
            if (!error && (b == 2 || b == 16))
                text = text.Substring(2); // chop prefix
            Int32 val = 0;
            if (!error)
            {
                foreach (var c in text)
                {
                    if (b == 10 && '0' <= c && c <= '9')
                        val = val * b + c - '0';
                    else if (b == 16 && '0' <= c && c <= '9')
                        val = val * b + c - '0';
                    else if (b == 16 && 'a' <= c && c <= 'f')
                        val = val*b + c - 'a' + 10;
                    else if (b == 2 && '0' <= c && c <= '1')
                        val = val*b + c - '0';
                    else
                        error = true;
                }
            }
            if (error)
                throw new InternalFailure($"Invalid base {b} at {node}");
            return val;
        }

        // types bool,byte,enum,i32,r32,string
        //
        // Binary operators: and types they apply to
        // all          : !=,==,
        // byte,i32,r32 : >=,>,<=,<,
        // bool         : ||,&&,
        // byte,i32     : >>,<<,>>>,<<<, &,|,^,++,--,%
        // byte,i32,r32 : +,-,/,*
        //
        // byte promoted to i32 for ops, enum turned to i32
        // i32  promoted to r32
        // only demotion is assignment
        // 
        // Unary operators and types they apply to
        // +,-,!,~
        //

        /// <summary>
        /// Does everything possible to an expression
        /// Expressions are literals, function calls, and identifiers
        /// </summary>
        /// <param name="node"></param>
        static void ProcessExpression(Ast node, State state)
        {
            todo - make table based - table tells what can be done, else errors

            // process children first, bubble up
            foreach (var ch in node.Children)
                ProcessExpression(ch, state);

            // now process individual node types

            if (node is LiteralAst)
            { // compute value and return
                var t = node.Token.TokenType;
                if (t == TokenType.DecimalLiteral)
                    ((LiteralAst) node).IntValue = ParseInt(state, node.Token.TokenValue, 10, node);
                else if (t == TokenType.HexadecimalLiteral)
                    ((LiteralAst)node).IntValue = ParseInt(state, node.Token.TokenValue, 16, node);
                else if (t == TokenType.BinaryLiteral)
                    ((LiteralAst)node).IntValue = ParseInt(state, node.Token.TokenValue, 2, node);
                else if (t == TokenType.FloatLiteral)
                {
                    double v;
                    if (!Double.TryParse(node.Token.TokenValue, out v))
                        Error(state, $"Invalid floating point value {node}");
                    else
                        ((LiteralAst) node).DoubleValue = v;
                }
            }

            else if (node is IdentifierAst)
            {
                state.env.Output.WriteLine("TODO - check constant and enum literals in expression");
            }

            else if (node is ExpressionAst)
            {

                // todo - replace constant variables with their value
                // todo - replace enum with their values
                // todo - cast expressions as needed

                // replace combination of literals with single literal

                if (node.Children.Count == 2)
                {
                    // process binary

//                // process all children first
//                var left = node.Children[0];
//                var right = node.Children[1];
//
//                if (left is ExpressionAst && right is ExpressionAst)
//                {
//                    left  = ProcessExpression(left as ExpressionAst, state);
//                    right = ProcessExpression(right as ExpressionAst, state);
//                }
//
//                if (left is LiteralAst && right is LiteralAst)
//                {
//                    switch (node.Token.TokenType)
//                    {
//                        //case TokenType.Plus:
//                        //    break;
//                        default:
//                            state.env.Output.WriteLine($"ERROR: unknown expression token {node}");
//                            break;
//                    }
//                }
                }
                else if (node.Children.Count == 1)
                {
                    // process unary
                    var c = node.Children[0] as ExpressionAst;
                    if (c is LiteralAst &&  c.HasValue)
                    {
                        switch (node.Token.TokenType)
                        {
                            case TokenType.Increment:
                            case TokenType.Decrement:
                                Error(state, $"cannot apply operator {node} to literal {c}");
                                break;
                            case TokenType.Plus:
                                ((ExpressionAst) node).IntValue = c.IntValue;
                                ((ExpressionAst) node).DoubleValue = c.DoubleValue;
                                break;
                            case TokenType.Minus:
                                ((ExpressionAst) node).IntValue = -c.IntValue;
                                ((ExpressionAst) node).DoubleValue = -c.DoubleValue;
                                break;
                            case TokenType.Tilde: // bitwise not
                                ((ExpressionAst)node).IntValue = -c.IntValue;
                                break;
                            case TokenType.Exclamation: // logical not
                            default:
                                Error(state, $"unknown unary expression token {node}");
                                break;
                        }
                    }

                }
                else if (node.Children.Count != 0)
                {
                    throw new InternalFailure($"Expression must have 0 to 2 children! {node}");
                }
            }
            else
            {
                throw new InternalFailure($"Unknown ExpressionAst derived node {node}");
            }
    }
#endif
    }
}
