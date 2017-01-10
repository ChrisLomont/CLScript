using System.Collections.Generic;
using System.Linq;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // build symbol tables, attached to nodes
    // attaches types when possible to nodes
    class BuildSymbolTableVisitor
    {
        readonly Environment env;
        readonly SymbolTableManager mgr;

        public BuildSymbolTableVisitor(Environment environment)
        {
            env = environment;
            mgr = new SymbolTableManager(env);
        }

        public SymbolTableManager BuildTable(Ast ast)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // walk global scope once, getting enums, modules, types, and function declarations
            ResolveGlobals(ast);

            // attach symbol tables
            BuildSymbolTable(ast);

            // todo - compute symbol table item sizes
            // todo - check all items checkable in symbol table

            // ensure all types defined
            foreach (var t in mgr.TypeManager.Types.Where(t1=>t1 is UserType).Select(t2=>(UserType)t2))
            {
                if (mgr.Lookup(t.Name) == null)
                    env.Error($"Undefined type {t}");
            }
            return mgr;
        }

        void ResolveGlobals(Ast ast)
        {
            foreach (var node in ast.Children)
            {
                if (node is FunctionDeclarationAst)
                    AddFunctionDeclSymbols((FunctionDeclarationAst)node);
                else if (node is EnumAst)
                    mgr.AddTypeSymbol(node, ((EnumAst) node).Name, SymbolType.EnumType);
                else if (node is TypeDeclarationAst)
                    mgr.AddTypeSymbol(node, ((TypeDeclarationAst) node).Name, SymbolType.UserType);
                else if (node is ModuleAst)
                    mgr.AddTypeSymbol(node, ((ModuleAst) node).Name, SymbolType.Module);
            }
        }

        void BuildSymbolTable(Ast node)
        {

            if (node is TypedItemAst)
                AddTypedItem((TypedItemAst)node, NodeVariableType(node));
            //else if (node is FunctionDeclarationAst)
            //    AddFunctionDeclSymbols((FunctionDeclarationAst)node);
            else if (node is ReturnValuesAst)
                return; // these have no names, not added to symbol table
            else if (node is ParameterListAst)
                return; // these added to symbol table during function block
            //else if (node is EnumAst)
            //    mgr.AddSymbol(node, ((EnumAst)node).Name, SymbolType.Enum, VariableUse.None);
            else if (node is EnumValueAst)
            {
                var type = mgr.TypeManager.GetType(SymbolType.EnumValue);
                ((EnumValueAst) node).Symbol = mgr.AddVariableSymbol(node, ((EnumValueAst) node).Name, type, VariableUse.Const);
            }

            //else if (node is TypeDeclarationAst)
            //    mgr.AddSymbol(node, ((TypeDeclarationAst) node).Name, SymbolType.Typedef, VariableUse.None);
            //else if (node is ModuleAst)
            //    mgr.AddSymbol(node, ((ModuleAst) node).Name, SymbolType.Module, VariableUse.None);

            // todo - handle: attributes?

            mgr.EnterAst(node);

            // if we added a block, see if some special variables need added
            if (node is BlockAst)
                AddBlock((BlockAst) node);

            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child);

            mgr.ExitAst(node);
        }

        // determine use of this variable
        public static VariableUse NodeVariableType(Ast node)
        {
            var p = node.Parent;
            while (p != null)
            {
                if (p is TypeDeclarationAst)
                    return VariableUse.Member;
                if (p is FunctionDeclarationAst)
                    return VariableUse.Param;
                if (p is BlockAst)
                    return VariableUse.Local;
                if (p is DeclarationsAst)
                    return VariableUse.Global;
                p = p.Parent;
            }
            throw new InternalFailure($"Could not determine variable usage {node}");
        }

        // add special vars for a block: function parameters and for loop variables
        void AddBlock(BlockAst node)
        {
            if (node.Parent is ForStatementAst)
            {
                var forNode = (ForStatementAst) node.Parent;
                var varName = forNode.Name;
                var symbol = mgr.AddVariableSymbol(node.Parent, varName, mgr.TypeManager.GetType(SymbolType.ToBeResolved), VariableUse.ForLoop);
                forNode.VariableSymbol = symbol;
            }
            else if (node.Parent is FunctionDeclarationAst)
            {
                var funcAst = (FunctionDeclarationAst) node.Parent;
                funcAst.SymbolTable = mgr.SymbolTable;
                funcAst.Symbol = mgr.Lookup(funcAst.Name);
                var par = ((FunctionDeclarationAst) node.Parent).Children[1] as ParameterListAst;
                if (par == null) 
                    throw new InternalFailure("Function mismatch in symbol builder AddBlock");
                foreach (var item in par.Children)
                    AddTypedItem(item as TypedItemAst, VariableUse.Param);
            }
        }

        // get list of types, used for function parameter and return lists
        List<InternalType> ParseTypelist(List<Ast> nodes)
        {
            var list = new List<InternalType>();
            foreach (var node in nodes)
            {
                var tItem = node as TypedItemAst;
                if (tItem == null)
                    throw new InternalFailure("Id List internals mismatched");

                List<int> arrayDimensions; // results tossed
                list.Add(GetTypedItemType(tItem, out arrayDimensions));
            }
            return list;
        }

        void AddFunctionDeclSymbols(FunctionDeclarationAst node)
        {
            if (node.Children.Count < 2 || !(node.Children[0] is ReturnValuesAst) || !(node.Children[1] is ParameterListAst))
                throw new InternalFailure("Function internal format mismatched");

            var returnType = ParseTypelist(node.Children[0].Children);
            var paramsType = ParseTypelist(node.Children[1].Children);

            var attrib = SymbolAttribute.None;
            if (node.ImportToken != null)
                attrib |= SymbolAttribute.Import;
            if (node.ExportToken != null)
                attrib |= SymbolAttribute.Export;

            var symbol = mgr.AddFunctionSymbol(node, attrib,returnType,paramsType);
            node.Symbol = symbol;
        }


        /// <summary>
        /// use this to look up single built in types
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        public static SymbolType GetSymbolType(TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.Int32:
                    return SymbolType.Int32;
                case TokenType.Float32:
                    return SymbolType.Float32;
                case TokenType.String:
                    return SymbolType.String;
                case TokenType.Byte:
                    return SymbolType.Byte;
                case TokenType.Bool:
                    return SymbolType.Bool;
                case TokenType.Identifier:
                    return SymbolType.ToBeResolved; // todo - can be new type or enum, or use of a type
                default:
                    throw new InternalFailure($"Unknown symbol type {tokenType}");
            }
        }

        public static InternalType GetType(Token token, SymbolTableManager mgr)
        {
            var symbolType = GetSymbolType(token.TokenType);
            if (symbolType != SymbolType.ToBeResolved)
                return mgr.TypeManager.GetType(symbolType);
            else
            {
                var symbol = mgr.Lookup(token.TokenValue);
                return symbol?.Type;
            }
        }

        // helper function that gets needed items to create types based on a TypedItemAst
        InternalType GetTypedItemType(TypedItemAst node, out List<int> arrayDimensions)
        {
            arrayDimensions = null;

            // typed item has variable name and type name
            var baseType = GetType(node.BaseTypeToken, mgr);
            if (baseType == null)
                env.Error($"Cannot find symbol for token {node.BaseTypeToken}");

//            var symbolType = GetSymbolType(node.BaseTypeToken.TokenType);
//            if (symbolType != SymbolType.ToBeResolved)
//                baseType = mgr.TypeManager.GetType(symbolType);
//            else
//            {
//                var symbol = mgr.Lookup(node.BaseTypeToken.TokenValue);
//                baseType = null;
//                if (symbol == null)
//                    env.Error($"Cannot find symbol for token {node.BaseTypeToken}");
//                else
//                    baseType = symbol.Type;
//            }
//
            if (node.Children.Any())
            {
                Ast arrayAst = node.Children[0] as ArrayAst;
                // walk down array list to get dimensions
                while (true)
                {
                    if (arrayAst == null || 2 < arrayAst.Children.Count)
                        throw new InternalFailure($"Array formed incorrectly {node}");
                    if (arrayDimensions == null)
                        arrayDimensions = new List<int>();

                    // some arrays have size, such as declarations, others have no size specified, such as function parameters
                    // children can therefore be: expression , expression + array , array, none

                    // get dimension (1 if none present)
                    var dim = 0;
                    var count = arrayAst.Children.Count;
                    if (count > 0 && !(arrayAst.Children[0] is ArrayAst))
                    {
                        // not array, must be an constant expression
                        var exprChild = arrayAst.Children[0] as ExpressionAst;

                        // todo - eval const, enum, expr before this.... or do in semantic.... or how to do?
                        if (exprChild is LiteralAst)
                            SemanticAnalyzerVisitor.ProcessLiteral(exprChild as LiteralAst, env);

                        if (exprChild == null || !exprChild.HasValue || !exprChild.IntValue.HasValue)
                            env.Error($"Array size {arrayAst} not constant");
                        else
                            dim = exprChild.IntValue.Value;
                    }
                    else
                        dim = 1; // todo - check parent hits a FunctionDeclarationAst before a BlockAst

                    arrayDimensions.Add(dim);

                    // get next array if present
                    if (count == 1 && arrayAst.Children[0] is ArrayAst)
                        arrayAst = (ArrayAst) arrayAst.Children[0];
                    else if (count == 2 && arrayAst.Children[1] is ArrayAst)
                        arrayAst = (ArrayAst) arrayAst.Children[1];
                    else
                        break; // done
                }
            }

            if (arrayDimensions != null)
                return mgr.TypeManager.GetType(arrayDimensions.Count, baseType);
            return baseType;
        }

        void AddTypedItem(TypedItemAst node, VariableUse usage)
        {
            if (node.BaseTypeToken == null)
                return; // type not defined here, must be elsewhere in tree

            var attrib = SymbolAttribute.None;
            if (node.ConstToken != null)
                attrib |= SymbolAttribute.Const;
            if (node.ImportToken != null)
                attrib |= SymbolAttribute.Import;
            if (node.ExportToken != null)
                attrib |= SymbolAttribute.Export;

            List<int> arrayDimensions;
            var totalType = GetTypedItemType(node, out arrayDimensions);

            var s  = mgr.AddVariableSymbol(node, 
                node.Name,
                totalType,
                usage,
                arrayDimensions,
                attrib);

            node.Symbol = s;
            node.Type = totalType;
        }
    }
}