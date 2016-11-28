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
                mgr = new SymbolTableManager();
            }
        }

        public static void BuildTable(Ast ast, Environment environment)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // attach symbol tables
            var state = new SymbolBuilderState(environment);
            BuildSymbolTable(ast,state);

            state.mgr.Dump(state.environment.Output);

            // todo - compute symbol table item sizes
            // todo - check all items checkable in symbol table

        }

        static void BuildSymbolTable(Ast node, SymbolBuilderState state)
        {

            if (node is TypedItemAst)
                AddTypedItem((TypedItemAst)node, state);
            else if (node is FunctionDeclarationAst)
                AddFunctionDeclSymbols((FunctionDeclarationAst)node, state);
            else if (node is EnumAst)
                state.mgr.AddSymbol(node, ((EnumAst)node).Name, SymbolType.Enum);
            else if (node is EnumValueAst)
                state.mgr.AddSymbol(node, ((EnumValueAst) node).Name, SymbolType.EnumValue);
            else if (node is TypeDeclarationAst)
                state.mgr.AddSymbol(node, ((TypeDeclarationAst) node).Name, SymbolType.UserType);
            else if (node is ModuleAst)
                state.mgr.AddSymbol(node, ((ModuleAst) node).Name, SymbolType.Module);

            // todo - handle: attribute, import, export, const
            // todo - handle block - if func or for, add the variable in it

            state.mgr.EnterAst(node);
            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child, state);
            state.mgr.ExitAst(node);
        }

        static void AddFunctionDeclSymbols(FunctionDeclarationAst node, SymbolBuilderState state)
        {
            // todo
        }

        static void AddTypedItem(TypedItemAst node, SymbolBuilderState state)
        {
            var symbolType = state.mgr.GetSymbolType(node.BaseTypeToken.TokenType);
            var s = state.mgr.AddSymbol(node, node.Name, symbolType);
            if (symbolType == SymbolType.UserType)
                s.AddUserType(node.BaseTypeToken.TokenValue);
            if (node.Children.Any())
            { // for now, only support one array
                if (node.Children.Count != 1 || !(node.Children[0] is ArrayAst))
                    throw new InternalFailure("Only one child array supported");
                s.AddArraySize(node.Children[0].Children.Count);
            }
        }
    }
}