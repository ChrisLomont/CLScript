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
            public Environment env;
            public SymbolTable table;
            public ScopeManager scope;

            public SymbolBuilderState(Environment end)
            {
                env = end;
                table = new SymbolTable();
                scope = new ScopeManager();
            }
        }

        public static void BuildTable(Ast ast, Environment environment)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // attach symbol tables
            var state = new SymbolBuilderState(environment);
            BuildSymbolTable(ast,state);

            state.table.Dump(environment.Output);

            //todo - compute symbol table item sizes
        }

        static void BuildSymbolTable(Ast node, SymbolBuilderState state)
        {

            if (node is VariableDefinitionAst)
                AddVariableDeclSymbols((VariableDefinitionAst)node, state);
            else if (node is FunctionDeclarationAst)
                AddFunctionDeclSymbols((FunctionDeclarationAst)node, state);
            else if (node is ParameterAst)
                state.table.AddSymbol(node, state.scope.Scope, ((ParameterAst)node).Name.TokenValue,GetParameterType(node as ParameterAst));
            else if (node is EnumAst)
                state.table.AddSymbol(node, state.scope.Scope, ((EnumAst) node).Name, SymbolType.Enum);
            else if (node is EnumValueAst)
                state.table.AddSymbol(node, state.scope.Scope, ((EnumValueAst) node).Name, SymbolType.EnumValue);
            else if (node is TypeDeclarationAst)
                state.table.AddSymbol(node, state.scope.Scope, ((TypeDeclarationAst) node).Name, SymbolType.UserType);
            else if (node is ModuleAst)
                state.table.AddSymbol(node, state.scope.Scope, ((ModuleAst) node).Name, SymbolType.Module);

            // todo - handle: attribute, import, export, const

            state.scope.Enter(node);
            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child, state);
            state.scope.Exit(node);
        }

        static List<SymbolType> GetParameterType(ParameterAst item)
        {
            var types = new List<SymbolType>();
            types.Add(SymbolTable.GetSymbolType(item.Type.TokenType));
            var arrayDepth = item.ArrayDepth;
            if (arrayDepth > 0)
                types.Add((SymbolType)((int)SymbolType.Array) + arrayDepth - 1);
            return types;
        }

        static void AddFunctionDeclSymbols(FunctionDeclarationAst node, SymbolBuilderState state)
        {
            // function type is params -> ret types. Construct list of these types
            var types = new List<SymbolType>();

            // get parameter types
            foreach (var item in node.Children[1].Children)
            {
                if (!(item is ParameterAst))
                    throw new InternalFailure("Symbol builder expected ParameterAst in return types");
                types.AddRange(GetParameterType(item as ParameterAst));
            }
            
            // add the function mapping
            types.Add(SymbolType.Function);

            // get return types
            foreach (var item in node.Children[0].Children)
            {
                if (!(item is TypeAst))
                    throw new InternalFailure("Symbol builder expected TypeAst in return types");
                types.Add(SymbolTable.GetSymbolType(item.Token.TokenType));
            }

            // add item
            state.table.AddSymbol(node, state.scope.Scope, node.Name, types);
        }

        static void AddVariableDeclSymbols(VariableDefinitionAst node, SymbolBuilderState state)
        {
            var ids = node.Children[0] as IdListAst;
            if (ids == null)
                throw new InternalFailure("variable ids not in correct location");
            foreach (var id in ids.Children)
            {
                if (!(id is IdentifierAst))
                    throw new InternalFailure("Expected IdentifierAst");
                var symbol = state.table.AddSymbol(node, state.scope.Scope, (id as IdentifierAst).Name, SymbolTable.GetSymbolType(node.Token.TokenType));
                if (id.Children.Any())
                {
                    if (id.Children.Count != 1 || !(id.Children[0] is ArrayAst))
                        throw new InvalidSyntax($"Array malformed {id}");
                    symbol.Types.Add((SymbolType)((int)SymbolType.Array+id.Children.Count-1));
                }
            }
        }

        static void AddSymbolTable(Ast node)
        {
            //if (node.SymbolTable != null)
                throw new InternalFailure("expected null Symbol Table");
            //node.SymbolTable = new SymbolTable(node);
        }
        
        // given a node defining a symbol, a name to add, and a symbol type, add the
        // symbol to the first parent with a symbol table
        static SymbolEntry AddSymbol(Ast tableNode, Ast node, string moduleName, string name, SymbolType symbolType)
        {
            var table = FindTable(tableNode);
            if (table == null)
                throw new InternalFailure("Symbol table missing");
            return table.AddSymbol(node, moduleName, name, symbolType);
        }

        // given a node, find the symbol table here or at the first ancestor
        static SymbolTable FindTable(Ast node)
        {
           // if (node != null && node.SymbolTable != null)
            //    return node.SymbolTable;
            if (node != null && node.Parent != null)
                return FindTable(node.Parent);
            return null;
        }

    }
}