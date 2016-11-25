using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // some basic visitors
    class BuildSymbolTableVisitor
    {
        public static void BuildTable(Ast ast, Environment environment)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // attach symbol tables
            BuildSymbolTable(ast,"");
        }

        public static void BuildSymbolTable(Ast node, string moduleName)
        {
            // actions to take on seeing various node types
            if (node is DeclarationsAst)
                AddSymbolTable(node); // global table first
            else if (node is BlockAst)
            { // add a symbol table. Some blocks add their parent variable (such as for variables, function parameters)
                AddSymbolTable(node); // block scopes get symbol table
                var forAst = node.Parent as ForStatementAst;
                if (forAst != null)
                    AddSymbol(node, forAst, moduleName, forAst.Token.TokenValue, SymbolType.ToBeResolved);
                
                var funcAst = node.Parent as FunctionDeclarationAst;
                if (funcAst != null)
                {
                    var paramsAst = funcAst.Children[1] as ParameterListAst;
                    if (paramsAst == null)
                        throw new InternalFailure("Function parameters in wrong spot for symbol table");
                    foreach (var param in paramsAst.Children)
                    {
                        var p = param as ParameterAst;
                        if (p == null)
                            throw new InternalFailure("parameter in wrong spot in symbol table");
                        AddSymbol(node, funcAst, moduleName, p.Name.TokenValue, SymbolType.ToBeResolved);
                    }
                }
            }

            else if (node is FunctionDeclarationAst)
                AddSymbol(node,node, moduleName, node.Token.TokenValue, SymbolType.Function);
            else if (node is EnumAst)
                AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.Enum);
            else if (node is EnumValueAst)
                AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.EnumValue);
            else if (node is TypeDeclarationAst)
                AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.UserType);
            else if (node is VariableDefinitionAst)
            {
                var ids = node.Children[0] as IdListAst;
                if (ids == null)
                    throw new InternalFailure("variable ids not in correct location");
                foreach (var id in ids.Children)
                    AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.ToBeResolved);
            }
            else if (node is ModuleAst)
                moduleName = ((ModuleAst) node).Token.TokenValue;

            // todo - handle: attribute

            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child, moduleName);
        }

        static void AddSymbolTable(Ast node)
        {
            if (node.SymbolTable != null)
                throw new InternalFailure("expected null Symbol Table");
            node.SymbolTable = new SymbolTable(node);
        }
        
        // given a node defining a symbol, a name to add, and a symbol type, add the
        // symbol to the first parent with a symbol table
        static void AddSymbol(Ast tableNode, Ast node, string moduleName, string name, SymbolType symbolType)
        {
            var table = FindTable(tableNode);
            if (table == null)
                throw new InternalFailure("Symbol table missing");
            table.AddSymbol(node,moduleName, name,symbolType);
        }
        
        // given a node, find the symbol table here or at the first ancestor
        static SymbolTable FindTable(Ast node)
        {
            if (node != null && node.SymbolTable != null)
                return node.SymbolTable;
            if (node != null && node.Parent != null)
                return FindTable(node.Parent);
            return null;
        }

    }
}