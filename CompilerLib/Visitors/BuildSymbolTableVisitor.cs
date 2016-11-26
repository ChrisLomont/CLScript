using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    // build symbol tables, attached to nodes
    class BuildSymbolTableVisitor
    {
/*        public static void BuildTable(Ast ast, Environment environment)
        {
            // attach parents for easy tree walking
            AttachParentsVisitor.AttachParents(ast);

            // attach symbol tables
            var moduleName = "";
            BuildSymbolTable(ast,ref moduleName);

            // attach symbol tables to all nodes, link descendants to parents
            //LinkTables(ast);

            //todo - compute symbol table item sizes
        }

        public static void BuildSymbolTable(Ast node, ref string moduleName)
        {
            // actions to take on seeing various node types
            if (node is DeclarationsAst)
                AddSymbolTable(node); // global table first
            else if (node is BlockAst)
            {   // add a symbol table. 
                AddSymbolTable(node); // block scopes get symbol table

                // Some blocks add their parent variable (such as for variables, function parameters)
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
                        AddSymbol(node, funcAst, moduleName, p.Name.TokenValue, SymbolTable.GetSymbolType(p.Type.TokenType));
                        // todo - param can be more complex....
                    }
                }
            }

            else if (node is FunctionDeclarationAst)
            {
                // todo - more complicated....
                AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.Function);
            }
            else if (node is VariableDefinitionAst)
                AddVarDefEntry("",(VariableDefinitionAst)node, moduleName);
            else if (node is ModuleAst)
                moduleName = ((ModuleAst) node).Token.TokenValue;

            if (node is EnumAst)
            {
                var s = AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.Enum);

                foreach (var child in node.Children)
                {
                    var eval = child as EnumValueAst;
                    if (eval == null)
                        throw new InternalFailure("Enum child wrong type");
                    AddSymbol(node, child, moduleName, s.Name + '.' + child.Token.TokenValue, SymbolType.EnumValue);
                }
                return; // do not parse children
            }
            if (node is TypeDeclarationAst)
            {
                AddSymbol(node, node, moduleName, node.Token.TokenValue, SymbolType.UserType);
                foreach (var child in node.Children)
                {
                    var varAst = child as VariableDefinitionAst;
                    if (varAst == null)
                        throw new InternalFailure("Type child incorrect in build symbol table");
                    AddVarDefEntry(node.Token.TokenValue, varAst, moduleName);
                }
                return; // do not parse children
            }
            if (node is ModuleAst)
                moduleName = ((ModuleAst)node).Token.TokenValue;

            foreach (var child in node.Children)
                Recurse(child, table, ref moduleName);
        }


            // todo - handle: attribute, import, export, const

            // recurse
            foreach (var child in node.Children)
                BuildSymbolTable(child, ref moduleName);
        }

        static void AddVarDefEntry(string prefix, VariableDefinitionAst node, string moduleName)
        {
            var ids = node.Children[0] as IdListAst;
            if (ids == null)
                throw new InternalFailure("variable ids not in correct location");
            foreach (var id in ids.Children)
            {
                var name = id.Token.TokenValue;
                if (!String.IsNullOrEmpty(prefix))
                    name = prefix + '.' + name;
                var s = AddSymbol(node, node, moduleName, name,
                    SymbolTable.GetSymbolType(node.Token.TokenType));
                if (id.Children.Any())
                {
                    if (id.Children.Count != 1 || !(id.Children[0] is ArrayAst))
                        throw new InvalidSyntax($"Array malformed {id}");
                    s.ArraySize = id.Children[0].Children.Count;
                }
            }

        }

        static void AddSymbolTable(Ast node)
        {
            if (node.SymbolTable != null)
                throw new InternalFailure("expected null Symbol Table");
            node.SymbolTable = new SymbolTable(node);
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
            if (node != null && node.SymbolTable != null)
                return node.SymbolTable;
            if (node != null && node.Parent != null)
                return FindTable(node.Parent);
            return null;
        }

        // attach symbol tables to all nodes, link descendants to parents
        static void LinkTables(Ast ast)
        {
            if (ast.SymbolTable == null)
                ast.SymbolTable = FindTable(ast);
            else if (ast.Parent != null)
                ast.SymbolTable.Parent = FindTable(ast.Parent);
        }
*/

    }
}