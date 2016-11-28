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

        public SymbolTableManager()
        {
            var g = "<global>";
            tables.Push(new SymbolTable(null,g));
            stack.Push(new Tuple<string, bool>(g,true));
        }

        public SymbolEntry AddSymbol(Ast node, string name, SymbolType symbolType)
        {
            return SymbolTable.AddSymbol(node, Scope, name, symbolType);
        }

        /// <summary>
        /// use this to look up single types
        /// </summary>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        public SymbolType GetSymbolType(TokenType tokenType)
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
                    // todo - many types
                    return SymbolType.UserType;
                default:
                    throw new InternalFailure($"Unknown symbol type {tokenType}");
            }
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
            else if (node is FunctionDeclarationAst)
                Push(((FunctionDeclarationAst)node).Name,true);
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
            else if (node is FunctionDeclarationAst)
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
                var child = new SymbolTable(SymbolTable,Scope);
                tables.Push(child);
            }
        }


        void Pop()
        {
            var item = stack.Pop();
            if (item.Item2)
            {
                var tbl = tables.Pop();
                // remove empty tables
                if (!tbl.Entries.Any() && !tbl.Children.Any())
                    tbl.Parent.Children.Remove(tbl);
            }
        }

        #endregion
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

        public SymbolEntry Lookup(string name)
        {
            //foreach (var e in Entries)
            //{
            //    if (e.Module == moduleName && e.Name == name)
            //        return e;
            //}
            return null;
        }

        public SymbolEntry AddSymbol(Ast node, string scope, string name, SymbolType symbolType)
        {
            var entry = new SymbolEntry(node, name, symbolType);
            Entries.Add(entry);

            //var e = Lookup(scope, name);
            //todo - error on dups, etc.
            //if (e != null)
            //    throw new InvalidSyntax($"Symbol {moduleName}:{name} already defined, {e.Node} and {node}");
            // todo - check duplicates
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
        /// Name of symbol
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Type of symbol, used with a SymbolManager to get detailed type info
        /// </summary>
        public SymbolType Type { get; }

        /// <summary>
        /// The defining node (more or less)
        /// </summary>
        public Ast Node { get; private set; }

        // if greater than zero, is array dimension
        public int ArraySize { get; private set; }

        public string ReturnType { get; set; }
        public string ParamsType { get; set; }

        public SymbolAttribute Attrib { get; set; } = SymbolAttribute.None;

        /// <summary>
        /// If symbol type is user type, and this is a variable, then this is the text of the type
        /// </summary>
        public string UserType { get; private set; }

        public SymbolEntry(Ast node, string name, SymbolType symbolType)
        {
            Node = node;
            Name = name;
            Type = symbolType;
        }

        public string TypeText
        {
            get
            {
                if (Type == SymbolType.Function)
                {
                    return $"{Type} {ParamsType} => {ReturnType}";
                }

                if (ArraySize == 0 && UserType == null)
                    return Type.ToString();
                var arrayText = "";
                if (ArraySize > 0)
                    arrayText = "[" + new string(',',ArraySize-1) + "]";
                var userText = !String.IsNullOrEmpty(UserType) ? $" of {UserType}" : "";
                return $"{Type}{arrayText}{userText}";
            }
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
            return $"T {name,-15} {TypeText,-15}";//,{Node}";
        }

        public void AddArraySize(int arraySize)
        {
            ArraySize = arraySize;
        }

        public void AddUserType(string userType)
        {
            UserType = userType;
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
        Char,
        Enum,
        EnumValue,
        Module,
        ToBeResolved,
        UserType, 
        Function  
    }
}
