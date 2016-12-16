using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Track symbol tables, interface to externals
    /// </summary>
    public class SymbolTableManager
    {
        // name of the global scope, unique
        public static string GlobalScope { get; } = "<global>";

        /// <summary>
        /// The current symbol table when walking AST
        /// </summary>
        public SymbolTable SymbolTable => tables.Peek();

        /// <summary>
        /// The root symbol table
        /// </summary>
        public SymbolTable RootTable { get; private set;  }

        /// <summary>
        /// Manages types
        /// </summary>
        public TypeManager TypeManager { get; private set; }

        public SymbolTableManager(Environment environment)
        {
            env = environment;
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
        /// <param name="usage">How this variable is used</param>
        /// <param name="arrayDimensions">Array dimensions if one present, else 0</param>
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
            var arrayDimension = arrayDimensions?.Count ?? 0;
            var iSymbol = TypeManager.GetType(
                symbolType,
                arrayDimension,
                typeName,
                returnType,
                paramsType
                );

            var symbol = SymbolTable.AddSymbol(node, Scope, symbolName, usage, iSymbol, arrayDimensions);
            symbol.Attrib = attrib;
            var match = CheckDuplicate(SymbolTable, symbol);
            if (match != null)
            {
                var msg = $"Symbol {symbolName} already defined, {symbol.Node} and {match.Item1.Node}";
                if (!ReferenceEquals(match.Item2, SymbolTable))
                    env.Warning(msg);
                else
                    env.Error(msg);
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
        /// Compute symbol table sizes in bytes, filling type size fields
        /// </summary>
        /// <param name="environment"></param>
        public void ComputeSizes(Environment environment)
        {
            // loop, trying to size types, requires repeating until nothing else can be done
            while (true)
            {
                int undone = 0, done = 0;
                InternalType unsizedType = null;
                ComputeTypeSizes(RootTable, environment, ref done, ref undone, ref unsizedType);
                if (undone > 0 && done == 0)
                {
                    // show one that was not resolvable
                    environment.Error($"Could not resolve type sizes {unsizedType}");
                    break;
                }
                if (undone == 0)
                    break;
            }
        }

        // recurse on tables, adding number sized and number unable to be sized this pass
        // return a type of one that cannot be sized if any
        void ComputeTypeSizes(SymbolTable table, Environment environment, ref int done, ref int undone, ref InternalType type)
        {
            // fill in basic types symbols. These are simple types and arrays of simple types
            foreach (var e in table.Entries)
            {
                var byteSize = 0; // size in packed bytes
                var stackSize = 0; // size on stack
                switch (e.Type.SymbolType)
                {
                    case SymbolType.Byte:
                    case SymbolType.Bool:
                        byteSize = 1;
                        stackSize = 1; // one entry
                        break;
                    case SymbolType.String:
                    case SymbolType.EnumValue:
                    case SymbolType.Float32:
                    case SymbolType.Int32:
                        byteSize = 4;
                        stackSize = 1; // one entry
                        break;
                }
                if (e.VariableUse == VariableUse.ForLoop && byteSize>0)
                {
                    byteSize += 4*(CodeGeneratorVisitor.ForLoopStackSize - 1);
                    stackSize += CodeGeneratorVisitor.ForLoopStackSize - 1;
                }
                if (byteSize > 0)
                {
                    DoArraySizing(e, ref stackSize, ref byteSize);
                    e.ByteSize = byteSize;
                    e.StackSize = stackSize;
                    done++;
                }
            }

            // loop over unsized user types
            foreach (var item in table.Entries.Where(e => e.ByteSize < 0))
            {
                if (/*item.Type.SymbolType != SymbolType.Typedef &&*/ item.Type.SymbolType != SymbolType.UserType1)
                    continue;

                // try to compute size
                var tbl = GetTableWithScope(item.Type.UserTypeName);
                if (tbl == null)
                {
                    undone++;
                    type = item.Type;
                    environment.Error($"Cannot find symbol table for {item} members");
                    return;
                }
                var byteSize = 0;
                var stackSize = 0;
                var allFound = true;
                foreach (var e in tbl.Entries)
                {
                    if (e.ByteSize > 0)
                    {
                        byteSize += e.ByteSize;
                        stackSize += e.StackSize;
                    }
                    else
                    {
                        allFound = false;
                        break;
                    }
                }
                if (allFound)
                {
                    done++;
                    // before adding any array items, set the base type
                    var itemTypeSymbol = Lookup(RootTable, item.Type.UserTypeName);
                    itemTypeSymbol.ByteSize = byteSize;
                    itemTypeSymbol.StackSize = stackSize;

                    DoArraySizing(item, ref stackSize, ref byteSize);
                    item.ByteSize = byteSize; // note this may set multiple types if same member structure
                    item.StackSize = stackSize;
                }
                else
                {
                    undone++;
                    type = item.Type;
                }
            }
            // recurse on children
            foreach (var child in table.Children)
                ComputeTypeSizes(child, environment, ref done, ref undone, ref type);
        }

        // given a symbol, and base type size, compute any additional array sizing requirements
        void DoArraySizing(SymbolEntry e, ref int stackSize, ref int byteSize)
        {
            if (e.ArrayDimensions != null)
            {
                // these must be computed in reverse
                var dim1 = e.ArrayDimensions.Count;
                for (var i = 0; i < dim1; ++i)
                {
                    var dim = e.ArrayDimensions[dim1 - 1 - i];
                    stackSize = Runtime.ArrayHeaderSize + stackSize * dim;
                    byteSize = Runtime.ArrayHeaderSize * 4 + byteSize * dim;
                }
            }

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
        public SymbolEntry Lookup(SymbolTable table, string symbol, bool noParents = false)
        {
            if (table == null) return null;
            foreach (var entry in table.Entries)
                if (symbol == entry.Name)
                    return entry;
            if (noParents)
                return null;
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

        Environment env;

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


        // given the name of a type, and the name of a member, get the offset
        // returns ReferenceAddress, not LayoutAddress
        public int GetTypeOffset(string typeName, string memberName)
        {
            var r = RootTable;
            foreach (var c in r.Children)
            {
                if (c.Scope == NestScope(GlobalScope, typeName))
                {
                    var s = Lookup(c, memberName, true);
                    if (s?.LayoutAddress != null)
                        return s.ReferenceAddress;
                    break;
                }
            }
            env.Error($"GetTypeOffset did not find member {memberName} in type {typeName}");
            return -1;
        }
    }

    public class SymbolTable
    {
        /// <summary>
        /// stack size required for this block
        /// </summary>
        public int StackEntries { get; set; }

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

        public SymbolEntry AddSymbol(Ast node, string scope, string name, VariableUse usage, InternalType symbolType, List<int> arrayDimensions)
        {
            // todo - pass scope in and resolve
            var entry = new SymbolEntry(node, name, usage, symbolType, arrayDimensions);
            Entries.Add(entry);
            return entry;
        }


        public void Dump(TextWriter output, string indent)
        {
            output.WriteLine($"{indent}Symbol Table : {Scope} : stack {StackEntries}:");
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
        /// Used in memory layout. For items with a header 
        /// (arrays, for example), this is where the header starts.
        /// See ReferenceAddress
        /// </summary>
        public int? LayoutAddress { get; set; }

        /// <summary>
        /// Address offset if the symbol has a fixed value
        /// Used to reference the variable in code generation layout. 
        /// For items with a header (arrays, for example), this is past 
        /// the header. See LayoutAddress.
        /// </summary>
        public int ReferenceAddress
        {
            get
            {
                if (ArrayDimensions != null && ArrayDimensions.Any() && LayoutAddress.HasValue)
                    return LayoutAddress.Value + Runtime.ArrayHeaderSize;
                return LayoutAddress ?? -1;
            }
        }



        /// <summary>
        /// Dimensions if array type
        /// </summary>
        public List<int> ArrayDimensions { get; set; }

        /// <summary>
        /// Attributes associated with this symbol
        /// </summary>
        public List<Attribute> Attributes { get; set; } = new List<Attribute>();

        /// <summary>
        /// size of item in bytes - used for code storage
        /// -1 when not used or relevant
        /// </summary>
        public int ByteSize { get; set; }
        /// <summary>
        /// size of item in stack entries - used for local variable storage
        /// -1 when not used or relevant
        /// </summary>
        public int StackSize { get; set; }


        public SymbolEntry(Ast node, string name, VariableUse usage, InternalType symbolType, List<int> arrayDimensions)
        {
            Node = node;
            Name = name;
            Type = symbolType;
            VariableUse = usage;
            ArrayDimensions = arrayDimensions;
            ByteSize = StackSize = -1; // unused
        }

        public override string ToString()
        {
            var sizeText = ByteSize >= 0 ?
                $",{ByteSize}b,{StackSize}s"
                : "";

            var name = Name;
            var flags = "";
            if ((Attrib & SymbolAttribute.Const) != SymbolAttribute.None)
                flags += "c";
            if ((Attrib & SymbolAttribute.Export) != SymbolAttribute.None)
                flags += "e";
            if ((Attrib & SymbolAttribute.Import) != SymbolAttribute.None)
                flags += "i";
            var value = Value.HasValue?Value.ToString():"";
            var addr  = LayoutAddress.HasValue ? LayoutAddress.ToString() : "";
            var attributes = "";
            foreach (var attr in Attributes)
                attributes += attr.ToString();

            return $"name:{name,-8} flags:{flags,-3} val:{value,-4} addr:{addr,-4} Use:{VariableUse,-6} Type:{Type,-15} Size:{sizeText,-5} Attr:{attributes} ";
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
