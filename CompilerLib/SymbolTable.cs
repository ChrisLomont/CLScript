using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib
{
    public class SymbolTable
    {
        public List<SymbolEntry> Entries { get;  } = new List<SymbolEntry>();

        public SymbolEntry Lookup(string moduleName, string name)
        {
            //foreach (var e in Entries)
            //{
            //    if (e.Module == moduleName && e.Name == name)
            //        return e;
            //}
            return null;
        }

        public SymbolEntry AddSymbol(Ast node, string scope, string name, List<SymbolType> symbolTypes)
        {
            var entry = new SymbolEntry(node, scope, name, symbolTypes);
            Entries.Add(entry);

            //var e = Lookup(scope, name);
            //todo - error on dups, etc.
            //if (e != null)
            //    throw new InvalidSyntax($"Symbol {moduleName}:{name} already defined, {e.Node} and {node}");
            // todo - check duplicates
            return entry;
        }

        public SymbolEntry AddSymbol(Ast node, string scope, string name, SymbolType symbolType)
        {
            return AddSymbol(node, scope, name, new List<SymbolType> { symbolType });
        }

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
                case TokenType.Char:
                    return SymbolType.Char;
                case TokenType.Bool:
                    return SymbolType.Bool;
                case TokenType.Identifier:
                    return SymbolType.UserType;
                default:
                    throw new InternalFailure($"Unknown symbol type {tokenType}");
            }
            return SymbolType.ToBeResolved;
        }

        public void Dump(TextWriter output)
        {
            foreach (var entry in Entries)
                output.WriteLine(entry);
        }
    }

    public class SymbolEntry
    {

        /// <summary>
        /// Where is this symbol valid
        /// </summary>
        public string Scope { get; private set; }
        
        /// <summary>
        /// Name of symbol
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Type of symbol
        /// </summary>
        public List<SymbolType> Types { get; } = new List<SymbolType>();

        public Ast Node { get; private set; }

        public SymbolEntry(Ast node, string scope, string name, List<SymbolType> symbolTypes)
        {
            Node = node;
            Name = name;
            Scope = scope;
            Types.AddRange(symbolTypes);
        }

        public string TypeText
        {

            get
            {
                if (Types.Count == 1)
                    return Types[0].ToString();
                var sb = new StringBuilder();
                for (var i = 0; i < Types.Count; ++i)
                {
                    var t= Types[i];
                    if (t == SymbolType.Function)
                        sb.Append(" -> ");
                    else if (t >= SymbolType.Array)
                    {
                        sb.Append('[');
                        for (var j = 0; j < t - SymbolType.Array; ++j)
                            sb.Append(',');
                        sb.Append(']');
                    }
                    else
                        sb.Append(t);
                    // if cur or next is function , don't use '*'
                    // if next is array, dont use '*'
                    // else if more, use '*'
                    var isMore      = i < Types.Count - 1;
                    var curIsFunc   = Types[i] == SymbolType.Function;
                    var nextIsFunc  = isMore && Types[i+1] == SymbolType.Function;
                    var nextIsArray = isMore && Types[i + 1] >= SymbolType.Array;
                    if (isMore && !nextIsFunc && !curIsFunc && !nextIsArray)
                        sb.Append(" * ");
                }
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            return $"{Scope,-30},{Name,-15},{TypeText,-15},{Node}";
        }
    }

    public enum SymbolType
    {
        Enum,
        EnumValue,
        UserType,
        Int32,
        Float32,
        String,
        Char,
        Bool,
        Module,
        Function, // comes in type list between parameters and return values: params -> retvals
        ToBeResolved,
        Array = 100, // 100+ is array types of size 1,2,....
    }
}
