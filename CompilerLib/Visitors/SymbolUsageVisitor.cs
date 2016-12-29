using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    class SymbolUsageVisitor
    {
        Environment env;
        SymbolTableManager mgr;

        public SymbolUsageVisitor(Environment environment)
        {
            env = environment;
        }

        public void Check(SymbolTableManager symbolTable, Ast ast)
        {

            symbolTable.Start();
            mgr = symbolTable;
            Recurse(ast);

            // do final top level functions and type members
            foreach (var ch in ast.Children)
            {
                if (ch is FunctionDeclarationAst)
                    TagFunctionDeclaration((FunctionDeclarationAst) ch);
                if (ch is TypeDeclarationAst)
                    TagTypeDeclaration((TypeDeclarationAst)ch);
            }

            OutputUnusedSymbols(mgr.RootTable);
        }

        void TagTypeDeclaration(TypeDeclarationAst node)
        {
            var symbol = mgr.Lookup(node.Name);
            //mgr.EnterAst(node);

            // todo - this is too agressive - unused members are tagged
            // better would be to tag on use, taking care for types used in import/export
            if (symbol.Used)
            {
                foreach (var ch in node.Children)
                    ((TypedItemAst) ch).Symbol.Used = true;
            }

            //mgr.ExitAst(node);
        }

        void OutputUnusedSymbols(SymbolTable table)
        {
            // needs to mark line where unused item declared - add to symbol table
            foreach (var e in table.Entries)
                if (!e.Used)
                    env.Warning($"Unused symbol: {e}");
            foreach (var child in table.Children)
                OutputUnusedSymbols(child);
        }


        void Recurse(Ast node)
        {
            var recurseChildren = true;
            mgr.EnterAst(node);

            // pre recurse checking
            if (node is ExpressionListAst)
            {
                foreach (var child in node.Children)
                    TagExpression((ExpressionAst) child);
                recurseChildren = false;
            }
            else if (node is ForStatementAst)
                ((ForStatementAst) node).VariableSymbol.Used = true;
            else if (node is FunctionCallAst)
            {
                ((FunctionCallAst) node).Symbol.Used = true;
                foreach (var child in node.Children)
                    TagExpression((ExpressionAst)child);
                recurseChildren = false;
            }

            if (recurseChildren)
            {
                // recurse children
                foreach (var child in node.Children)
                    Recurse(child);
            }

            // post recurse checking


            mgr.ExitAst(node);

        }

        // recurse expression, tagging symbols
        void TagExpression(ExpressionAst node)
        {
            if (node is FunctionCallAst)
                node.Symbol.Used = true;
            else if (node.Children.Count == 2)
            {
                TagExpression(node.Children[0] as ExpressionAst);
                TagExpression(node.Children[1] as ExpressionAst);
            }
            else if (node.Children.Count == 1)
            {
                TagExpression(node.Children[0] as ExpressionAst);
            }
            else if (node is TypedItemAst)
            {
                node.Symbol.Used = true;
                if (node.Symbol.Type is UserType)
                    mgr.Lookup((node.Symbol.Type as UserType).Name).Used = true;
            }
            else if (node.Children.Count != 0)
                throw new InternalFailure($"Expression must have 0 to 2 children! {node}");
        }

        void TagFunctionDeclaration(FunctionDeclarationAst node)
        {
            // if used, tag all parameters
            // if exported, tag all parameters
            var symbol = node.Symbol;
            if (symbol.Used || node.ExportToken != null)
            {
                symbol.Used = true;
                var funcType = symbol.Type as FunctionType;
                var types = funcType.ReturnType.Tuple;
                types.AddRange(funcType.ParamsType.Tuple);
                foreach (var t in types)
                    if (t is UserType)
                    {
                        var typeSymbol = mgr.Lookup((t as UserType).Name);
                        typeSymbol.Used = true;
                    }

            }
        }
    }
}
