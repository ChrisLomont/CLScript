using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Tracks types, giving each a unique index, perform formatting
    /// </summary>
    public class TypeManager
    {
        readonly Environment env;
        public TypeManager(Environment environment)
        {
            env = environment;
        }

        /// <summary>
        /// Lookup basic type given the symbol type
        /// </summary>
        /// <param name="symbolType"></param>
        /// <returns></returns>
        public SimpleType GetType(SymbolType symbolType)
        {
            foreach (var t in Types.OfType<SimpleType>())
            {
                if (t.SymbolType == symbolType)
                    return t;
            }
            var type = new SimpleType(this, Types.Count, symbolType);
            Types.Add(type);
            return type;
        }

        /// <summary>
        /// Lookup type, if not created, do so
        /// </summary>
        public TupleType GetType(List<InternalType> tupleType)
        {
            foreach (var t in Types.OfType<TupleType>())
            {
                if (ListMatches(tupleType, t.Tuple))
                    return t;
            }
            var type = new TupleType(this, Types.Count, tupleType);
            Types.Add(type);
            return type;
        }

        /// <summary>
        /// Lookup type, if not created, do so
        /// </summary>
        public FunctionType GetType(string name, TupleType returnType, TupleType paramsType)
        {
            foreach (var t in Types.OfType<FunctionType>())
            {
                if (t.Name == name && t.ReturnType == returnType && t.ParamsType == paramsType)
                    return t;
            }
            var type = new FunctionType(this, Types.Count, name, returnType,paramsType);
            Types.Add(type);
            return type;
        }

        /// <summary>
        /// Lookup type, if not created, do so
        /// </summary>
        public InternalType GetType(int arrayDimension, InternalType baseType)
        {
            if (arrayDimension <= 0)
                throw new InternalFailure($"Array dimension {arrayDimension} must be positive");
            foreach (var t in Types.OfType<ArrayType>())
            {
                if (t.ArrayDimension == arrayDimension && t.BaseType == baseType)
                    return t;
            }
            var type = new ArrayType(this, Types.Count, arrayDimension, baseType);
            Types.Add(type);
            return type;
        }

        /// <summary>
        /// Lookup type, if not created, do so
        /// </summary>
        public InternalType GetType(string typeName, SymbolType symbolType)
        {
            foreach (var t in Types.OfType<UserType>())
            {
                if (t.Name == typeName)
                    return t;
            }
            var type = new UserType(this, Types.Count,typeName, symbolType);
            Types.Add(type);
            return type;
        }


        bool ListMatches(List<InternalType> list1, List<InternalType> list2)
        {
            if (list1 == null && list2 == null)
                return true;
            if (list1 == null || list2 == null)
                return false;
            if (ReferenceEquals(list1, list2))
                return true;
            if (list1.Count != list2.Count)
                return false;
            for (var i = 0; i < list1.Count; ++i)
                if (list1[i] != list2[i])
                    return false;
            return true;
        }

        // store unique types here
        public List<InternalType> Types { get;  } = new List<InternalType>();
    }

    #region Type classes

    /// <summary>
    /// Represent a type, useful for semantic analysis, type propagation, and type casting
    /// </summary>
    public abstract class InternalType
    {
        protected InternalType(TypeManager mgr, int index)
        {
            TypeManager = mgr;
            TypeIndex = index;
        }

        #region Equality

        public override bool Equals(Object obj)
        {
            return obj is InternalType && this == (InternalType)obj;
        }

        public override int GetHashCode()
        {
            return TypeManager.GetHashCode() ^ TypeIndex.GetHashCode();
        }

        public static bool operator ==(InternalType a, InternalType b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
                return true;

            // If one is null, but not both, return false.
            if (((object)a == null) || ((object)b == null))
                return false;

            return ReferenceEquals(a.TypeManager, b.TypeManager) && a.TypeIndex == b.TypeIndex;
        }

        public static bool operator !=(InternalType x, InternalType y)
        {
            return !(x == y);
        }

        #endregion

        protected readonly TypeManager TypeManager;
        protected readonly int TypeIndex;

    }


    /// <summary>
    /// built in type such as int32, r32, string, bool, etc.
    /// </summary>
    public class SimpleType : InternalType
    {
        public SimpleType(
            TypeManager mgr,
            int index,
            SymbolType type
            ) : base(mgr,index)
        {
            SymbolType = type;
        }

        /// <summary>
        /// Underlying symbol type
        /// </summary>
        public SymbolType SymbolType { get; }

        public override string ToString()
        {
            return SymbolType.ToString();
        }

    }

    /// <summary>
    /// Type of a function
    /// </summary>
    public class FunctionType : InternalType
    {
        public FunctionType(
            TypeManager mgr,
            int index,
            string name,
            TupleType returnType,
            TupleType paramsType) : base(mgr,index)
        {

            Name = name;
            ReturnType = returnType;
            ParamsType = paramsType;
        }

        public string Name { get; }

        /// <summary>
        /// number of stack entries this function leaves on the call stack
        /// </summary>
        public int CallStackReturnSize { get; set; } = -1;

        /// <summary>
        /// return types for function type
        /// </summary>
        public TupleType ReturnType { get; }
        /// <summary>
        /// parameter types for function type
        /// </summary>
        public TupleType ParamsType { get; }

        public override string ToString()
        {
            return $"{ParamsType} => {ReturnType}";
        }
    }

    /// <summary>
    /// An array of some other type
    /// </summary>
    public class ArrayType : InternalType
    {
        public ArrayType(
            TypeManager mgr,
            int index,
            int arrayDimension,
            InternalType baseType
        ) : base(mgr, index)
        {
            ArrayDimension = arrayDimension;
            BaseType = baseType;
        }

        /// <summary>
        /// dimension of multidimensional arrays
        /// 0 is no array
        /// 1 is single array
        /// etc.
        /// </summary>
        public int ArrayDimension { get; }

        /// <summary>
        /// The type of the base item.
        /// If not an array, the same as self
        /// If an array, the type of the underlying item, all arrays removed
        /// </summary>
        public InternalType BaseType { get; }

        public override string ToString()
        {
            //return $"{h.Symbol},[{h.ArrayDimension}],{h.Text}";
            //if (h.ArrayDimension == 0)
            //    return h.Symbol.ToString();
            var arrayText = "";
            for (var i = 0; i < ArrayDimension; ++i)
                arrayText += "[]";
            return $"{BaseType}{arrayText}";
        }
    }

    /// <summary>
    /// A tuple type - list of other types
    /// </summary>
    public class TupleType : InternalType
    {
        public TupleType(
            TypeManager mgr,
            int index,
            List<InternalType> tuple
            )
            : base(mgr,index)
        {
            Tuple = tuple;
        }

        /// <summary>
        /// the types in the tuple
        /// </summary>
        public List<InternalType> Tuple { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Tuple.Count; ++i)
            {
                var t = Tuple[i];
                sb.Append(t);
                if (i != Tuple.Count - 1)
                    sb.Append("*");
            }
            if (Tuple.Count == 0)
                sb.Append("()");
            return sb.ToString();
        }
    }

    /// <summary>
    /// User defined type
    /// </summary>
    public class UserType : InternalType
    {
        public UserType(
            TypeManager mgr,
            int index,
            string name,
            SymbolType symbolType
            ) : base (mgr,index)
        {
            Name = name;
            SymbolType = symbolType;
        }

        /// <summary>
        /// A user type name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Module, Enum, UserType
        /// </summary>
        public SymbolType SymbolType { get; }

        public override string ToString()
        {
            return Name;
        }
    }
    #endregion
}
