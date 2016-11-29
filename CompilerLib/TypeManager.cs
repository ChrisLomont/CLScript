using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    /// <summary>
    /// Tracks types, giving each a unique index, and performs formatting
    /// </summary>
    public class TypeManager
    {
        /// <summary>
        /// Used to add basic types like 'bool' and 'function' to the system
        /// </summary>
        /// <param name="symbolType"></param>
        /// <param name="text"></param>
        public void AddBasicType(SymbolType symbolType, string text)
        {
            var type = new HiddenType(symbolType, text, this, types.Count);
            types.Add(type);
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
            foreach (var e in types)
            {
                var symbolMatches = e.symbol == symbolType || symbolType == SymbolType.MatchAny;
                var userMatches = String.IsNullOrEmpty(userTypename) || userTypename == e.UserType;
                if (symbolMatches && 
                    arrayDimension == e.ArrayDimension  &&
                    userMatches &&
                    ListMatches(e.ReturnType,returnType) &&
                    ListMatches(e.ParamsType, paramsType)
                    )
                    return e.InternalType;
            }

            var type = new HiddenType(symbolType, userTypename, this, types.Count, arrayDimension,returnType,paramsType);
            types.Add(type);
            return type.InternalType;
        }

        bool ListMatches(List<InternalType> list1, List<InternalType> list2)
        {
            if (list1 == null && list2 == null)
                return true;
            if (list1 == null && list2 != null)
                return false;
            if (list1 != null && list2 == null)
                return false;
            if (ReferenceEquals(list1, list2))
                return true;
            if (list1.Count != list2.Count)
                return false;
            for (var i =0; i < list1.Count; ++i)
                if (list1[i] != list2[i])
                    return false;
            return true;
        }

        /// <summary>
        /// Lookup type, if not created, do so
        /// </summary>
        /// <param name="typeString"></param>
        /// <param name="arrayDimension"></param>
        /// <returns></returns>
        public InternalType GetType(string typeString, int arrayDimension)
        {
            return GetType(SymbolType.MatchAny, arrayDimension,typeString);
        }

        public string GetTypeText(int typeIndex)
        {
            var h = types[typeIndex];
            return h.symbol.ToString() + "[TODO]";
//            if (Type.IsFunction)
//            {
//                return $"{Type} {ParamsType} => {ReturnType}";
//            }
//
//            if (ArrayDimension == 0 && UserType == null)
//                return Type.ToString();
//            var arrayText = "";
//            if (ArrayDimension > 0)
//                arrayText = "[" + new string(',', ArrayDimension - 1) + "]";
//            var userText = !String.IsNullOrEmpty(UserType) ? $" of {UserType}" : "";
//            return $"{Type}{arrayText}{userText}";
        }


        #region implementation

        List<HiddenType> types = new List<HiddenType>();

        // an internal representation to store type items
        class HiddenType
        {
            public HiddenType(SymbolType type, string name, TypeManager mgr, int index, 
                int arrayDimension = 0, List<InternalType> returnType = null, List<InternalType> paramsType = null)
            {
                text           = name;
                symbol         = type;
                InternalType   = new InternalType(mgr, index);
                ArrayDimension = arrayDimension;
                ReturnType     = returnType;
                ParamsType     = paramsType;
            }

            public string UserType;
            public int ArrayDimension;
            public bool IsFunction;
            public SymbolType symbol;
            public string text;
            public InternalType InternalType;
            public List<InternalType> ReturnType;
            public List<InternalType> ParamsType;
        }
        #endregion
    }

    /// <summary>
    /// Represent a type used for semantic analysis
    /// </summary>
    public class InternalType
    {
        public InternalType(TypeManager typeManager, int index)
        {
            this.typeManager = typeManager;
            this.index = index;
        }

        public override string ToString()
        {
            return typeManager.GetTypeText(index);
        }

        public override bool Equals(Object obj)
        {
            return obj is InternalType && this == (InternalType)obj;
        }
        public override int GetHashCode()
        {
            return typeManager.GetHashCode() ^ index.GetHashCode();
        }
        public static bool operator ==(InternalType a, InternalType b)
        {
            return ReferenceEquals(a.typeManager, b.typeManager) && a.index == b.index;
        }
        public static bool operator !=(InternalType x, InternalType y)
        {
            return !(x == y);
        }

        readonly TypeManager typeManager;
        readonly int index;

    }
}
