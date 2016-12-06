using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // build symbol tables, attached to nodes
    class BuildSymbolTableVisitor
    {
        public Environment environment;
        public SymbolTableManager mgr;

        public BuildSymbolTableVisitor(Environment environment1)
        {
            environment = environment1;
            mgr = new SymbolTableManager(environment);
        }

        public SymbolTableManager BuildTable(Ast ast)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // attach symbol tables
            BuildSymbolTable(ast);

            // todo - compute symbol table item sizes
            // todo - check all items checkable in symbol table

            return mgr;
        }

        void BuildSymbolTable(Ast node)
        {

            if (node is TypedItemAst)
                AddTypedItem((TypedItemAst)node, NodeVariableType(node));
            else if (node is FunctionDeclarationAst)
                AddFunctionDeclSymbols((FunctionDeclarationAst)node);
            else if (node is ReturnValuesAst)
                return; // these have no names, not added to symbol table
            else if (node is ParameterListAst)
                return; // these added to symbol table during block
            else if (node is EnumAst)
                mgr.AddSymbol(node, ((EnumAst)node).Name, SymbolType.Enum, VariableUse.None);
            else if (node is EnumValueAst)
                (node as EnumValueAst).Symbol = mgr.AddSymbol(node, ((EnumValueAst) node).Name, SymbolType.EnumValue, VariableUse.Const);
            else if (node is TypeDeclarationAst)
                mgr.AddSymbol(node, ((TypeDeclarationAst) node).Name, SymbolType.Typedef, VariableUse.None);
            else if (node is ModuleAst)
                mgr.AddSymbol(node, ((ModuleAst) node).Name, SymbolType.Module, VariableUse.None);

            // todo - handle: attribute?

            mgr.EnterAst(node);

            // if we added a block, see if some special variables need added
            if (node is BlockAst)
                AddBlock(node as BlockAst);

            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child);

            mgr.ExitAst(node);
        }

        // determine use of this variable
        static VariableUse NodeVariableType(Ast node)
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
                var forNode = node.Parent as ForStatementAst;
                var varName = forNode.Token.TokenValue;
                var symbol = mgr.AddSymbol(node.Parent, varName, SymbolType.ToBeResolved, VariableUse.ForLoop);
                forNode.VariableSymbol = symbol;
            }
            else if (node.Parent is FunctionDeclarationAst)
            {
                var par = (node.Parent as FunctionDeclarationAst).Children[1] as ParameterListAst;
                if (par == null) 
                    throw new InternalFailure("Function mismatch in symbol builder AddBlock");
                foreach (var item in par.Children)
                    AddTypedItem(item as TypedItemAst, VariableUse.Param);
            }
            
        }

        List<InternalType> ParseTypelist(List<Ast> nodes)
        {
            var list = new List<InternalType>();
            for (var i = 0; i < nodes.Count; ++i)
            {
                var node = nodes[i];
                var tItem = node as TypedItemAst;
                if (tItem == null)
                    throw new InternalFailure("Id List internals mismatched");

                list.Add(GetTypedItemType1(tItem));
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

            mgr.AddSymbol(node, node.Name, SymbolType.Function, VariableUse.Param, null, attrib,"",returnType,paramsType);
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


        // helper function that gets needed items to create types based on a TypedItemAst
        InternalType GetTypedItemType1(TypedItemAst node)
        {
            // typed item has variable name and type name

            var symbolType = GetSymbolType(node.BaseTypeToken.TokenType);
            var userName = "";
            if (symbolType == SymbolType.ToBeResolved)
            { // replace with type name
                symbolType = SymbolType.UserType1;
                userName = node.BaseTypeToken.TokenValue;
            }

            List<int> arrayDimensions = null;
            if (node.Children.Any())
            { // for now, only support one array
                arrayDimensions = new List<int>();
                foreach (var child in node.Children)
                {
                    var arrChild = child as ArrayAst;
                    if (arrChild == null || arrChild.Children.Count != 1)
                        throw new InternalFailure($"Array formed incorrectly {node}");
                    var exprChild = arrChild.Children[0] as ExpressionAst;
                    
                    // todo - eval const, enum, expr before this.... or do in semantic.... or how to do?
                    if (exprChild is LiteralAst)
                        SemanticAnalyzerVisitor.ProcessLiteral(exprChild as LiteralAst, environment);
                    if (exprChild == null || !exprChild.HasValue || !exprChild.IntValue.HasValue)
                        environment.Error($"Array size {arrChild} not constant");
                    else
                        arrayDimensions.Add(exprChild.IntValue.Value);
                }
            }

            return mgr.TypeManager.GetType(symbolType, arrayDimensions,userName);
        }

        void AddTypedItem(TypedItemAst node, VariableUse usage)
        {

            var attrib = SymbolAttribute.None;
            if (node.ConstToken != null)
                attrib |= SymbolAttribute.Const;
            if (node.ImportToken != null)
                attrib |= SymbolAttribute.Import;
            if (node.ExportToken != null)
                attrib |= SymbolAttribute.Export;

            var itemType = GetTypedItemType1(node);
            var s  = mgr.AddSymbol(node, 
                node.Name,
                itemType.SymbolType,
                usage,
                itemType.ArrayDimensions,
                attrib, 
                itemType.UserTypeName, 
                null, null);
            node.Symbol = s;
        }
    }
}