using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{
    public class Compiler
    {

        Lexer.Lexer lexer;

        public void Compile(string text, Environment environment, out string result)
        {
            bool dumpLex = false;
            result = "";
            environment.Output.WriteLine($"Compiling <todo> lines");
            try
            {
                lexer = new Lexer.Lexer(text, environment);
                if (dumpLex)
                {
                    result = LexToGoldParser(lexer);
                    environment.Output.WriteLine(result);
                    lexer = new Lexer.Lexer(text, environment);
                }
                var parser = new Parser.Parser(lexer);
                var ast = parser.Parse(environment);
                if (ast != null)
                {
                    ast = ProcessTree(ast, environment);
                    var pr = new PrintVisitor(environment);
                    pr.Start(ast);
                }
            }
            catch (Exception ex)
            {
                do
                {
                    environment.Output.WriteLine($"Error: {ex.Message}");
                    ex = ex.InnerException;
                } while (ex != null);
            }
        }

        // do compiler tree transforms
        Ast ProcessTree(Ast ast, Environment environment)
        {
            var pass1 = new CollapseHelperVisitor(environment);
            pass1.Start(ast);

            return ast;
        }

        // helper function to tag items for gold parser
        string LexToGoldParser(Lexer.Lexer lexer)
        {
            var sb = new StringBuilder();
            foreach (var token in lexer.Lex(false))
            {
                if (token.TokenType == TokenType.Comment)
                    continue;
                else if (token.TokenType == TokenType.EndOfLine)
                    sb.Append(" `E\n");
                else if (token.TokenType == TokenType.Indent)
                    sb.Append(" `I ");
                else if (token.TokenType == TokenType.Undent)
                    sb.Append(" `U ");
                else
                    sb.Append(token.TokenValue);
            }
            return sb.ToString();
        }

    }
}
