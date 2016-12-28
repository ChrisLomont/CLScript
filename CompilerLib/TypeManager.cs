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
            foreach (var t in Types.Where(t1 => t1 is SimpleType).Select(t2 => (SimpleType)t2))
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
            foreach (var t in Types.Where(t1 => t1 is TupleType).Select(t2 => (TupleType)t2))
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
            foreach (var t in Types.Where(t1 => t1 is FunctionType).Select(t2 => (FunctionType)t2))
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
            foreach (var t in Types.Where(t1 => t1 is ArrayType).Select(t2 => (ArrayType)t2))
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
        public InternalType GetType(string typeName)
        {
            foreach (var t in Types.Where(t1 => t1 is UserType).Select(t2 => (UserType)t2))
            {
                if (t.Name == typeName)
                    return t;
            }
            var type = new UserType(this, Types.Count,typeName);
            Types.Add(type);
            return type;
        }

        /*

                /// <summary>
                /// Lookup type, if not created, do so
                /// </summary>
                /// <param name="symbolType"></param>
                /// <param name="arrayDimension"></param>
                /// <param name="userTypename"></param>
                /// <param name="returnType"></param>
                /// <param name="paramsType"></param>
                /// <returns></returns>
                public InternalType GetTypeOLD(
                    SymbolType symbolType,
                    int arrayDimension = 0,
                    string userTypename = "",
                    List<InternalType> returnType = null,
                    List<InternalType> paramsType = null)
                {


                    foreach (var e in Types)
                    {
                        var symbolMatches = e.SymbolType == symbolType || symbolType == SymbolType.MatchAny;
                        var userMatches = String.IsNullOrEmpty(userTypename) || userTypename == e.UserTypeName;
                        if (symbolMatches &&
                            arrayDimension == e.ArrayDimension && 
                            userMatches &&
                            ListMatches(e.ReturnType, returnType) &&
                            ListMatches(e.ParamsType, paramsType)
                        )
                            return e;
                    }

                    var type = new InternalType(
                        symbolType, 
                        userTypename, 
                        this, 
                        Types.Count, 
                        arrayDimension, 
                        returnType,
                        paramsType);
                    Types.Add(type);
                    return type;
                }
        */

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

/*        public InternalType GetBaseType(InternalType type)
        {
            if (type.ArrayDimension == 0)
                return type;
            else if (type.SymbolType != SymbolType.UserType)
                return GetType(type.SymbolType);
            else
            {
                var userName = type.UserTypeName;
                throw new NotImplementedException("Type feature not yet implemented");
                foreach (var t in Types)
                {
                }
                env.Error($"Could not match base type for {type}");
                return null;
            }
        } */
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
        public SymbolType SymbolType { get; private set; }

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

        public string Name { get; private set; }

        /// <summary>
        /// return types for function type
        /// </summary>
        public TupleType ReturnType { get; private set; }
        /// <summary>
        /// parameter types for function type
        /// </summary>
        public TupleType ParamsType { get; private set; }

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
        public int ArrayDimension { get; private set; }

        /// <summary>
        /// The type of the base item.
        /// If not an array, the same as self
        /// If an array, the type of the underlying item, all arrays removed
        /// </summary>
        public InternalType BaseType { get; private set; }

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
        public List<InternalType> Tuple { get; private set; }

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
            string name
            ) : base (mgr,index)
        {
            Name = name;
        }

        /// <summary>
        /// A user type name
        /// </summary>
        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
    #endregion
}
