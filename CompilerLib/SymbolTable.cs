using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public SymbolTable RootTable { get; }

        /// <summary>
        /// Manages types
        /// </summary>
        public TypeManager TypeManager { get; }

        public SymbolTableManager(Environment environment)
        {
            env = environment;
            RootTable = new SymbolTable(null, GlobalScope);
            tables.Push(RootTable);
            stack.Push(new Tuple<string, bool>(GlobalScope, true));
            onlyScan = false;
            TypeManager = new TypeManager(environment);
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

        void CheckDuplicate(SymbolEntry symbol, string typeName)
        {
            var match = CheckDuplicate(SymbolTable, symbol);
            if (match != null)
            {
                var msg = $"Symbol {typeName} already defined, {symbol.Node} and {match.Item1.Node}";
                if (!ReferenceEquals(match.Item2, SymbolTable))
                    env.Warning(msg);
                else
                    env.Error(msg);
            }

        }

        public SymbolEntry AddTypeSymbol(
            Ast node,
            string typeName
            )
        {
            var type = TypeManager.GetType(typeName);
            var symbol = SymbolTable.AddSymbol(node, Scope, typeName, VariableUse.None, type, null);
            symbol.Attrib = SymbolAttribute.None;
            CheckDuplicate(symbol,typeName);
            return symbol;
        }


        public SymbolEntry AddVariableSymbol(
            Ast node,
            string symbolName,
            InternalType symbolType,
            VariableUse usage,
            List<int> dimensions = null,
            SymbolAttribute attribute = SymbolAttribute.None
            )
        {
            var symbol = SymbolTable.AddSymbol(node, Scope, symbolName, usage, symbolType, dimensions);
            symbol.Attrib = attribute;
            CheckDuplicate(symbol, symbolName);
            return symbol;
        }

        public SymbolEntry AddFunctionSymbol(
            FunctionDeclarationAst node,
            SymbolAttribute attrib,
            List<InternalType> returnType,
            List<InternalType> paramsType
        )
        {
            // var symbol = mgr.AddSymbol(node, node.Name, SymbolType.Function, VariableUse.None, null, attrib, "", returnType, paramsType);
            string symbolName = node.Name;

            var rType = TypeManager.GetType(returnType);
            var pType = TypeManager.GetType(paramsType);
            var iSymbol = TypeManager.GetType(
                symbolName,
                rType,
                pType
                );

            var symbol = SymbolTable.AddSymbol(node, Scope, symbolName, VariableUse.None, iSymbol, null);
            symbol.Attrib = attrib;
            if (attrib.HasFlag(SymbolAttribute.Import) || attrib.HasFlag(SymbolAttribute.Export))
                symbol.UniqueId = GetUniqueId();
            CheckDuplicate(symbol, symbolName);
            return symbol;
        }

        int uniqueId = 0;
        int GetUniqueId()
        {
            return uniqueId++;
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
        public void ComputeSizes()
        {
            // loop, trying to size types, requires repeating until nothing else can be done
            while (true)
            {
                int undone = 0, done = 0;
                InternalType unsizedType = null;
                ComputeTypeSizes(RootTable, ref done, ref undone, ref unsizedType);
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
        void ComputeTypeSizes(SymbolTable table, ref int done, ref int undone, ref InternalType unsizedType)
        {
            // fill in unfilled symbol sizes. 
            foreach (var entry in table.Entries.Where(e => e.ByteSize < 0))
            {
                if (entry.Type is FunctionType)
                    continue;
                var byteSize = 0; // size in packed bytes
                var stackSize = 0; // size on stack
                var name = "";

                var baseType = entry.Type;
                if (entry.Type is ArrayType)
                    baseType = ((ArrayType) entry.Type).BaseType;

                if (baseType is SimpleType)
                    ComputeSimpleSize(entry, (SimpleType) baseType, ref byteSize, ref stackSize);
                else if (baseType is UserType)
                {
                    ComputeUserSize(entry, (UserType) baseType, ref byteSize, ref stackSize);
                    name = ((UserType) baseType).Name;
                }

                if (env.ErrorCount > 0)
                    return;

                if (byteSize > 0)
                {
                    if (!String.IsNullOrEmpty(name))
                    {
                        // before adding any array items, set the base type
                        var itemTypeSymbol = Lookup(RootTable, name);
                        itemTypeSymbol.ByteSize = byteSize;
                        itemTypeSymbol.StackSize = stackSize;
                    }

                    DoArraySizing(entry, ref stackSize, ref byteSize);
                    entry.ByteSize = byteSize;
                    entry.StackSize = stackSize;
                    done++;
                }
                else
                {
                    undone++;
                    unsizedType = entry.Type;
                }

            }

            // recurse on children
            foreach (var child in table.Children)
                ComputeTypeSizes(child, ref done, ref undone, ref unsizedType);
        }

        void ComputeSimpleSize(SymbolEntry entry, SimpleType baseType, ref int byteSize, ref int stackSize)
        {

            switch (baseType.SymbolType)
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
            if (entry.VariableUse == VariableUse.ForLoop && byteSize > 0)
            {
                byteSize += 4 * (CodeGeneratorVisitor.ForLoopStackSize - 1);
                stackSize += CodeGeneratorVisitor.ForLoopStackSize - 1;
            }
        }

        void ComputeUserSize(SymbolEntry entry, UserType baseType, ref int byteSize, ref int stackSize)
        {
            // try to compute size
            var tbl = GetTableWithScope(baseType.Name);
            if (tbl == null)
            {
                env.Error($"Cannot find symbol table for {entry} members");
                return;
            }

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
            if (!allFound)
                byteSize = stackSize = 0; // mark unresolved
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
        /// <param name="noParents"></param>
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

        /// <summary>
        /// Lookup value. If exists, return true. Else return false
        /// </summary>
        /// <param name="enumText"></param>
        /// <param name="memberText"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool LookupEnumValue(string enumText, string memberText, out int value)
        {
            value = 0;
            var tbl = GetTableWithScope(enumText);
            if (tbl == null) return false;
            var s = Lookup(tbl, memberText);
            if (s?.Value == null) return false;
            value = s.Value.Value;
            return true;
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
                /*var tbl = */tables.Pop();
                // remove empty tables - cannot do this since walked later in tree walking
                //if (!tbl.Entries.Any() && !tbl.Children.Any())
                //    tbl.Parent.Children.Remove(tbl);
            }
        }

        #endregion

        readonly Environment env;

        void AddBasicTypes()
        {
            TypeManager.GetType(SymbolType.Bool);
            TypeManager.GetType(SymbolType.Int32);
            TypeManager.GetType(SymbolType.Float32);
            TypeManager.GetType(SymbolType.String);
            TypeManager.GetType(SymbolType.EnumValue);
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

        public string Scope { get; }
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

    // todo - split entries into types?
    public class SymbolEntry
    {
        /// <summary>
        /// Type of symbol
        /// </summary>
        public InternalType Type { get; set;  }

        /// <summary>
        /// Name of symbol
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The defining node (more or less)
        /// </summary>
        public Ast Node { get; }

        /// <summary>
        /// Some symbol attributes
        /// </summary>
        public SymbolAttribute Attrib { get; set; } = SymbolAttribute.None;

        /// <summary>
        /// How used if a variable
        /// </summary>
        public VariableUse VariableUse { get; }

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
        /// -1 when not used, not relevant, or not yet filled in
        /// </summary>
        public int ByteSize { get; set; }
        /// <summary>
        /// size of item in stack entries - used for local variable storage
        /// -1 when not used or relevant
        /// </summary>
        public int StackSize { get; set; }

        // unique id used for imports and exports
        public int UniqueId { get; set; } = -1; 

        /// <summary>
        /// Is this symbol referenced?
        /// </summary>
        public bool Used { get; set;  }

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
                $"{ByteSize}b,{StackSize}s"
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
        // basic types
        Bool,
        Int32,
        Float32,
        String,
        Byte,
        EnumValue,
        UserType,  // use of a user type, type has a name

        ToBeResolved, // cannot yet be matched, like for loop variables
    }
}
