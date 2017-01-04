using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{

    // todo - merge all this into a simpler unit, use both places, and/or cache results
    //        from semantic checking for use in the code generation section

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
        // returns # of dimensions of item already specified before this walk
        // thus, if a is defined as i32 a[1][2][3], and a[0][0] is being walked, returns 2, the number of dimensions already used
        static internal int DecomposeExpr(ExpressionAst expr, out SymbolEntry symbol, out InternalType baseType, out int numCopies)
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
            return skipDimensions;
        }

        /// <summary>
        /// Get the base type of a type, which is
        /// the type itself if not an array, else the
        /// base type of the array
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        static InternalType BaseType(InternalType type)
        {
            if (type is ArrayType)
                return ((ArrayType)type).BaseType;
            return type;
        }

        // given a symbol, return the number of items due to array, taking into account multiple dimensions
        // skip 1 or more array dimensions if they are already specified
        static internal int NumItems(SymbolEntry symbol, int skip = 0)
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
        SymbolEntry symbol;
        InternalType baseType;
        SymbolTableManager mgr;
        Environment env;
        List<ItemData> items = new List<ItemData>();
        public TypeListWalker(SymbolTableManager mgr, ExpressionAst ast, Environment environment)
        {
            this.mgr = mgr;
            this.ast = ast;
            this.env = environment;
            int numCopies;
            var skippedDimensions = TypeHelper.DecomposeExpr(ast, out symbol, out baseType, out numCopies);
            Recurse(items, ast.Type, skippedDimensions, symbol);
            
            // dump skips
            // var sb = new StringBuilder();
            // for (var i = 0; i < items.Count; ++i)
            //     sb.Append($"{items[i].PreAddressIncrement} ");
            // env.Info($"Walk indices {sb}");

            items.Last().Last = true; // stack address not needed after last item
        }

        // first item has skip 0, rest have (default) skip 1, some array operations increase these
        void Recurse(List<ItemData> items, InternalType type, int skippedDimensions, SymbolEntry arraySymbol)
        {
            if (type is SimpleType)
            {
                var simple = (SimpleType)type;
                var skip = items.Any() ? 1 : 0;
                items.Add(
                    new ItemData
                    {
                        OperandType = CodeGeneratorVisitor.GetOperandType(simple.SymbolType),
                        PreAddressIncrement = skip,
                        SymbolName = symbol.Name,
                        Last = false
                    });
            }
            else if (type is UserType)
            {
                var user = (UserType)type;
                var stbl = mgr.GetTableWithScope(user.Name);
                foreach (var entry in stbl.Entries)
                    Recurse(items, entry.Type,0, entry);
            }
            else if (type is ArrayType)
            {
                var arr = (ArrayType)type;
                baseType = arr.BaseType;

                var counter = new ArrayTools.IndexCounter(arraySymbol.ArrayDimensions, skippedDimensions);

                var fulldim = skippedDimensions == 0;
                var firstPass = true;

                bool more;
                do
                {
                    var index = items.Count; // save the index
                    Recurse(items, baseType, 0, null);

                    // patch increments if there was digit rollover
                    var rolled = counter.Digit;
                    more = counter.Next();
                    if (rolled > 0)
                        items[index].PreAddressIncrement += rolled*Runtime.ArrayHeaderSize; // add here in case others add too
                    if (firstPass && fulldim)
                    {
                        // first is offset wrong in multidim case
                        items[index].PreAddressIncrement = 2*(arraySymbol.ArrayDimensions.Count - 1);
                    }
                    firstPass = false;


                } while (more);
            }
            else
                throw new InternalFailure($"Type not convertible yet {ast}");

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
            // what type of operand is this?
            public OperandType OperandType = OperandType.None;
            // address skip to do before write
            public int PreAddressIncrement = 0;
            // is this the last item?
            public bool Last = false;
            public string SymbolName;
        }
    }
}
