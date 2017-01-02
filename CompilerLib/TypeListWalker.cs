using System.Collections.Generic;
using System.Linq;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{

    static class TypeHelper
    {
        // given a list of types, flatten to simple types
        // cannot have unsized arrays
        internal static List<InternalType> FlattenTypes(List<InternalType> types, Environment env, SymbolTableManager mgr)
        {
            // todo - cache with type or symbol?
            var flat = new List<InternalType>();
            foreach (var type in types)
            {
                if (type is ArrayType)
                {
                    env.Error($"Cannot have abstract array here : {type}. Wrap it in a type.");
                    return null;
                }
                RecurseFlatten(flat, 1, type, mgr);
            }
            return flat;
        }

        // given a list of nodes, return a list of ordered, flattened types
        // where flattened means down to basic types // i32,bool, r32, string, byte
        internal static List<InternalType> FlattenTypes(List<Ast> nodes, Environment env, SymbolTableManager mgr)
        {
            //todo - cache with type or symbol?
            var flat = new List<InternalType>();
            foreach (var node in nodes)
            {
                if (!(node is ExpressionAst))
                {
                    env.Error($"Node {node} is not an Expression");
                    return null;
                }
                var expr = node as ExpressionAst;
                SymbolEntry symbol;
                InternalType baseType;
                int numCopies;
                DecomposeExpr(expr, out symbol, out baseType, out numCopies);
                RecurseFlatten(flat, numCopies, baseType, mgr);
            }
            return flat;
        }

        // given an expression, find the symbol name, the base type, and the number of copies 
        // takes into account full or partial array declared
        static void DecomposeExpr(ExpressionAst expr, out SymbolEntry symbol, out InternalType baseType, out int numCopies)
        {
            var node = expr;
            var skipDimensions = 0;
            while (node is ArrayAst)
            {
                node = (ExpressionAst) node.Children[1];
                skipDimensions++;
            }
            symbol = node.Symbol;

            baseType = BaseType(expr.Type);
            numCopies = NumItems(symbol, skipDimensions);
        }

        static InternalType BaseType(InternalType type)
        {
            if (type is ArrayType)
                return ((ArrayType)type).BaseType;
            return type;
        }

        // given a symbol, return the number of items due to array, taking into account multiple dimensions
        // skip 1 or more array dimensions if they are already specified
        static int NumItems(SymbolEntry symbol, int skip = 0)
        {
            var num = 1;
            if (symbol?.ArrayDimensions != null)
            {
                for (var i = skip; i < symbol.ArrayDimensions.Count; ++i)
                    num *= symbol.ArrayDimensions[i];
            }
            return num;
        }

        // Given some types
        static void RecurseFlatten(List<InternalType> flat, int num, InternalType type, SymbolTableManager mgr)
        {
            // t is simple, user, or tuple

            if (type is SimpleType)
            {
                for (var i = 0; i < num; ++i)
                    flat.Add(type);
            }
            else if (type is UserType)
            {
                var table = mgr.GetTableWithScope(((UserType)type).Name);
                for (var i = 0; i < num; ++i)
                    foreach (var s1 in table.Entries)
                        RecurseFlatten(flat, NumItems(s1), BaseType(s1.Type), mgr);
            }
            else if (type is TupleType)
            {
                var tuple = ((TupleType)type).Tuple;
                for (var i = 0; i < num; ++i)
                    foreach (var tt in tuple)
                        RecurseFlatten(flat, 1, tt,mgr);
            }
            else
                throw new InternalFailure($"Unsupported type to flatten {type}");
        }
    }

    // walks an expression structure, yields data about sequential items
    // each item is something that can be assigned to
    class TypeListWalker : IEnumerable<TypeListWalker.ItemData>
    {
        ExpressionAst ast;
        SymbolTableManager mgr;
        List<ItemData> items = new List<ItemData>();
        public TypeListWalker(SymbolTableManager mgr, ExpressionAst ast)
        {
            this.mgr = mgr;
            this.ast = ast;
            Recurse(items, ast.Type);
            items.Last().Skip = 0; // end the loop
        }

        void Recurse(List<ItemData> items, InternalType type)
        {
            if (type is SimpleType)
            {
                var simple = (SimpleType)type;
                items.Add(
                    new ItemData
                    {
                        OperandType = CodeGeneratorVisitor.GetOperandType(simple.SymbolType),
                        Skip = 1,
                        SymbolName = ast.Symbol?.Name
                    });
            }
            else if (type is UserType)
            {
                var user = (UserType)type;
                var stbl = mgr.GetTableWithScope(user.Name);
                foreach (var entry in stbl.Entries)
                    Recurse(items, entry.Type);
            }
            else throw new InternalFailure($"Type not convertible yet {ast}");

        }

        public IEnumerator<ItemData> GetEnumerator()
        {
            foreach (var item in items)
                yield return item;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal class ItemData
        {
            public OperandType OperandType = OperandType.None;
            public int Skip = 0;
            public bool More => Skip > 0;
            public string SymbolName;
        }
    }
}
