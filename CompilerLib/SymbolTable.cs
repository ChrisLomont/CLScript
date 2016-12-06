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
        public static string GlobalScope { get; } = "<global>";

        /// <summary>
        /// The current symbol table
        /// </summary>
        public SymbolTable SymbolTable => tables.Peek();

        /// <summary>
        /// The root symbol table
        /// </summary>
        public SymbolTable RootTable { get; private set;  }

        public TypeManager TypeManager { get; private set; }

        public SymbolTableManager(Environment env)
        {
            environment = env;
            RootTable = new SymbolTable(null, GlobalScope);
            tables.Push(RootTable);
            stack.Push(new Tuple<string, bool>(GlobalScope, true));
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
            VariableUse usage, 
            List<int> arrayDimensions = null, 
            SymbolAttribute attrib = SymbolAttribute.None, 
            string typeName = "", 
            List<InternalType> returnType = null, 
            List<InternalType> paramsType = null
            )
        {
            var iSymbol = TypeManager.GetType(
                symbolType,
                arrayDimensions,
                typeName,
                returnType,
                paramsType
                );

            var symbol = SymbolTable.AddSymbol(node, Scope, symbolName, usage, iSymbol);
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

        public SymbolTable GetTableWithScope(string typeName)
        {
            return SearchScope(RootTable, typeName);
        }

        SymbolTable SearchScope(SymbolTable table, string typeName)
        {
            if (table.Scope == NestScope(GlobalScope, typeName))
                return table;
            foreach (var child in table.Children)
            {
                var subTbl = SearchScope(child, typeName);
                if (subTbl != null)
                    return subTbl;
            }
            return null;
        }

        /// <summary>
        /// Compute type sizes in bytes, filling type size fields
        /// </summary>
        /// <param name="env"></param>
        public void ComputeSizes(Environment env)
        {
            // fill in basic types in type table
            foreach (var t in TypeManager.Types)
            {
                var size = 0;
                switch (t.SymbolType)
                {
                    case SymbolType.Byte:
                    case SymbolType.Bool:
                        size = 1;
                        break;
                    case SymbolType.String:
                    case SymbolType.EnumValue:
                    case SymbolType.Float32:
                    case SymbolType.Int32:
                        size = 4;
                        break;
                }
                foreach (var dim in t.ArrayDimensions)
                    size *= dim;
                if (size > 0)
                    t.Size = size;
            }

            // loop, trying to size types, requires repeating until nothing else can be done
            while (true)
            {
                int undone = 0, done = 0;
                InternalType unsizedType = null;
                ComputeTypeSizes(RootTable, env, ref done, ref undone, ref unsizedType);
                if (undone > 0 && done == 0)
                {
                    // show one that was not resolvable
                    env.Error($"Could not resolve type sizes {unsizedType}");
                    break;
                }
                if (undone == 0)
                    break;
            }
        }

        // recurse on tables, adding number sized and number unable to be sized this pass
        // return a type of one that cannot be sized if any
        void ComputeTypeSizes(SymbolTable table, Environment env, ref int done, ref int undone, ref InternalType type)
        {
            foreach (
                var item in table.Entries.Where(t => t.Type.SymbolType == SymbolType.UserType1 && !t.Type.Size.HasValue)
            )
            {
                // try to compute size
                var tbl = GetTableWithScope(item.Type.UserTypeName);
                if (tbl == null)
                {
                    undone++;
                    type = item.Type;
                    env.Error($"Cannot find symbol table for {item} members");
                    return;
                }
                var size = 0;
                var allFound = true;
                foreach (var e in tbl.Entries)
                {
                    if (e.Type.Size.HasValue)
                        size += e.Type.Size.Value;
                    else
                    {
                        allFound = false;
                        break;
                    }
                }
                if (allFound)
                {
                    done++;
                    foreach (var dim in item.Type.ArrayDimensions)
                        size *= dim;
                    item.Type.Size = size; // note this may set multiple types if same member structure
                }
                else
                {
                    undone++;
                    type = item.Type;
                }
            }
            // recurse on children
            foreach (var child in table.Children)
                ComputeTypeSizes(child, env, ref done, ref undone, ref type);
        }


        // return symbol and table where found
        Tuple<SymbolEntry, SymbolTable> CheckDuplicate(SymbolTable table, SymbolEntry entryToMatch)
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
            else if (node is BlockAst)
                Pop();
        }

        public void Dump(TextWriter output)
        {
            var top = SymbolTable;
            while (top.Parent != null) top = top.Parent;
            Dump(output,top);
        }

        void Dump(TextWriter output, SymbolTable table, string indent="")
        {
            table.Dump(output, indent);
            var subIndet = indent + "     ";
            foreach (var child in table.Children)
                Dump(output,child, subIndet);
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

        // how scope text representations are formed
        static string NestScope(string outerScope, string innerScope)
        {
            return outerScope + "." + innerScope;
        }

        void Push(string name, bool newTable)
        {
            var top = stack.Peek().Item1;
            stack.Push(new Tuple<string, bool>(NestScope(top,name),newTable));
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

        // set to true for walking existing table, else creates table 
        bool onlyScan = false;


    }

    public class SymbolTable
    {
        /// <summary>
        /// stack size required for this block
        /// </summary>
        public int StackSize { get; set; }

        /// <summary>
        /// parameters size on stack
        /// </summary>
        public int ParamsSize { get; set; }

        public string Scope { get; private set; }
        public List<SymbolEntry> Entries { get; } = new List<SymbolEntry>();
        public SymbolTable Parent { get;}
        public List<SymbolTable> Children { get; } = new List<SymbolTable>();

        // is this a function block table?
        public bool IsFunctionBlock
        {
            get
            {
                // form:  global.block_...
                var words = Scope.Split('.');
                return words.Length > 1 && words[1].StartsWith("Block_");
            }
        }


        public SymbolTable(SymbolTable parent, string scope)
        {
            Parent = parent;
            parent?.Children.Add(this);
            Scope = scope;
        }

        public SymbolEntry AddSymbol(Ast node, string scope, string name, VariableUse usage, InternalType symbolType)
        {
            // todo - pass scope in and resolve
            var entry = new SymbolEntry(node, name, usage, symbolType);
            Entries.Add(entry);
            return entry;
        }


        public void Dump(TextWriter output, string indent)
        {
            output.WriteLine($"{indent}Symbol Table Scope: {Scope} :{StackSize},{ParamsSize}:");
            foreach (var entry in Entries)
                output.WriteLine(indent+entry);
            output.WriteLine($"{indent}****************************");
        }
    }

    public class SymbolEntry
    {
        /// <summary>
        /// Type of symbol
        /// </summary>
        public InternalType Type { get; set;  }

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

        /// <summary>
        /// How used if a variable
        /// </summary>
        public VariableUse VariableUse { get;  private set; }

        /// <summary>
        /// Value if the symbol has a fixed value
        /// </summary>
        public int? Value { get; set; }

        /// <summary>
        /// Address offset if the symbol has a fixed value
        /// </summary>
        public int? Address { get; set; }

        /// <summary>
        /// Attributes associated with this symbol
        /// </summary>
        public List<Attribute> Attributes { get; set; } = new List<Attribute>();

        public SymbolEntry(Ast node, string name, VariableUse usage, InternalType symbolType)
        {
            Node = node;
            Name = name;
            Type = symbolType;
            VariableUse = usage;
        }

        public override string ToString()
        {
            var name = Name;
            var flags = "";
            if ((Attrib & SymbolAttribute.Const) != SymbolAttribute.None)
                flags += "c";
            if ((Attrib & SymbolAttribute.Export) != SymbolAttribute.None)
                flags += "e";
            if ((Attrib & SymbolAttribute.Import) != SymbolAttribute.None)
                flags += "i";
            var value = Value.HasValue?Value.ToString():"";
            var addr  = Address.HasValue ? Address.ToString() : "";
            var attributes = "";
            foreach (var attr in Attributes)
                attributes += attr.ToString();

            return $"{name,-12} {flags,-3} {value,-8} {addr,-8} {VariableUse,-6} {Type,-15} {attributes} ";
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

    public enum VariableUse
    {
        None,
        Const,
        Member,
        ForLoop,
        Local,
        Global,
        Param
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
