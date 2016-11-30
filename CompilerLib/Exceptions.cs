using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public class InvalidExpression : Exception
    {
        public InvalidExpression(string format) : base(format)
        {
        }
    }

    public class InternalFailure : Exception
    {
        public InternalFailure(string format) : base(format)
        {
        }
    }

    public class InvalidSyntax : Exception
    {
        public InvalidSyntax(string format) : base(format)
        {
        }
    }
    public class IllegalCharacter : Exception
    {
        public IllegalCharacter(string format) : base(format)
        {
        }
    }

    public class ReturnException : Exception
    {
        public dynamic Value { get; private set; }

        public ReturnException(dynamic value)
        {
            Value = value;
        }
    }

    public class UndefinedElementException : Exception
    {
        public UndefinedElementException(string msg, params string[] param) : base(String.Format(msg, param))
        {

        }
    }
}
