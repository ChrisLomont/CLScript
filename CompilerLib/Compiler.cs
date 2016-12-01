using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Parser;
using Lomont.ClScript.CompilerLib.Visitors;

namespace Lomont.ClScript.CompilerLib
{
    public class Compiler
    {

        Lexer.Lexer lexer;
        Parser.Parser parser;

        public Ast SyntaxTree { get; private set; }

        public string SyntaxTreeToText()
        {
            if (SyntaxTree != null)
            {
                var output = new StringWriter();
                var pr = new PrintVisitor(output);
                pr.Start(SyntaxTree);
                return output.ToString();
            }
            return "No syntax tree to show\n";
        }

        public void Compile(string text, Environment environment, out string result)
        {
            result = "";
            environment.Info($"Compiling <todo> lines");
            try
            {
                lexer = new Lexer.Lexer(text, environment);
                parser = new Parser.Parser(lexer);
                SyntaxTree = parser.Parse(environment);
                if (SyntaxTree != null)
                {
                    var success = ProcessTree(SyntaxTree, environment);
                }
            }
            catch (Exception ex)
            {
                do
                {
                    environment.Error($"Exception: {ex.Message}");
                    environment.Error($"Details: {ex}");
                    ex = ex.InnerException;
                } while (ex != null);
            }
        }

        // do compiler tree transforms
        // return true on success
        bool ProcessTree(Ast ast, Environment environment)
        {
            var symbolTable = BuildSymbolTableVisitor.BuildTable(ast,environment);
            SemanticAnalyzerVisitor.Check(symbolTable, ast, environment);
            var cg = new CodeGeneratorVisitor();
            cg.Generate(symbolTable, ast, environment);

            symbolTable.Dump(environment.Output);

            return environment.ErrorCount == 0;
        }

        // helper function to tag items for gold parser
        string LexToGoldParser(Lexer.Lexer lexer)
        {
            var filterTokens = true; // causes comments to be skipped, etc
            var sb = new StringBuilder();
            foreach (var token in lexer.Lex(filterTokens))
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

        public List<Token> GetTokens()
        {
            if (parser != null)
                return parser.GetTokens();
            return new List<Token>();
        }
    }
}
