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

            var type = new InternalType(symbolType, userTypename, this, Types.Count, arrayDimension, returnType,
                paramsType);
            Types.Add(type);
            return type;
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
            for (var i = 0; i < list1.Count; ++i)
                if (list1[i] != list2[i])
                    return false;
            return true;
        }

        // store unique types here
        public List<InternalType> Types { get;  } = new List<InternalType>();

    }

    /// <summary>
    /// Represent a type used for semantic analysis
    /// </summary>
    public class InternalType
    {

        public InternalType(SymbolType type, string name, TypeManager mgr, int index,
            int arrayDimension = 0, List<InternalType> returnType = null, List<InternalType> paramsType = null)
        {

            this.typeManager = mgr;
            this.index = index;
            SymbolType = type;
            UserTypeName = name;
            ArrayDimension = arrayDimension;
            ReturnType = returnType;
            ParamsType = paramsType;
        }

        public SymbolType SymbolType { get; set; }
        public int ArrayDimension { get; set; }
        public string UserTypeName { get; set; }
        public List<InternalType> ReturnType { get; set; }
        public List<InternalType> ParamsType { get; set; }

        public int? Size { get; set; }


        public override string ToString()
        {
            var sizeText = Size.HasValue ? ":" + Size.ToString() + ":" : "";
            //return $"{h.Symbol},[{h.ArrayDimension}],{h.Text}";
            if (SymbolType == SymbolType.Function)
            {
                var ret = FormatTypeList(ReturnType);
                var par = FormatTypeList(ParamsType);
                return $"{SymbolType} {par} => {ret}";
            }
            //if (h.ArrayDimension == 0)
            //    return h.Symbol.ToString();
            var arrayText = "";
            if (ArrayDimension > 0)
                arrayText = "[" + new string(',', ArrayDimension - 1) + "]";
            if (!String.IsNullOrEmpty(UserTypeName))
                return $"{UserTypeName}{arrayText}{sizeText}";
            return $"{SymbolType}{arrayText}{sizeText}";
        }

        static string FormatTypeList(List<InternalType> list)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < list.Count; ++i)
            {
                var t = list[i];
                sb.Append(t);
                if (i != list.Count - 1)
                    sb.Append(" * ");
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
            return typeManager.GetHashCode() ^ index.GetHashCode();
        }

        public static bool operator ==(InternalType a, InternalType b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
                return true;

            // If one is null, but not both, return false.
            if (((object) a == null) || ((object) b == null))
                return false;

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
