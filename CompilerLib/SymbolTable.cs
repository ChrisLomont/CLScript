using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib
{
    public class SymbolTable
    {
        public Ast Node { get; set; }
        public SymbolTable(Ast node)
        {
            Node = node;
        }

        public List<SymbolEntry> Entries { get;  } = new List<SymbolEntry>();

        public void AddSymbol(Ast node, string moduleName, string name, SymbolType symbolType)
        {
            Entries.Add(new SymbolEntry(node,moduleName,name,symbolType));
        }
    }

    public class SymbolEntry
    {
        Ast Node;
        string Module;
        string Name;
        SymbolType Type;
        public SymbolEntry(Ast node, string moduleName, string name, SymbolType symbolType)
        {
            Node = node;
            Module = moduleName;
            Name = name;
            Type = symbolType;
        }

        public override string ToString()
        {
            return $"T<{Module}:{Name} {Type} - {Node}>";
        }
    }

    public enum SymbolType
    {
        Enum,
        EnumValue,
        UserType,
        Function,
        ToBeResolved
    }
}
