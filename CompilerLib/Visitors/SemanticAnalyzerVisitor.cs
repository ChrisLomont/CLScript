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
         * 
         * Also fills in values of enums, memory item sizes, etc
         */

        public SemanticAnalyzerVisitor(Environment environment)
        {
            env = environment;
        }

        Environment env;
        SymbolTableManager mgr;
        // pairs of attributes and things that get them
        List<Tuple<Ast, Ast>> attributePairs = new List<Tuple<Ast, Ast>>();

        public void Check(SymbolTableManager symbolTable, Ast ast)
        {
            symbolTable.Start();
            mgr = symbolTable;
            Recurse(ast);
            FillAttributes();
        }

        void FillAttributes()
        {
            foreach (var pair in attributePairs)
            {
                var attr = pair.Item1 as AttributeAst;
                var func = pair.Item2 as FunctionDeclarationAst;
                var symbol = mgr.Lookup(func.Name);
                symbol.Attributes.Add(new Attribute(attr.Token.TokenValue,attr.Children.Select(c=>((LiteralAst)c).Token.TokenValue)));
            }
        }

        void Recurse(Ast node)
        {
            var recurseChildren = true;
            mgr.EnterAst(node);

            // for statement loop variable needs set here, before children processed
            if (node is BlockAst && node.Parent is ForStatementAst)
                ProcessForStatement((ForStatementAst)(node.Parent));

            // special case: '.' cannot have right child resolved in usual way...
            //if (node is ExpressionAst && node.Token.TokenType == TokenType.Dot)
            //{
            //    ProcessDot(node as ExpressionAst);
            //    recurseChildren = false;
            //}



            if (recurseChildren)
            {
                // recurse children
                foreach (var child in node.Children)
                    Recurse(child);
            }

            // adds type info, does type checking
            ProcessTypeForNode(node);

            mgr.ExitAst(node);
        }

        void ProcessDot(ExpressionAst node)
        {
            if (node.Children.Count != 2 || node.Token.TokenType != TokenType.Dot)
            {
                env.Error($"Dereference '.' ast node malformed {node}");
                return;
            }
            
            // get type for this one
            Recurse(node.Children[0]);

            // do children of other (should be none?)
            foreach (var child in node.Children[1].Children)
                Recurse(child);

            // now set the type and symbol of the right one
            var tbl = mgr.GetTableWithScope(node.Children[0].Type.UserTypeName);
            var symbol = mgr.Lookup(tbl, node.Children[1].Token.TokenValue);
            node.Children[1].Type = symbol.Type;

            // and set the type of this one
            node.Type = symbol.Type;
        }


        // adds type info, does type checking, other work

        // todo - replace constant variables with their value
        // todo - replace enum with their values
        // todo - cast expressions as needed

        void ProcessTypeForNode(Ast node)
        {
            var typeName = "";
            var symbolType = SymbolType.ToBeResolved;
            InternalType internalType = null;
            if (node is IdentifierAst)
            {
                typeName = ((IdentifierAst) node).Name;
                ((IdentifierAst) node).Symbol = mgr.Lookup(typeName);
            }
            else if (node is TypedItemAst && !(node.Parent.Parent is FunctionDeclarationAst))
                typeName = ((TypedItemAst) node).Name;
            else if (node is LiteralAst)
                symbolType = ProcessLiteral(node as LiteralAst, env);
            //else if (node is AssignItemAst)
            //    internalType = ProcessAssignItem((AssignItemAst)node);
            else if (node is AssignStatementAst)
                ProcessAssignment((AssignStatementAst) node);
            else if (node is VariableDefinitionAst)
                ProcessVariableDefinition((VariableDefinitionAst) node);
            else if (node is WhileStatementAst)
                ProcessWhileStatement((WhileStatementAst) node);
            else if (node is JumpStatementAst)
                ProcessJumpStatement((JumpStatementAst) node);
            else if (node is IfStatementAst)
                ProcessIfStatement((IfStatementAst) node);
            else if (node is FunctionDeclarationAst)
                ProcessFunctionDeclaration((FunctionDeclarationAst) node);
            else if (node is ExpressionAst)
                internalType = ProcessExpression(node as ExpressionAst);
            else if (node is EnumAst)
                ProcessEnum(node as EnumAst);
            else if (node is AttributeAst)
                ProcessAttribute(node as AttributeAst);

            if (internalType != null)
                node.Type = internalType;
            else if (!String.IsNullOrEmpty(typeName))
            {
                var symbol = mgr.Lookup(typeName);
                if (symbol == null)
                    env.Error($"Cannot find symbol definition {node}");
                else
                    node.Type = symbol.Type;
            }
            else if (symbolType != SymbolType.ToBeResolved)
            {
                var type1 = mgr.TypeManager.GetType(symbolType);
                if (type1 == null)
                    env.Error($"Cannot find symbol definition {node}");
                else
                    node.Type = type1;
            }
        }

        void ProcessAttribute(AttributeAst node)
        {
            // attach the attribute to the next non-attribute symbol(s)
            var index = node.Parent.Children.IndexOf(node);
            var count = node.Parent.Children.Count;
            while (index < count && node.Parent.Children[index] is AttributeAst)
                ++index;
            if (index == count)
            {
                env.Error($"Attribute modifies nothing following {node}");
                return;
            }
            var func = node.Parent.Children[index] as FunctionDeclarationAst;
            if (func == null)
            {
                env.Error($"Attribute must modify a following function {node}");
                return;
            }
            attributePairs.Add(new Tuple<Ast, Ast>(node,func));
        }



        // assign enum values, must all be constant now
        void ProcessEnum(EnumAst node)
        {
            var value = 0; // start default value
            foreach (var child in node.Children)
            {
                if (!(child is EnumValueAst))
                    throw new InternalFailure($"Enum child not proper AST {child}");
                if (child.Children.Any())
                {
                    var ex = child.Children[0] as ExpressionAst;
                    if (ex == null || !(ex.ByteValue.HasValue || ex.IntValue.HasValue))
                        env.Error($"Enum value {node.Children[0]} not constant");
                    else
                        value = ex.IntValue ?? ex.ByteValue ?? -1; // will be one of the first two
                }
                var symbol = mgr.Lookup((child as EnumValueAst).Name);
                if (symbol == null)
                    env.Error($"Symbol table missing enum val {child}");
                else
                    symbol.Value = value;
                value++;
            }
        }

        // ensure if expressions are boolean
        void ProcessIfStatement(IfStatementAst node)
        {
            // an if statement is alternating expression and block statements
            // with perhaps an extra block at the end

            var boolType = mgr.TypeManager.GetType(SymbolType.Bool);
            for (var i = 0; i < node.Children.Count-1; i += 2)
            {
                var expr = node.Children[i] as ExpressionAst;
                if (expr == null)
                    throw new InternalFailure($"Expected expression in 'if' {node}");
                if (expr.Type != boolType)
                    env.Error($"'if' expression is not boolean: {expr}");
            }
        }

        // check parameter types, (return types checked elsewhere on each return)
        // set node type to return type, and return it
        InternalType ProcessFunctionCall(FunctionCallAst node)
        {
            var symbol = mgr.Lookup(node.Token.TokenValue);
            if (symbol == null)
            {
                env.Error($"Cannot find function definition for {node}");
                return null;
            }

            var retVals = symbol.Type.ReturnType;
            var parms   = symbol.Type.ParamsType;

            // a fuction call has expressions as children
            if (parms.Count != node.Children.Count)
            {
                env.Error($"Function requires {parms.Count} parameters but has {node.Children.Count}, at {node}");
                return null;
            }
            for (var i = 0; i < parms.Count; ++i)
            {
                if (node.Children[i].Type != parms[i])
                {
                    env.Error($"Function required type {parms[i]} in position {i+1}");
                    return null;
                }
            }

            if (retVals.Count > 1)
            {
                env.Error($"Function returns with more than one parameter not yet supported {node}");
                return null;
            }
            if (retVals.Count == 1)
            {
                node.Type = retVals[0];
                return retVals[0];
            }
            return null;
        }

        // check types of bounds, set type of loop variable node
        // must be called in scope of for block to set type of variable
        void ProcessForStatement(ForStatementAst node)
        {
            if (node.Children.Count != 2 || !(node.Children[1] is BlockAst))
                throw new InternalFailure($"For structure invalid {node}");
            var forVar = mgr.Lookup(node.ForVariable);
            var bounds = node.Children[0];
            if (bounds is ExpressionListAst)
            { // tuple of 2 or 3 i32
                var types = GetTypes(bounds.Children);
                var count = types.Count;
                var i32type = mgr.TypeManager.GetType(SymbolType.Int32);
                var allI32 = types.All(tt => tt == i32type); // todo
                if (count < 2 || 3 < count || !allI32)
                    env.Error($"for range not 2 or 3 i32 values {node}");
                else
                {
                    forVar.Type = types[0];
                    node.Type = forVar.Type;
                }
            }
            // else if (bounds is AssignItemAst)
            // { // array of some item
            //     var t = bounds.Type;
            //     if (!t.ArrayDimensions.Any())
            //         env.Error($"for bounds needs to be one dimensional array {bounds}");
            //     else // strip off array part of type
            //     {
            //         forVar.Type = mgr.TypeManager.GetType(t.SymbolType);
            //         node.Type = forVar.Type;
            //     }
            // }
            else
                throw new InternalFailure($"For structure invalid {node}");
        }

        // ensure expression is boolean
        void ProcessWhileStatement(WhileStatementAst node)
        {
            if (node.Children.Count != 2 || !(node.Children[0] is ExpressionAst))
                throw new InternalFailure($"While statement has wrong structure {node}");
            var e = (ExpressionAst) node.Children[0];
            if (e.Type != mgr.TypeManager.GetType(SymbolType.Bool))
                env.Error($"While expression is not boolean {node}");
        }

        void ProcessJumpStatement(JumpStatementAst node)
        {
            var tt = node.Token.TokenType;

            if (tt == TokenType.Return)
            {
                // ensure return statements match return types
                Ast p = node;
                while (p != null && !(p is FunctionDeclarationAst))
                    p = p.Parent;
                if (p == null)
                    env.Error($"Return without enclosing function {node}");
                else
                {
                    var func = p as FunctionDeclarationAst;
                    var symbol   = mgr.Lookup(func.Name);
                    var funcReturnTypes = symbol.Type.ReturnType;
                    if (funcReturnTypes.Count == 0 && node.Children.Count == 0)
                        return; // nothing to do
                    var returnStatementTypes = GetTypes(node.Children[0].Children);

                    CheckAssignments(node,funcReturnTypes,returnStatementTypes);
                }
            }
            else if (tt == TokenType.Continue || tt == TokenType.Break)
            {
                // todo - ensure break, continue are in loops
                Ast p = node;
                while (p != null && !(p is ForStatementAst) && !(p is WhileStatementAst))
                    p = p.Parent;
                if (p == null)
                    env.Error($"Jump statement needs to be in a for or while loop: {node.Token}");
            }
        }

        static List<InternalType> GetTypes(List<Ast> children)
        {
            return children.Select(c => c.Type).ToList();
        }

        // ensure function declarations that need one end with a return statement 
        void ProcessFunctionDeclaration(FunctionDeclarationAst node)
        {
            if (node.Children.Count != 3 || !(node.Children[2] is BlockAst))
                throw new InternalFailure($"Function {node} has wrong structure");

            var symbol = mgr.Lookup(node.Name);
            var funcReturnTypes = symbol.Type.ReturnType;
            if (funcReturnTypes.Count == 0)
                return; // nothing to do here - any actual return statements checked elsewhere

            var block = (BlockAst) (node.Children[2]);
            if (!(block.Children.LastOrDefault() is JumpStatementAst))
                env.Error($"Function {node} requires a return statement at end.");
        }

        //InternalType ProcessAssignItem(AssignItemAst node)
        //{
        //    var symbol = mgr.Lookup(node.Token.TokenValue);
        //    if (symbol == null)
        //        env.Error($"Cannot find symbol {node}");
        //    return symbol?.Type;
        //}

        void CheckAssignments(Ast node, List<InternalType> left, List<InternalType> right)
        {
            // todo - check type symbols exist for user defined
            // todo - allow assigning list of items to structures...

            if (left.Count != right.Count)
            {
                env.Error($"Mismatched number of expressions to assignments {node}");
                return;
            }
            for (var i = 0; i < left.Count; ++i)
            {
                // todo - check type exists
                if (left[i] != right[i])
                    env.Error($"Types at position {i + 1} mismatched in assignment {node}");
            }

        }

        // check var type exists
        // check variable type exists, counts and types match
        void ProcessAssignment(AssignStatementAst node)
        {
            // todo - check type symbols exist for user defined
            // todo - allow assigning list of items to structures...
            if (node.Children.Count != 2)
                throw new InternalFailure($"Expected 2 children {node}");
            var items = node.Children[0] as TypedItemsAst;
            var exprs = node.Children[1] as ExpressionListAst;
            if (items == null || exprs == null)
                throw new InternalFailure($"Variable nodes incorrect {node}");
            CheckAssignments(node, GetTypes(items.Children), GetTypes(exprs.Children));
            // set symbols
            foreach (var item in items.Children)
            {
                var asn = item as TypedItemAst;
                if (asn.Symbol == null)
                    asn.Symbol = mgr.Lookup(asn.Name);
            }
        }

        // check var type exists
        // check variable type exists, counts and types match
        void ProcessVariableDefinition(VariableDefinitionAst node)
        {
            // todo - check type symbols exist for user defined
            // todo - allow assigning list of items to structures...
            if (node.Children.Count == 1)
                return; // nothing to check - only names variables
            if (node.Children.Count != 2)
                throw new InternalFailure($"Expected 2 children {node}");
            var items = node.Children[0] as TypedItemsAst;
            var exprs = node.Children[1] as ExpressionListAst;
            if (items == null || exprs == null)
                throw new InternalFailure($"Variable nodes incorrect {node}");
            CheckAssignments(node, GetTypes(items.Children),GetTypes(exprs.Children));
        }

        static Int32 ParseInt(Ast node)
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

        /// <summary>
        /// Process a literal, filling in the value
        /// </summary>
        /// <param name="node"></param>
        /// <param name="env"></param>
        /// <returns></returns>
        public static SymbolType ProcessLiteral(LiteralAst node, Environment env)
        {
            // compute value and return
            var t = node.Token.TokenType;
            if (t == TokenType.DecimalLiteral)
                ((LiteralAst)node).IntValue = ParseInt(node);
            else if (t == TokenType.HexadecimalLiteral)
                ((LiteralAst)node).IntValue = ParseInt(node);
            else if (t == TokenType.BinaryLiteral)
                ((LiteralAst)node).IntValue = ParseInt(node);
            else if (t == TokenType.FloatLiteral)
            {
                double v;
                if (!Double.TryParse(node.Token.TokenValue, out v))
                    env.Error($"Invalid floating point value {node}");
                else
                    ((LiteralAst)node).FloatValue = v;
            }
            else if (t == TokenType.True)
                ((LiteralAst)node).BoolValue = true;
            else if (t == TokenType.False)
                ((LiteralAst)node).BoolValue = false;
            else if (t == TokenType.ByteLiteral)
            {
                var v = ParseInt(node);
                if (v < 0 || 255 < v)
                    env.Warning($"Value {node} truncated to byte");
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
        // byte,i32     : >>,<<,>>>,<<<, &,|,^,%
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

        InternalType ProcessUnaryExpression(ExpressionAst node)
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
                env.Error(
                    $"Unary operator {node.Token.TokenValue} cannot be applied to type {child.Type.SymbolType}");
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
                (n,l,r) => n.BoolValue = l.ByteValue > r.ByteValue,
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

            // byte,i32     : <<
            new BinaryTableEntry(
                TokenType.LeftShift,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue << r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.LeftShift,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue << r.IntValue
                ),

            // byte,i32     : >>>
            new BinaryTableEntry(
                TokenType.RightRotate,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)RotateRight(l.ByteValue.Value,r.ByteValue.Value,8)
                ),
            new BinaryTableEntry(
                TokenType.RightRotate,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = RotateRight(l.IntValue.Value,r.IntValue.Value,32)
                ),

            // byte,i32     : <<<
            new BinaryTableEntry(
                TokenType.LeftRotate,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)RotateRight(l.ByteValue.Value,-r.ByteValue.Value,8)
                ),
            new BinaryTableEntry(
                TokenType.LeftRotate,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = RotateRight(l.IntValue.Value,-r.IntValue.Value,32)
                ),

            // byte,i32     : &
            new BinaryTableEntry(
                TokenType.Ampersand,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue & r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Ampersand,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue & r.IntValue
                ),

            // byte,i32     : |
            new BinaryTableEntry(
                TokenType.Pipe,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue | r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Pipe,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue | r.IntValue
                ),

            // byte,i32     : ^
            new BinaryTableEntry(
                TokenType.Caret,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue ^ r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Caret,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue ^ r.IntValue
                ),

            // byte,i32     : %
            new BinaryTableEntry(
                TokenType.Percent,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue % r.ByteValue)
                ),
            new BinaryTableEntry(
                TokenType.Percent,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue % r.IntValue
                ),

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


        // right rotate the number of bits, negative rotation rotates left
        static int RotateRight(int value, int shift, int numBits)
        {
            if (numBits < 0  || 32 < numBits)
                throw new InvalidExpression($"Cannot shift number of bits {numBits}");
            if (numBits == 0)
                return value;
            if (shift < 0)
            {
                // left shift by K is same as right shift by numbits-K
                shift = numBits - ((-shift)%numBits);
                if (shift < 0 || numBits < shift)
                    throw new InternalFailure("Invalid rotate assumption");
            }
            shift %= numBits;

            uint v = (uint)value; // work unsigned

            var l = (v >> shift)&((1U<<(numBits-shift))-1); // low bits
            var h = (v << (numBits - shift));               // high bits

            v = (h | l); // merged back
            if (numBits < 32)
                v&=((1U<<numBits)-1); // merged back

            return (int) v;
        }


        InternalType ProcessBinaryExpression(ExpressionAst node)
        {
            var left  = node.Children[0] as ExpressionAst;
            var right = node.Children[1] as ExpressionAst;

            if (left == null || right == null)
                throw new InternalFailure("Expected ExpressionAst");

            if (node.Token.TokenType == TokenType.LeftBracket)
                return DereferenceArray(node,left,right);
            if (node.Token.TokenType == TokenType.Dot)
            {
                if (node.Type == null)
                    throw new InternalFailure($"Dereference node {node} should already be typed");
                return node.Type; // done elsewhere
            }

            if (left.Type != right.Type)
            {
                env.Error($"Cannot combine types {left} and {right} via {node}");
                return null;
            }

            foreach (var entry in binaryActionTable)
            {
                if (entry.actionType == node.Token.TokenType &&
                    entry.leftValueType  == left.Type.SymbolType &&
                    entry.rightValueType == right.Type.SymbolType
                    )
                {
                    // it matches, do action if children have values
                    if (left.HasValue && right.HasValue)
                        entry.action(node, left, right);
                    if (entry.result != SymbolType.MatchAny)
                        node.Type = mgr.TypeManager.GetType(entry.result);
                    else
                        node.Type = left.Type; // left and right same here
                    return node.Type;
                }
            }
            env.Error($"Binary operator {node} cannot be applied to left {left} and {right}");
            return null;
        }

        // do type checking on array dereference, return type
        InternalType DereferenceArray(ExpressionAst node, ExpressionAst left, ExpressionAst right)
        {
            // left must be array type, right must be type Int32 or byte
            if (!left.Type.ArrayDimensions.Any())
            {
                env.Error($"Cannot apply array dereference '[' ']' to non-array {left}");
                return null;
            }

            if (right.Type != mgr.TypeManager.GetType(SymbolType.Byte) &&
                right.Type != mgr.TypeManager.GetType(SymbolType.Int32) &&
                right.Type != mgr.TypeManager.GetType(SymbolType.EnumValue)
            )
            {
                env.Error($"Array dereference {node} needs integral array index {right}");
                return null;
            }

            // costly, but need array dim here...
            var arrd = new List<int>();
            for (var i = 0; i < left.Type.ArrayDimensions.Count - 1; ++i)
                arrd.Add(left.Type.ArrayDimensions[i]);
            return mgr.TypeManager.GetType(
                left.Type.SymbolType,
                arrd,
                left.Type.UserTypeName
            );
        }


        #endregion 

        // process the expression, checking types, etc, and upgrading constants
        InternalType ProcessExpression(ExpressionAst node)
        {
            if (node is FunctionCallAst)
                return ProcessFunctionCall((FunctionCallAst) node);
            else if (node.Children.Count == 2)
                return ProcessBinaryExpression(node);
            else if (node.Children.Count == 1)
                return ProcessUnaryExpression(node);
            else if (node.Children.Count != 0)
                throw new InternalFailure($"Expression must have 0 to 2 children! {node}");
            env.Warning($"Unknown expression with 0 children {node}");
            return null; // error?
        }

        #endregion
    }
}
