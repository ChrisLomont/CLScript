using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib
{
    public class SymbolTable
    {
        public Ast Node { get; set; }

        // disallow external creation
        SymbolTable(Ast node)
        {
            Node = node;
        }

        static public SymbolTable Create(SymbolTable parent, Ast node)
        {
            return new SymbolTable(node) {Parent = parent};
        }

        public List<SymbolEntry> Entries { get;  } = new List<SymbolEntry>();
        public SymbolTable Parent { get; set; }

        public SymbolEntry Lookup(string moduleName, string name)
        {
            //foreach (var e in Entries)
            //{
            //    if (e.Module == moduleName && e.Name == name)
            //        return e;
            //}
            return null;
        }

        public SymbolEntry AddSymbol(Ast node, string moduleName, string name, SymbolType symbolType)
        {
            var e = Lookup(moduleName, name);
            //if (e != null)
            //    throw new InvalidSyntax($"Symbol {moduleName}:{name} already defined, {e.Node} and {node}");
            // todo - check duplicates
            //var entry = new SymbolEntry(node, moduleName, name, symbolType);
            //Entries.Add(entry);
            //return entry;
            return null;
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
    }

    public class Scope : List<string>
    {
        
    }

    public class SymbolEntry
    {

        /// <summary>
        /// Where is this symbol valid
        /// </summary>
        public Scope Scope { get; private set; }
        
        /// <summary>
        /// Name of symbol
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Type of symbol
        /// </summary>
        public SymbolType Type { get; private set; }

        public SymbolEntry(string moduleName, string name, SymbolType symbolType)
        {
        }
    }

    public enum SymbolType
    {
        Enum,
        EnumValue,
        UserType,
        Function,
        Int32,
        Float32,
        String,
        Char,
        Bool,
        ToBeResolved
    }
}
