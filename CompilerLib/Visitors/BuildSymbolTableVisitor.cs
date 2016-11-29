using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // build symbol tables, attached to nodes
    class BuildSymbolTableVisitor
    {
        class SymbolBuilderState
        {
            public Environment environment;
            public SymbolTableManager mgr;

            public SymbolBuilderState(Environment env)
            {
                environment = env;
                mgr = new SymbolTableManager(env);
            }
        }

        public static SymbolTableManager BuildTable(Ast ast, Environment environment)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // attach symbol tables
            var state = new SymbolBuilderState(environment);
            BuildSymbolTable(ast,state);

            state.mgr.Dump(state.environment.Output);

            // todo - compute symbol table item sizes
            // todo - check all items checkable in symbol table

            return state.mgr;
        }

        static void BuildSymbolTable(Ast node, SymbolBuilderState state)
        {

            if (node is TypedItemAst)
                AddTypedItem((TypedItemAst)node, state);
            else if (node is FunctionDeclarationAst)
                AddFunctionDeclSymbols((FunctionDeclarationAst)node, state);
            else if (node is ReturnValuesAst)
                return; // these have no names, not added to symbol table
            else if (node is ParameterListAst)
                return; // these added to symbol table during block
            else if (node is EnumAst)
                state.mgr.AddSymbol(node, ((EnumAst)node).Name, SymbolType.Enum);
            else if (node is EnumValueAst)
                state.mgr.AddSymbol(node, ((EnumValueAst) node).Name, SymbolType.EnumValue);
            else if (node is TypeDeclarationAst)
                state.mgr.AddSymbol(node, ((TypeDeclarationAst) node).Name, SymbolType.UserType);
            else if (node is ModuleAst)
                state.mgr.AddSymbol(node, ((ModuleAst) node).Name, SymbolType.Module);

            // todo - handle: attribute?

            state.mgr.EnterAst(node);

            // if we added a block, see if some special variables need added
            if (node is BlockAst)
                AddBlock(node as BlockAst, state);

            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child, state);


            state.mgr.ExitAst(node);
        }

        // add special vars for a block: function parameters and for loop variables
        static void AddBlock(BlockAst node, SymbolBuilderState state)
        {
            if (node.Parent is ForStatementAst)
            {
                var varName = (node.Parent as ForStatementAst).Token.TokenValue;
                state.mgr.AddSymbol(node.Parent, varName, SymbolType.ToBeResolved);
            }
            else if (node.Parent is FunctionDeclarationAst)
            {
                var par = (node.Parent as FunctionDeclarationAst).Children[1] as ParameterListAst;
                if (par == null) 
                    throw new InternalFailure("Function mismatch in symbol builder AddBlock");
                foreach (var item in par.Children)
                    AddTypedItem(item as TypedItemAst, state);
            }
            
        }

        static void AddFunctionDeclSymbols(FunctionDeclarationAst node, SymbolBuilderState state)
        {
            if (node.Children.Count < 2 || !(node.Children[0] is ReturnValuesAst) || !(node.Children[1] is ParameterListAst))
                throw new InternalFailure("Function internal format mismatched");

            var returnType = ParseTypelist(node.Children[0].Children,state);
            var paramsType = ParseTypelist(node.Children[1].Children,state);

            var attrib = SymbolAttribute.None;
            if (node.ImportToken != null)
                attrib |= SymbolAttribute.Import;
            if (node.ExportToken != null)
                attrib |= SymbolAttribute.Export;

            state.mgr.AddSymbol(node, node.Name, SymbolType.Function,0,attrib,"",returnType,paramsType);
        }

        static List<InternalType> ParseTypelist(List<Ast> nodes, SymbolBuilderState state)
        {
            var list = new List<InternalType>();
            for (var i =0; i < nodes.Count; ++i)
            {
                var node = nodes[i];
                var tItem = node as TypedItemAst;
                if (tItem == null)
                    throw new InternalFailure("Id List internals mismatched");

                var arraySize = 0;
                if (tItem.Children.Any())
                { // for now, only support one array
                    if (tItem.Children.Count != 1 || !(tItem.Children[0] is ArrayAst))
                        throw new InternalFailure("Only one child array supported");
                    arraySize = tItem.Children[0].Children.Count;
                }
                list.Add(
                    state.mgr.TypeManager.GetType(
                    tItem.BaseTypeToken.TokenValue, 
                    arraySize
                    ));

            }
            return list;
        }

        static void AddTypedItem(TypedItemAst node, SymbolBuilderState state)
        {
            var symbolType = state.mgr.GetSymbolType(node.BaseTypeToken.TokenType);

            var userTypename = "";
            if (symbolType == SymbolType.UserType)
                userTypename = node.BaseTypeToken.TokenValue;

            var attrib = SymbolAttribute.None;
            if (node.ConstToken != null)
                attrib |= SymbolAttribute.Const;
            if (node.ImportToken != null)
                attrib |= SymbolAttribute.Import;
            if (node.ExportToken != null)
                attrib |= SymbolAttribute.Export;

            var arrayDimension = 0;
            if (node.Children.Any())
            { // for now, only support one array
                if (node.Children.Count != 1 || !(node.Children[0] is ArrayAst))
                    throw new InternalFailure("Only one child array supported");
                arrayDimension = node.Children[0].Children.Count;
            }

            state.mgr.AddSymbol(node, node.Name, symbolType,arrayDimension, attrib, userTypename,null,null);


        }
    }
}