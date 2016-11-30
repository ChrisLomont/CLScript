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
    /// <summary>
    /// Track symbol tables, interface to externals
    /// </summary>
    public class SymbolTableManager
    {
        /// <summary>
        /// The current symbol table
        /// </summary>
        public SymbolTable SymbolTable => tables.Peek();

        public TypeManager TypeManager { get; private set; }

        public SymbolTableManager(Environment env)
        {
            environment = env;
            var g = "<global>";
            tables.Push(new SymbolTable(null,g));
            stack.Push(new Tuple<string, bool>(g,true));
            onlyScan = false;
            TypeManager = new TypeManager();
            AddBasicTypes();
        }

        /// <summary>
        /// Ensure is at top of parse
        /// </summary>
        public void Start()
        {
            onlyScan = true;
            while (tables.Count > 1)
                Pop();
            blockIndex = 0;

        }

        // set to true for walking existing table, else creates table 
        bool onlyScan = false;

        /// <summary>
        /// Add a symbol
        /// </summary>
        /// <param name="node">Ast node that triggered the generation</param>
        /// <param name="symbolName">Name</param>
        /// <param name="symbolType">Symbol type</param>
        /// <param name="arrayDimension">Array dimension if one present, else 0</param>
        /// <param name="attrib">Attributes</param>
        /// <param name="typeName">Used if present, used when declaring a variable of a user defined type</param>
        /// <param name="returnType">List of function return types</param>
        /// <param name="paramsType">List of function parameter types</param>
        /// <returns></returns>
        public SymbolEntry AddSymbol(
            Ast node, 
            string symbolName, 
            SymbolType symbolType,
            int arrayDimension = 0, 
            SymbolAttribute attrib = SymbolAttribute.None, 
            string typeName = "", 
            List<InternalType> returnType = null, 
            List<InternalType> paramsType = null
            )
        {
            var iSymbol = TypeManager.GetType(
                symbolType,
                arrayDimension,
                typeName,
                returnType,
                paramsType
                );

            var symbol = SymbolTable.AddSymbol(node, Scope, symbolName, iSymbol);
            symbol.Attrib = attrib;
            var match = CheckDuplicate(SymbolTable, symbol);
            if (match != null)
            {
                var msg = $"Symbol {symbolName} already defined, {symbol.Node} and {match.Item1.Node}";
                if (!ReferenceEquals(match.Item2, SymbolTable))
                    environment.Warning(msg);
                else
                    environment.Error(msg);
            }
            return symbol;
        }

        // return symbol and table where found
        Tuple<SymbolEntry,SymbolTable> CheckDuplicate(SymbolTable table, SymbolEntry entryToMatch)
        {
            if (table == null)
                return null;
            foreach (var entry in table.Entries)
            {
                if (ReferenceEquals(entry, entryToMatch))
                    continue;
                if (entry.Name == entryToMatch.Name)
                    return new Tuple<SymbolEntry, SymbolTable>(entry, table);
            }
            return CheckDuplicate(table.Parent, entryToMatch);
        }

        /// <summary>
        /// lookup symbol in current table or any ancestors
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public SymbolEntry Lookup(string symbol)
        {
            return Lookup(SymbolTable,symbol);
        }

        /// <summary>
        /// lookup symbol in this table or any ancestors
        /// </summary>
        /// <param name="table"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        SymbolEntry Lookup(SymbolTable table, string symbol)
        {
            if (table == null) return null;
            foreach (var entry in table.Entries)
                if (symbol == entry.Name)
                    return entry;
            return Lookup(table.Parent, symbol);
        }

        public string Scope => stack.Peek().Item1;

        // call when walking nodes, before child recurse
        public void EnterAst(Ast node)
        {
            if (node is EnumAst)
                Push(((EnumAst)node).Name,true);
            else if (node is ModuleAst)
                Push(((ModuleAst)node).Name,false);
            else if (node is TypeDeclarationAst)
                Push(((TypeDeclarationAst)node).Name,true);
//            else if (node is FunctionDeclarationAst)
//                Push(((FunctionDeclarationAst)node).Name,true);
            else if (node is BlockAst)
                Push(GetBlockName(),true);

        }

        public void ExitAst(Ast node)
        {
            if (node is EnumAst)
                Pop();
            else if (node is ModuleAst)
                Pop();
            else if (node is TypeDeclarationAst)
                Pop();
//            else if (node is FunctionDeclarationAst)
//                Pop();
            else if (node is BlockAst)
                Pop();
        }

        public void Dump(TextWriter output)
        {
            var top = SymbolTable;
            while (top.Parent != null) top = top.Parent;
            Dump(output,top);
        }

        void Dump(TextWriter output, SymbolTable table)
        {
            table.Dump(output);
            foreach (var child in table.Children)
                Dump(output,child);
        }


        #region Scoping

        // stack of names of scopes, with bool telling whether or not a new symbol table was pushed
        readonly Stack<Tuple<string,bool>> stack = new Stack<Tuple<string,bool>>();

        readonly Stack<SymbolTable> tables = new Stack<SymbolTable>();

        // todo - this needs reset each pass
        int blockIndex = 0;
        string GetBlockName()
        {
            ++blockIndex;
            return "Block_" + blockIndex;
        }


        void Push(string name, bool newTable)
        {
            var top = stack.Peek().Item1;
            stack.Push(new Tuple<string, bool>(top + "." + name,newTable));
            if (newTable)
            {
                if (onlyScan)
                { // find child table and push it
                    var tbl = SymbolTable.Children.FirstOrDefault(t => t.Scope == Scope);
                    if (tbl == null)
                        throw new InternalFailure("Cannot find table!");
                    tables.Push(tbl);
                }
                else
                {
                    var child = new SymbolTable(SymbolTable, Scope);
                    tables.Push(child);
                }
            }
        }


        void Pop()
        {
            var item = stack.Pop();
            if (item.Item2)
            {
                var tbl = tables.Pop();
                // remove empty tables - cannot do this since walked later in tree walking
                //if (!tbl.Entries.Any() && !tbl.Children.Any())
                //    tbl.Parent.Children.Remove(tbl);
            }
        }

        #endregion

        Environment environment;

        void AddBasicTypes()
        {
            TypeManager.AddBasicType(SymbolType.Bool,"bool");
            TypeManager.AddBasicType(SymbolType.Int32, "i32");
            TypeManager.AddBasicType(SymbolType.Float32, "r32");
            TypeManager.AddBasicType(SymbolType.String, "string");
            TypeManager.AddBasicType(SymbolType.Enum, "enum");
            TypeManager.AddBasicType(SymbolType.EnumValue, "enum value");
            TypeManager.AddBasicType(SymbolType.Module, "module");
            TypeManager.AddBasicType(SymbolType.Typedef, "Type");

            TypeManager.AddBasicType(SymbolType.ToBeResolved, "UNKNOWN");
            // TypeManager.AddBasicType(SymbolType.UserType1, "UserType");
        }

    }

    public class SymbolTable
    {
        public string Scope { get; private set; }
        public List<SymbolEntry> Entries { get; } = new List<SymbolEntry>();
        public SymbolTable Parent { get;}
        public List<SymbolTable> Children { get; } = new List<SymbolTable>();

        public SymbolTable(SymbolTable parent, string scope)
        {
            Parent = parent;
            parent?.Children.Add(this);
            Scope = scope;
        }

        public SymbolEntry AddSymbol(Ast node, string scope, string name, InternalType symbolType)
        {
            // todo - pass scope in and resolve
            var entry = new SymbolEntry(node, name, symbolType);
            Entries.Add(entry);
            return entry;
        }


        public void Dump(TextWriter output)
        {
            output.WriteLine($"*********** symbol tbl {Scope} ********************");
            foreach (var entry in Entries)
                output.WriteLine(entry);
            output.WriteLine("*****************************************************");
        }
    }

    public class SymbolEntry
    {
        /// <summary>
        /// Type of symbol
        /// </summary>
        public InternalType Type { get; }

        /// <summary>
        /// Name of symbol
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The defining node (more or less)
        /// </summary>
        public Ast Node { get; private set; }

        /// <summary>
        /// Some symbol attributes
        /// </summary>
        public SymbolAttribute Attrib { get; set; } = SymbolAttribute.None;

        public SymbolEntry(Ast node, string name, InternalType symbolType)
        {
            Node = node;
            Name = name;
            Type = symbolType;
        }

        public override string ToString()
        {
            var name = Name;
            if ((Attrib & SymbolAttribute.Const) != SymbolAttribute.None)
                name += "+c";
            if ((Attrib & SymbolAttribute.Export) != SymbolAttribute.None)
                name += "+e";
            if ((Attrib & SymbolAttribute.Import) != SymbolAttribute.None)
                name += "+i";
            return $"T {name,-15} {Type,-15}";//,{Node}";
        }
    }

    [Flags]
    public enum SymbolAttribute
    {
        None   = 0x0000,
        Const  = 0x0001,
        Import = 0x0002,
        Export = 0x0004
    }

    public enum SymbolType
    {
        Bool,
        Int32,
        Float32,
        String,
        Byte,
        Enum,
        EnumValue,
        Module,
        UserType1,  // use of a user type, with a name
        Typedef,    // type definition
        Function,

        ToBeResolved, // cannot yet be matched, like for loop variables
        MatchAny      // used for searches
    }
}
