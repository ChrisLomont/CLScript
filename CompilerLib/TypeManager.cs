using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Tracks types, giving each a unique index, perform formatting
    /// </summary>
    public class TypeManager
    {
        Environment env;
        public TypeManager(Environment environment)
        {
            env = environment;
        }

        /// <summary>
        /// Used to add basic types like 'bool' and 'function' to the system
        /// </summary>
        /// <param name="symbolType"></param>
        /// <param name="text"></param>
        public void AddBasicType(SymbolType symbolType, string text)
        {
            var type = new InternalType(symbolType, text, this, Types.Count);
            Types.Add(type);
        }

        /// <summary>
        /// Lookup type, if not created, do so
        /// </summary>
        /// <param name="symbolType"></param>
        /// <param name="arrayDimension"></param>
        /// <param name="userTypename"></param>
        /// <param name="returnType"></param>
        /// <param name="paramsType"></param>
        /// <returns></returns>
        public InternalType GetType(
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

        public InternalType GetBaseType(InternalType type)
        {
            if (type.ArrayDimension == 0)
                return type;
            else if (type.SymbolType != SymbolType.UserType1)
                return GetType(type.SymbolType);
            else
            {
                var userName = type.UserTypeName;
                foreach (var t in Types)
                {
                }
                env.Error($"Could not match base type for {type}");
                return null;
            }
        }
    }

    /// <summary>
    /// Represent a type used for semantic analysis
    /// </summary>
    public class InternalType
    {

        public InternalType(SymbolType type, 
            string name, 
            TypeManager mgr, 
            int index,
            int arrayDimension = 0,
            List<InternalType> returnType = null, 
            List<InternalType> paramsType = null)
        {

            typeManager = mgr;
            typeIndex = index;
            SymbolType = type;
            UserTypeName = name;
            ArrayDimension = arrayDimension;
            ReturnType = returnType;
            ParamsType = paramsType;
        }

        /// <summary>
        /// dimension of multidimensional arrays
        /// 0 is no array
        /// 1 is single array
        /// etc.
        /// </summary>
        public int ArrayDimension { get; private set; }
        /// <summary>
        /// Underlying symbol type
        /// </summary>
        public SymbolType SymbolType { get; private set; }
        /// <summary>
        /// A user type name
        /// </summary>
        public string UserTypeName { get; private set; }
        
        /// <summary>
        /// return types for function type
        /// </summary>
        public List<InternalType> ReturnType { get; private set; }
        /// <summary>
        /// parameter types for function type
        /// </summary>
        public List<InternalType> ParamsType { get; private set; }

        /// <summary>
        /// The type of the base item.
        /// If not an array, the same as self
        /// If an array, the type of the underlying item, all arrays removed
        /// </summary>
        public InternalType BaseType { get; set; }

        public bool PassByRef
        {
            get
            {
                if (ArrayDimension>0)
                    return true;
                if (SymbolType == SymbolType.Bool || 
                    SymbolType == SymbolType.Byte || 
                    SymbolType == SymbolType.Float32 ||
                    SymbolType == SymbolType.Int32 ||
                    SymbolType == SymbolType.EnumValue
                    )
                    return false;
                return true;
            }

        }
        public override string ToString()
        {
            //return $"{h.Symbol},[{h.ArrayDimension}],{h.Text}";
            if (SymbolType == SymbolType.Function)
            {
                var ret = FormatTypeList(ReturnType);
                if (String.IsNullOrEmpty(ret))
                    ret = "()";
                var par = FormatTypeList(ParamsType);
                if (String.IsNullOrEmpty(par))
                    par = "()";
                return $"{par} => {ret}";
            }
            //if (h.ArrayDimension == 0)
            //    return h.Symbol.ToString();
            var arrayText = "";
            for (var i = 0; i < ArrayDimension; ++i)
                    arrayText += "[]";
            if (!String.IsNullOrEmpty(UserTypeName))
                return $"{UserTypeName}{arrayText}";
            return $"{SymbolType}{arrayText}";
        }


        static string FormatTypeList(List<InternalType> list)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count; ++i)
            {
                var t = list[i];
                sb.Append(t);
                if (i != list.Count - 1)
                    sb.Append("*");
            }
            return sb.ToString();
        }


        #region Equality

        public override bool Equals(Object obj)
        {
            return obj is InternalType && this == (InternalType) obj;
        }

        public override int GetHashCode()
        {
            return typeManager.GetHashCode() ^ typeIndex.GetHashCode();
        }

        public static bool operator ==(InternalType a, InternalType b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
                return true;

            // If one is null, but not both, return false.
            if (((object) a == null) || ((object) b == null))
                return false;

            return ReferenceEquals(a.typeManager, b.typeManager) && a.typeIndex == b.typeIndex;
        }

        public static bool operator !=(InternalType x, InternalType y)
        {
            return !(x == y);
        }

        #endregion

        readonly TypeManager typeManager;
        readonly int typeIndex;

    }
}
