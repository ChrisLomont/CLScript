using System;
using System.Collections.Generic;
using System.Linq;
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
         * 
         * TODO 
         * 1. check globals have values 
         * 2. array sizes must be constant
         * 
         */

        public SemanticAnalyzerVisitor(Environment environment)
        {
            env = environment;
        }

        readonly Environment env;
        SymbolTableManager mgr;
        // pairs of attributes and things that get them
        readonly List<Tuple<Ast, Ast>> attributePairs = new List<Tuple<Ast, Ast>>();

        public void Check(SymbolTableManager symbolTable, Ast ast)
        {
            mgr = symbolTable;
            mgr.Start();
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
                symbol.Attributes.Add(new Attribute(attr.Name,attr.Children.Select(c=>((LiteralAst)c).Name)));
            }
        }

        void Recurse(Ast node)
        {
            var recurseChildren = true;
            mgr.EnterAst(node);

            // for statement loop variable needs set here, before children processed
            if (node is BlockAst && node.Parent is ForStatementAst)
                ProcessForStatement((ForStatementAst) (node.Parent));

            if (node is TypedItemAst && node.Children.Any())
            {
                // do not do children. This is a type definition, and should already be typed
                var ti = (TypedItemAst) node;
                if (ti.Type == null || ti.Symbol == null)
                    throw new InternalFailure($"TypedItemAst {node} not filled in.");
                recurseChildren = false;
            }

            if (node is ParameterListAst || node is ReturnValuesAst)
                recurseChildren = false;
                    // no semantics to check - syntax was enough, and checking causes later problems

            if (recurseChildren)
            {
                // recurse children
                // NOTE: do not use foreach here since tree may be modified
                for (var i = 0; i < node.Children.Count; ++i)
                {
                    var child = node.Children[i];
                    Recurse(child);
                }
            }

            // adds type info, does type checking
            ProcessTypeForNode(node);

            mgr.ExitAst(node);
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
            {
                typeName = ((TypedItemAst) node).Name;
                ((TypedItemAst)node).Symbol = mgr.Lookup(typeName);
            }
            else if (node is LiteralAst)
                symbolType = ProcessLiteral((LiteralAst) node, env);
            else if (node is ArrayAst)
                internalType = ProcessArray((ArrayAst) node);
            else if (node is DotAst)
                internalType = ProcessDot((DotAst) node);
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
                internalType = ProcessExpression((ExpressionAst) node);
            else if (node is EnumAst)
                ProcessEnum((EnumAst) node);
            else if (node is AttributeAst)
                ProcessAttribute((AttributeAst) node);

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

        InternalType ProcessDot(DotAst node)
        {
            if (node.Children.Count != 1)
            {
                env.Error($"Dot ast malformed {node}");
                return null;
            }
            // get type of child
            var type = node.Children[0].Type as UserType;
            if (type == null)
            {
                env.Error($"Dot ast missing child type or wrong type {node}");
                return null;
            }
            if (String.IsNullOrEmpty(type.Name))
            {
                env.Error($"Dot ast missing child type {node}");
                return null;
            }
            var symbolName = node.Name;
            var typeTable = mgr.GetTableWithScope(type.Name);
            var symbol = mgr.Lookup(typeTable, symbolName);
            if (symbol == null)
            {
                env.Error($"Dot ast cannot locate symbol {symbolName} in type {type.Name}");
                return null;
            }
            node.Symbol = symbol;

            // expand enum here to i32
            var dotType = symbol.Type as SimpleType;
            if (dotType != null && dotType.SymbolType == SymbolType.EnumValue)
            {
                // replace node with literal
                var memberText = node.Token.TokenValue;
                var enumText = node.Children[0].Token.TokenValue;

                int value;
                if (!mgr.LookupEnumValue(enumText,memberText, out value))
                {
                    env.Error($"Cannot find enum value for {enumText}.{memberText}");
                    return null;
                }

                // tag symbols as used
                mgr.Lookup(enumText).Used = true;
                mgr.Lookup(mgr.GetTableWithScope(enumText), memberText).Used = true;

                // env.Info($"Enum: {enumText}.{memberText} has value {value}");

                var literal = new LiteralAst(new Token(TokenType.DecimalLiteral,value.ToString(),null,"inserted"));
                literal.IntValue = value;

                var p = node.Parent;
                var pos = p.Children.IndexOf(node);
                p.Children.Remove(node);
                p.Children.Insert(pos,literal);
                literal.Parent = p;
                literal.Type = mgr.TypeManager.GetType(SymbolType.Int32);
                return literal.Type;

            }
            return symbol.Type;
        }

        // do type checking on array dereference, return type
        InternalType ProcessArray(ArrayAst node)
        {
            if (node.Children.Count == 1)
                return null; // this is in a definition like i32 a[5]

            if (node.Children.Count != 2)
            {
                env.Error($"Array ast malformed {node}");
                return null;
            }
            var indexNode = node.Children[0];
            var indexType = indexNode.Type;
            var itemNode = node.Children[1];
            var itemType = itemNode.Type as ArrayType;
            if (itemType == null)
            {
                env.Error($"Expected array type {itemNode.Type}");
                return null;
            }

            // item must be array type, right must be type Int32 or byte
            if (itemType.ArrayDimension == 0)
            {
                env.Error($"Cannot apply array dereference '[' ']' to non-array {itemNode}");
                return null;
            }

            // allowed index types: int32, byte, enum value
            if (indexType != mgr.TypeManager.GetType(SymbolType.Byte) &&
                indexType != mgr.TypeManager.GetType(SymbolType.Int32) &&
                indexType != mgr.TypeManager.GetType(SymbolType.EnumValue)
            )
            {
                env.Error($"Array dereference {node} needs integral array index {indexNode}");
                return null;
            }

            // one less array dimension
            if (itemType.ArrayDimension > 1)
                return mgr.TypeManager.GetType(
                    itemType.ArrayDimension - 1,
                    itemType.BaseType);
            return itemType.BaseType;
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
            var symbol = mgr.Lookup(node.Name);
            if (symbol == null)
            {
                env.Error($"Cannot find function definition for {node}");
                return null;
            }
            node.Symbol = symbol;
            var type = symbol.Type as FunctionType;
            if (type == null)
                throw new InternalFailure($"Expected function type {node.Type}");

            var parameterTypes   = type.ParamsType.Tuple;

            // a function call has expressions as children
            if (parameterTypes.Count != node.Children.Count)
            {
                env.Error($"Function requires {parameterTypes.Count} parameters but has {node.Children.Count}, at {node}");
                return null;
            }
            for (var i = 0; i < parameterTypes.Count; ++i)
            {
                if (node.Children[i].Type != parameterTypes[i])
                {
                    env.Error($"Function {node} required type {parameterTypes[i]} in position {i+1}");
                    return null;
                }
            }
            node.Type = type.ReturnType.Tuple.Count == 1 ? type.ReturnType.Tuple[0] : type.ReturnType;
            return null;
        }

        // check types of bounds, set type of loop variable node
        // must be called in scope of for block to set type of variable
        void ProcessForStatement(ForStatementAst node)
        {
            if (node.Children.Count != 2 || !(node.Children[1] is BlockAst))
                throw new InternalFailure($"For structure invalid {node}");
            var forVar = mgr.Lookup(node.Name);
            var bounds = node.Children[0];
            if (bounds is ExpressionListAst)
            { // tuple of 2 or 3 i32
                var types = GetTypes(bounds.Children);
                var count = types.Count;
                var i32Type = mgr.TypeManager.GetType(SymbolType.Int32);
                var allI32 = types.All(tt => tt == i32Type); // todo
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

        // gives 0 index of first parent type matched, else returns -1
        int FirstParentIndex(Ast node, out Ast matched, params Type[] parentTypes)
        {
            matched = null;
            // ensure return statements match return types
            Ast p = node;
            while (p != null)
            {
                p = p.Parent;
                if (p != null && parentTypes.Contains(p.GetType()))
                {
                    matched = p;
                    return Array.IndexOf(parentTypes, p.GetType());
                }
            }
            return -1;
        }

        void ProcessJumpStatement(JumpStatementAst node)
        {
            var tt = node.Token.TokenType;

            if (tt == TokenType.Return)
            {
                // ensure return statements match return types
                Ast p;
                var index = FirstParentIndex(node, out p, typeof(FunctionDeclarationAst));
                if (index == -1)
                    env.Error($"Return without enclosing function {node}");
                else
                {
                    var func = p as FunctionDeclarationAst;
                    var funcType = func?.Symbol.Type as FunctionType;
                    if (funcType == null)
                        throw new InternalFailure($"Expected function type {node}");
                    if (funcType.ReturnType.Tuple.Count == 0 && node.Children.Count == 0)
                        return; // nothing to do
                    CheckAssignments(node,funcType.ReturnType.Tuple, GetTypes(node.Children[0].Children));
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

        static List<InternalType> GetTypes(List<Ast> nodes)
        {
            return nodes.Select(c => c.Type).ToList();
        }

        // ensure function declarations that need one end with a return statement 
        // 
        void ProcessFunctionDeclaration(FunctionDeclarationAst node)
        {

            var funcType = node.Symbol.Type as FunctionType;
            if (funcType == null)
                throw new InternalFailure($"Expected function type {node}");

            funcType.CallStackReturnSize = TypeHelper.FlattenTypes(funcType.ReturnType.Tuple, env, mgr).Count;

            if (node.ImportToken != null)
                return;
            if (node.Children.Count != 3 || !(node.Children[2] is BlockAst))
                throw new InternalFailure($"Function {node} has wrong structure");


            var funcReturnTypes = funcType.ReturnType.Tuple;
            if (funcReturnTypes.Count == 0)
                return; // nothing to do here - any actual return statements checked elsewhere

            var block = (BlockAst) (node.Children[2]);
            if (!(block.Children.LastOrDefault() is JumpStatementAst))
                env.Error($"Function {node} requires a return statement at end.");
        }

        // return number of simple types in expanded left and right if equal, else -1
        int CheckAssignments(Ast node, List<Ast> left, List<Ast> right)
        {
            return CheckAssignments(node, TypeHelper.FlattenTypes(left, env, mgr), TypeHelper.FlattenTypes(right, env, mgr));
        }

        // check listf of types have same number of items and types match
        // return number of simple types in expanded left and right if equal, else -1
        int CheckAssignments(Ast node, List<InternalType> left, List<InternalType> right)
        {
            if (left.Count != right.Count)
            {
                env.Error($"Mismatched number of expressions ({right.Count}) to assignments ({left.Count}): {node}");
                return -1;
            }
            for (var i = 0; i < left.Count; ++i)
            {
                // todo - check type exists
                if (left[i] == null || left[i] != right[i])
                    env.Error($"Types at position {i + 1} mismatched in assignment {node}");
            }
            return left.Count;
        }

        // check var type exists
        // check variable type exists, counts and types match
        void ProcessAssignment(AssignStatementAst node)
        {
            // todo - check type symbols exist for user defined
            // todo - allow assigning list of items to structures...

            if (node.Children.Count == 1 &&
                (node.Token.TokenType == TokenType.Increment || node.Token.TokenType == TokenType.Decrement))
            {
                // inc or dec only has one child
                node.StackCount = 1;
                // set symbols
                var asn = node.Children[0].Children[0] as TypedItemAst;
                var simple = asn?.Type as SimpleType;
                if (simple == null || simple.SymbolType != SymbolType.Int32)
                    env.Error($"Auto increment or decrement on non-integral type {node}");
                if (asn != null && asn.Symbol == null)
                    asn.Symbol = mgr.Lookup(asn.Name);
                return;
            }

            if (node.Children.Count != 2)
                throw new InternalFailure($"Expected 2 children {node}");

            var items = node.Children[0] as TypedItemsAst;
            var exprs = node.Children[1] as ExpressionListAst;
            if (items == null || exprs == null)
                throw new InternalFailure($"Variable nodes incorrect {node}");
            node.StackCount = CheckAssignments(node, items.Children, exprs.Children);
            // set symbols
            foreach (var item in items.Children)
            {
                var asn = item as TypedItemAst;
                if (asn != null && asn.Symbol == null)
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
            // apply transform to create variables on one line, then assign the next
            // makes later expression parsing work correctly with arrays
            var varItems = node.Children[0] as TypedItemsAst;
            var varExprs = node.Children[1] as ExpressionListAst;
            if (varItems == null || varExprs == null)
                throw new InternalFailure($"Variable nodes incorrect {node}");

            var assign = new AssignStatementAst(new Token(TokenType.Equals, "=", node.Token.Position, node.Token.Filename));
            var asnItems = new TypedItemsAst();
            assign.AddChild(asnItems);
            assign.AddChild(varExprs);
            node.Children.Remove(varExprs); // remove old one

            // attach assignment to parent
            node.Parent.Children.Insert(node.Parent.Children.IndexOf(node)+1,assign);
            assign.Parent = node.Parent;

            foreach (var var in varItems.Children)
            {
                if (!(var is TypedItemAst))
                    throw new InternalFailure($"Expected TypedItemAst, got {var}");
                var ch = var as TypedItemAst;
                asnItems.AddChild(
                    new TypedItemAst(ch.Token,ch.BaseTypeToken)
                    {
                        Symbol = ch.Symbol,
                        Type = ch.Type
                    }
                    );
            }
            ProcessAssignment(assign);
        }

        static Int32 ParseInt(Ast node)
        {
            var text = node.Name;
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
                node.IntValue = ParseInt(node);
            else if (t == TokenType.HexadecimalLiteral)
                node.IntValue = ParseInt(node);
            else if (t == TokenType.BinaryLiteral)
                node.IntValue = ParseInt(node);
            else if (t == TokenType.FloatLiteral)
            {
                double v;
                if (!Double.TryParse(node.Name, out v))
                    env.Error($"Invalid floating point value {node}");
                else
                    node.FloatValue = v;
            }
            else if (t == TokenType.True)
                node.BoolValue = true;
            else if (t == TokenType.False)
                node.BoolValue = false;
            else if (t == TokenType.ByteLiteral)
            {
                var v = ParseInt(node);
                if (v < 0 || 255 < v)
                    env.Warning($"Value {node} truncated to byte");
                node.ByteValue = (byte)v;
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
            public readonly TokenType ActionType;
            public readonly SymbolType ValueType;
            public readonly UnaryAction Action;
            public UnaryTableEntry(TokenType actionType, SymbolType valueType, UnaryAction action)
            {
                this.ActionType = actionType;
                this.ValueType = valueType;
                this.Action = action;
            }
        }

        static readonly UnaryTableEntry[] UnaryActionTable =
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
            var childType = child?.Type as SimpleType;
            
            if (childType == null)
                throw new InternalFailure($"Expected simple type, got {child?.Type}");

            foreach (var entry in UnaryActionTable)
            {
                if (entry.ActionType == node.Token.TokenType &&
                    entry.ValueType == childType.SymbolType)
                {
                    // matches, do action if child has a value
                    if (child.HasValue)
                        entry.Action(node, child);
                    node.Type = child.Type;
                    return node.Type;
                }
            }
            env.Error(
                $"Unary operator {node.Name} cannot be applied to type {childType.SymbolType}");
            return null;
        }

        #endregion

        #region BinaryExpressions

        delegate void BinaryAction(ExpressionAst parent, ExpressionAst left, ExpressionAst right);

        // Binary expression table: given type and op, return result or null if illegal
        class BinaryTableEntry
        {
            public readonly TokenType ActionType;
            public readonly SymbolType LeftValueType;
            public readonly SymbolType RightValueType;
            public readonly SymbolType Result;
            public readonly BinaryAction Action;
            public BinaryTableEntry(
                TokenType actionType, 
                SymbolType leftValueType, SymbolType rightValueType,
                BinaryAction action, 
                SymbolType resultType
                )
            {
                this.ActionType = actionType;
                this.LeftValueType = leftValueType;
                this.RightValueType = rightValueType;
                this.Action = action;
                Result = resultType;
            }
        }

        // Binary operators: and types they apply to
        static readonly BinaryTableEntry[] BinaryActionTable =
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
                (n, l, r) => n.BoolValue = l.BoolValue.Value || r.BoolValue.Value,
                SymbolType.Bool
                ),
            new BinaryTableEntry(
                TokenType.LogicalAnd,
                SymbolType.Bool,SymbolType.Bool,
                (n, l, r) => n.BoolValue = l.BoolValue.Value && r.BoolValue.Value,
                SymbolType.Bool
                ),

            // byte,i32     : >>
            new BinaryTableEntry(
                TokenType.RightShift,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue >> r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.RightShift,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue >> r.IntValue,
                SymbolType.Int32
                ),

            // byte,i32     : <<
            new BinaryTableEntry(
                TokenType.LeftShift,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue << r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.LeftShift,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue << r.IntValue,
                SymbolType.Int32
                ),

            // byte,i32     : >>>
            new BinaryTableEntry(
                TokenType.RightRotate,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)RotateRight(l.ByteValue.Value,r.ByteValue.Value,8),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.RightRotate,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = RotateRight(l.IntValue.Value,r.IntValue.Value,32),
                SymbolType.Int32
                ),

            // byte,i32     : <<<
            new BinaryTableEntry(
                TokenType.LeftRotate,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)RotateRight(l.ByteValue.Value,-r.ByteValue.Value,8),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.LeftRotate,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = RotateRight(l.IntValue.Value,-r.IntValue.Value,32),
                SymbolType.Int32
                ),

            // byte,i32     : &
            new BinaryTableEntry(
                TokenType.Ampersand,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue & r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Ampersand,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue & r.IntValue,
                SymbolType.Int32
                ),

            // byte,i32     : |
            new BinaryTableEntry(
                TokenType.Pipe,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue | r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Pipe,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue | r.IntValue,
                SymbolType.Int32
                ),

            // byte,i32     : ^
            new BinaryTableEntry(
                TokenType.Caret,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue ^ r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Caret,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue ^ r.IntValue,
                SymbolType.Int32
                ),

            // byte,i32     : %
            new BinaryTableEntry(
                TokenType.Percent,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue % r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Percent,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue % r.IntValue,
                SymbolType.Int32
                ),

            // byte,i32,r32 : +
            new BinaryTableEntry(
                TokenType.Plus,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue+ r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Plus,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue + r.IntValue,
                SymbolType.Int32
                ),
            new BinaryTableEntry(
                TokenType.Plus,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue + r.FloatValue,
                SymbolType.Float32
                ),
            // byte,i32,r32 : -
            new BinaryTableEntry(
                TokenType.Minus,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue - r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Minus,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue - r.IntValue,
                SymbolType.Int32
                ),
            new BinaryTableEntry(
                TokenType.Minus,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue - r.FloatValue,
                SymbolType.Float32
                ),
            // byte,i32,r32 : *
            new BinaryTableEntry(
                TokenType.Asterix,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue * r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Asterix,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue * r.IntValue,
                SymbolType.Int32
                ),
            new BinaryTableEntry(
                TokenType.Asterix,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue * r.FloatValue,
                SymbolType.Float32
                ),
            // byte,i32,r32 : /
            // todo - check div 0 - will throw exception now
            new BinaryTableEntry(
                TokenType.Slash,
                SymbolType.Byte,SymbolType.Byte,
                (n, l, r) => n.ByteValue = (byte)(l.ByteValue / r.ByteValue),
                SymbolType.Byte
                ),
            new BinaryTableEntry(
                TokenType.Slash,
                SymbolType.Int32,SymbolType.Int32,
                (n, l, r) => n.IntValue = l.IntValue / r.IntValue,
                SymbolType.Int32
                ),
            new BinaryTableEntry(
                TokenType.Slash,
                SymbolType.Float32,SymbolType.Float32,
                (n, l, r) => n.FloatValue = l.FloatValue / r.FloatValue,
                SymbolType.Float32
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

            if (left.Type != right.Type)
            {
                env.Error($"Cannot combine types {left} and {right} via {node}");
                return null;
            }

            foreach (var entry in BinaryActionTable)
            {
                if (entry.ActionType == node.Token.TokenType &&
                    entry.LeftValueType  == CodeGeneratorVisitor.GetSymbolType(left) &&
                    entry.RightValueType == CodeGeneratorVisitor.GetSymbolType(right)
                    )
                {
                    // it matches, do action if children have values
                    if (left.HasValue && right.HasValue)
                        entry.Action(node, left, right);
                    node.Type = mgr.TypeManager.GetType(entry.Result);
                    return node.Type;
                }
            }
            env.Error($"Binary operator {node} cannot be applied to left {left} and {right}");
            return null;
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
