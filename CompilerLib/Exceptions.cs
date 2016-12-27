using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{

    /// <summary>
    /// Fatal error during runtime
    /// </summary>
    public class RuntimeException : Exception
    {
        public RuntimeException(string format) : base(format)
        {
        }
    }

    public class InvalidExpression : Exception
    {
        public InvalidExpression(string format) : base(format)
        {
        }
    }

    /// <summary>
    /// Exception when internal consistency check fails
    /// </summary>
    public class InternalFailure : Exception
    {
        public InternalFailure(string format) : base(format)
        {
        }
    }

    /// <summary>
    /// Syntax error during lexing/parsing
    /// </summary>
    public class InvalidSyntax : Exception
    {
        public InvalidSyntax(string format) : base(format)
        {
        }
    }

    /// <summary>
    /// Illegal character in lexer
    /// </summary>
    public class IllegalCharacter : Exception
    {
        public IllegalCharacter(string format) : base(format)
        {
        }
    }
}
