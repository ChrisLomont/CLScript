using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
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
            state.mgr.EnterAst(node);

            // recurse children first
            foreach (var child in node.Children)
                Recurse(child, state);

            // adds type info, does type checking
            ProcessTypeForNode(node,state);

            state.mgr.ExitAst(node);
        }


        // adds type info, does type checking, other work

        // todo - replace constant variables with their value
        // todo - replace enum with their values
        // todo - cast expressions as needed

        static void ProcessTypeForNode(Ast node, State state)
        {
            var typeName = "";
            var symbolType = SymbolType.ToBeResolved;
            InternalType internalType = null;
            if (node is IdentifierAst)
                typeName = ((IdentifierAst) node).Name;
            else if (node is TypedItemAst)
                typeName = ((TypedItemAst)node).Name;
            else if (node is LiteralAst)
                symbolType = ProcessLiteral(node as LiteralAst, state);
            else if (node is ExpressionAst)
                internalType = ProcessExpression(node as ExpressionAst, state);

            if (internalType != null)
                node.Type = internalType;
            else if (!String.IsNullOrEmpty(typeName))
            {
                var symbol = state.mgr.Lookup(typeName);
                if (symbol == null)
                    state.env.Error($"Cannot find symbol definition {node}");
                else
                    node.Type = symbol.Type;
            }
            else if (symbolType != SymbolType.ToBeResolved)
            {
                var type1 = state.mgr.TypeManager.GetType(symbolType);
                if (type1 == null)
                    state.env.Error($"Cannot find symbol definition {node}");
                else
                    node.Type = type1;
            }
        }

        static Int32 ParseInt(Ast node, State state)
        {
            var text = node.Token.TokenValue;
            text = text.Replace("_", ""); // remove underscores
            text = text.ToLower();
            var error = false;
            var b = 10;
            if (text.StartsWith("0b"))
                b = 2;
            else if (text.StartsWith("0x"))
                b = 16;
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
                        val = val * b + c - 'a' + 10;
                    else if (b == 2 && '0' <= c && c <= '1')
                        val = val * b + c - '0';
                    else
                        error = true;
                }
            }
            if (error)
                throw new InternalFailure($"Invalid base {b} at {node}");
            return val;
        }

        static SymbolType ProcessLiteral(LiteralAst node, State state)
        {
            // compute value and return
            var t = node.Token.TokenType;
            if (t == TokenType.DecimalLiteral)
                ((LiteralAst)node).IntValue = ParseInt(node, state);
            else if (t == TokenType.HexadecimalLiteral)
                ((LiteralAst)node).IntValue = ParseInt(node, state);
            else if (t == TokenType.BinaryLiteral)
                ((LiteralAst)node).IntValue = ParseInt(node, state);
            else if (t == TokenType.FloatLiteral)
            {
                double v;
                if (!Double.TryParse(node.Token.TokenValue, out v))
                    state.env.Error($"Invalid floating point value {node}");
                else
                    ((LiteralAst)node).FloatValue = v;
            }
            else if (t == TokenType.True)
                ((LiteralAst)node).BoolValue = true;
            else if (t == TokenType.False)
                ((LiteralAst)node).BoolValue = false;
            else if (t == TokenType.ByteLiteral)
            {
                var v = ParseInt(node, state);
                if (v < 0 || 255 < v)
                    state.env.Warning($"Value {node} truncated to byte");
                ((LiteralAst) node).ByteValue = (byte)v;
            }

            return LiteralType(node.Token.TokenType);
        }

        static SymbolType LiteralType(TokenType tokType)
        {
            switch (tokType)
            {
                case TokenType.BinaryLiteral:
                case TokenType.DecimalLiteral:
                case TokenType.HexadecimalLiteral:
                    return SymbolType.Int32;
                case TokenType.ByteLiteral:
                    return SymbolType.Byte;
                case TokenType.StringLiteral:
                    return SymbolType.String;
                case TokenType.FloatLiteral:
                    return SymbolType.Float32;
                case TokenType.True:
                    return SymbolType.Bool;
                case TokenType.False:
                    return SymbolType.Bool;
                default:
                    throw new InternalFailure($"Invalid literal type {tokType}");
            }
        }

        #region Process Expression

        // rules
        // byte promoted to i32 for ops, enum turned to i32
        // i32  promoted to r32
        // only demotion is assignment
        //
        // types bool,byte,enum,i32,r32,string
        //
        // Binary operators: and types they apply to
        // bool,byte,i32,r32  : !=,==,
        // byte,i32,r32 : >=,>,<=,<,
        // bool         : ||,&&,
        // byte,i32     : >>,<<,>>>,<<<, &,|,^,++,--,%
        // byte,i32,r32 : +,-,/,*
        //
        // 
        // Unary operators and types they apply to
        // 
        // bool          : !
        // byte, i32     : ~
        // byte,i32,r32  : +,-

        #region Unary Expressions
        delegate void UnaryAction(ExpressionAst node, ExpressionAst child);

        // Unary expression table: given type and op, return result or null if illegal
        class UnaryTableEntry
        {
            public TokenType actionType;
            public SymbolType valueType;
            public UnaryAction action;
            public UnaryTableEntry(TokenType actionType, SymbolType valueType, UnaryAction action)
            {
                this.actionType = actionType;
                this.valueType = valueType;
                this.action = action;
            }
        }

        static UnaryTableEntry[] unaryActionTable =
        {
            // bool          : !
            new UnaryTableEntry(
                TokenType.Exclamation,
                SymbolType.Bool, 
                (n,c)=>n.BoolValue = !c.BoolValue),

            // byte,i32,r32  : +
            new UnaryTableEntry(
                TokenType.Plus,
                SymbolType.Byte,
                (n,c)=>n.ByteValue = c.ByteValue),
            new UnaryTableEntry(
                TokenType.Plus,
                SymbolType.Int32,
                (n,c)=>n.IntValue = c.IntValue),
            new UnaryTableEntry(
                TokenType.Plus,
                SymbolType.Float32,
                (n,c)=>n.FloatValue = c.FloatValue),

            // byte,i32,r32  : -
            new UnaryTableEntry(
                TokenType.Minus,
                SymbolType.Byte,
                (n,c)=>n.ByteValue = (byte)(-c.ByteValue)),
            new UnaryTableEntry(
                TokenType.Minus,
                SymbolType.Int32,
                (n,c)=>n.IntValue = -c.IntValue),
            new UnaryTableEntry(
                TokenType.Minus,
                SymbolType.Float32,
                (n,c)=>n.FloatValue = -c.FloatValue),

            // byte,i32      : ~
            new UnaryTableEntry(
                TokenType.Tilde,
                SymbolType.Byte,
                (n,c)=>n.ByteValue = (byte)(~c.ByteValue)),
            new UnaryTableEntry(
                TokenType.Tilde,
                SymbolType.Int32,
                (n,c)=>n.IntValue = ~c.IntValue)
        };

        static InternalType ProcessUnaryExpression(ExpressionAst node, State state)
        {
            var child = node.Children[0] as ExpressionAst;

            foreach (var entry in unaryActionTable)
            {
                if (entry.actionType == node.Token.TokenType &&
                    entry.valueType == child.Type.SymbolType)
                {
                    // matches, do action if child has a value
                    if (child.HasValue)
                        entry.action(node, child);
                    node.Type = child.Type;
                    return node.Type;
                }
            }
            state.env.Error($"Unary operator {node.Token.TokenValue} cannot be applied to type {child.Type.SymbolType}");
            return null;
        }
        #endregion

        #region BinaryExpressions

        delegate void BinaryAction(ExpressionAst parent, ExpressionAst left, ExpressionAst right);

        // Binary expression table: given type and op, return result or null if illegal
        class BinaryTableEntry
        {
            public TokenType actionType;
            public SymbolType leftValueType,rightValueType, result;
            public BinaryAction action;
            public BinaryTableEntry(
                TokenType actionType, 
                SymbolType leftValueType, SymbolType rightValueType,
                BinaryAction action, 
                SymbolType resultType = SymbolType.MatchAny
                )
            {
                this.actionType = actionType;
                this.leftValueType = leftValueType;
                this.rightValueType = rightValueType;
                this.action = action;
                result = resultType;
            }
        }

        // Binary operators: and types they apply to
        static BinaryTableEntry[] binaryActionTable =
        {

            // bool,byte,i32,r32  : !=
            new BinaryTableEntry(
                TokenType.NotEqual,
                SymbolType.Bool,SymbolType.Bool,
                (n, l,r) => n.BoolValue = l.BoolValue != r.BoolValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.NotEqual,
                SymbolType.Byte,SymbolType.Byte,
                (n, l,r) => n.BoolValue = l.ByteValue != r.ByteValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.NotEqual,
                SymbolType.Int32,SymbolType.Int32,
                (n, l,r) => n.BoolValue = l.IntValue != r.IntValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.NotEqual,
                SymbolType.Float32,SymbolType.Float32,
                (n, l,r) => n.BoolValue = l.FloatValue != r.FloatValue,
                SymbolType.Bool
                ),
            
            // bool,byte,i32,r32  : ==
            new BinaryTableEntry(
                TokenType.Compare,
                SymbolType.Bool,SymbolType.Bool,
                (n, l,r) => n.BoolValue = l.BoolValue == r.BoolValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.Compare,
                SymbolType.Byte,SymbolType.Byte,
                (n, l,r) => n.BoolValue = l.ByteValue == r.ByteValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.Compare,
                SymbolType.Int32,SymbolType.Int32,
                (n, l,r) => n.BoolValue = l.IntValue == r.IntValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.Compare,
                SymbolType.Float32,SymbolType.Float32,
                (n, l,r) => n.BoolValue = l.FloatValue == r.FloatValue,
                SymbolType.Bool
                ),

            // byte : >=,>,<=,<,
            new BinaryTableEntry(
                TokenType.GreaterThanOrEqual,
                SymbolType.Byte,SymbolType.Byte,
                (n, l,r) => n.BoolValue = l.ByteValue >= r.ByteValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.GreaterThan,
                SymbolType.Byte,SymbolType.Byte,
                (n, l,r) => n.BoolValue = l.ByteValue > r.ByteValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LessThanOrEqual,
                SymbolType.Byte,SymbolType.Byte,
                (n, l,r) => n.BoolValue = l.ByteValue <= r.ByteValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LessThan,
                SymbolType.Byte,SymbolType.Byte,
                (n, l,r) => n.BoolValue = l.ByteValue < r.ByteValue,
                SymbolType.Bool
                ),

            // i32 : >=,>,<=,<,
            new BinaryTableEntry(
                TokenType.GreaterThanOrEqual,
                SymbolType.Int32,SymbolType.Int32,
                (n, l,r) => n.BoolValue = l.IntValue >= r.IntValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.GreaterThan,
                SymbolType.Int32,SymbolType.Int32,
                (n, l,r) => n.BoolValue = l.IntValue > r.IntValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LessThanOrEqual,
                SymbolType.Int32,SymbolType.Int32,
                (n, l,r) => n.BoolValue = l.IntValue <= r.IntValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LessThan,
                SymbolType.Int32,SymbolType.Int32,
                (n, l,r) => n.BoolValue = l.IntValue < r.IntValue,
                SymbolType.Bool
                ),

            // r32 : >=,>,<=,<,
            new BinaryTableEntry(
                TokenType.GreaterThanOrEqual,
                SymbolType.Float32,SymbolType.Float32,
                (n, l,r) => n.BoolValue = l.FloatValue >= r.FloatValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.GreaterThan,
                SymbolType.Float32,SymbolType.Float32,
                (n, l,r) => n.BoolValue = l.FloatValue > r.FloatValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LessThanOrEqual,
                SymbolType.Float32,SymbolType.Float32,
                (n, l,r) => n.BoolValue = l.FloatValue <= r.FloatValue,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LessThan,
                SymbolType.Float32,SymbolType.Float32,
                (n, l,r) => n.BoolValue = l.FloatValue < r.FloatValue,
                SymbolType.Bool
                ),


            // bool         : ||,&&,
            new BinaryTableEntry(
                TokenType.LogicalOr,
                SymbolType.Bool,SymbolType.Bool,
                (n, l, r) => n.BoolValue = l.BoolValue.Value || r.BoolValue.Value
                ),
            new BinaryTableEntry(
                TokenType.LogicalAnd,
                SymbolType.Bool,SymbolType.Bool,
                (n, l, r) => n.BoolValue = l.BoolValue.Value && r.BoolValue.Value
                ),

            // byte,i32     : >>
            new BinaryTableEntry(
                TokenType.RightShift,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue >> r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.RightShift,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue >> r.IntValue
                ),

            // byte,i32     : >>,<<,>>>,<<<, &,|,^,++,--,%
            //todo

            // byte,i32,r32 : +
            new BinaryTableEntry(
                TokenType.Plus,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue+ r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Plus,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue + r.IntValue
                ),
            new BinaryTableEntry(
                TokenType.Plus,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue + r.FloatValue
                ),
            // byte,i32,r32 : -
            new BinaryTableEntry(
                TokenType.Minus,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue - r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Minus,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue - r.IntValue
                ),
            new BinaryTableEntry(
                TokenType.Minus,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue - r.FloatValue
                ),
            // byte,i32,r32 : *
            new BinaryTableEntry(
                TokenType.Asterix,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue * r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Asterix,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue * r.IntValue
                ),
            new BinaryTableEntry(
                TokenType.Asterix,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue * r.FloatValue
                ),
            // byte,i32,r32 : /
            // todo - check div 0 - will throw exception now
            new BinaryTableEntry(
                TokenType.Slash,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue / r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Slash,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue / r.IntValue
                ),
            new BinaryTableEntry(
                TokenType.Slash,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue / r.FloatValue
                ),
        };

        static InternalType ProcessBinaryExpression(ExpressionAst node, State state)
        {
            var left  = node.Children[0] as ExpressionAst;
            var right = node.Children[1] as ExpressionAst;

            if (left.Type != right.Type)
            {
                state.env.Error($"Cannot combine types {left} and {right} via {node}");
                return null;
            }

            foreach (var entry in binaryActionTable)
            {
                if (entry.actionType == node.Token.TokenType &&
                    entry.leftValueType == left.Type.SymbolType &&
                    entry.rightValueType == right.Type.SymbolType
                    )
                {
                    // it matches, do action if children have values
                    if (left.HasValue && right.HasValue)
                        entry.action(node, left, right);
                    if (entry.result != SymbolType.MatchAny)
                        node.Type = state.mgr.TypeManager.GetType(entry.result);
                    else
                        node.Type = left.Type; // so far left and right same
                    return node.Type;
                }
            }
            state.env.Error($"Binary operator {node} cannot be applied to left {left} and {right}");
            return null;
        }

        #endregion 

        // process the expression, checking types, etc, and upgrading constants
        static InternalType ProcessExpression(ExpressionAst node, State state)
        {
            if (node.Children.Count == 2)
                return ProcessBinaryExpression(node, state);
            else if (node.Children.Count == 1)
                return ProcessUnaryExpression(node, state);
            else if (node.Children.Count != 0)
                throw new InternalFailure($"Expression must have 0 to 2 children! {node}");
            state.env.Warning($"Unknown expression with 0 children {node}");
            return null; // error?
        }

        #endregion

#if false

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


            else if (node is IdentifierAst)
            {
                state.env.Output.WriteLine("TODO - check constant and enum literals in expression");
            }

            else if (node is ExpressionAst)
            {

            }
            else
            {
                throw new InternalFailure($"Unknown ExpressionAst derived node {node}");
            }
    }
#endif
    }
}
