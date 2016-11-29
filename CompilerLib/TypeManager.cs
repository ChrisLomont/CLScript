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
                var symbolMatches = e.Symbol == symbolType || symbolType == SymbolType.MatchAny;
                var userMatches = String.IsNullOrEmpty(userTypename) || userTypename == e.Text;
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

        string FormatTypeList(List<InternalType> list)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count; ++i)
            {
                var t = list[i];
                sb.Append(t);
                if (i != list.Count-1)
                    sb.Append(" * ");
            }
            return sb.ToString();
        }

        public string GetTypeText(int typeIndex)
        {
            var h = types[typeIndex];

            if (h.Symbol == SymbolType.Function)
            {
                var ret = FormatTypeList(h.ReturnType);
                var par = FormatTypeList(h.ParamsType);
                return $"{h.Symbol} {par} => {ret}";
            }


            if (h.ArrayDimension == 0)
                return h.Symbol.ToString();

            var arrayText = "";
            if (h.ArrayDimension > 0)
                arrayText = "[" + new string(',', h.ArrayDimension - 1) + "]";

            if (!String.IsNullOrEmpty(h.Text))
                return $"{h.Text}{arrayText}";
            return $"{h.Symbol}{arrayText}";
        }


        #region implementation

        List<HiddenType> types = new List<HiddenType>();

        // an internal representation to store type items
        class HiddenType
        {
            public HiddenType(SymbolType type, string name, TypeManager mgr, int index, 
                int arrayDimension = 0, List<InternalType> returnType = null, List<InternalType> paramsType = null)
            {
                Text           = name;
                Symbol         = type;
                InternalType   = new InternalType(mgr, index);
                ArrayDimension = arrayDimension;
                ReturnType     = returnType;
                ParamsType     = paramsType;
            }

            public int ArrayDimension;
            public SymbolType Symbol;
            public string Text;
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

        #region Equality
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
        #endregion

        readonly TypeManager typeManager;
        readonly int index;

    }
}
