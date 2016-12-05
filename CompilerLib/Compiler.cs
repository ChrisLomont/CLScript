using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection.Emit;
using System.ServiceModel;
using System.Text;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Parser;
using Lomont.ClScript.CompilerLib.Visitors;

/* TODO
 *
 * 1. Redo array syntax - only C like, not multidim
 * 2. assignment of complex types needs done
 * 3. const implemented - values in ROM
 * 4. 
 * 
 */

namespace Lomont.ClScript.CompilerLib
{
    public class Compiler
    {
        /// <summary>
        /// The final binary image
        /// </summary>
        public byte[] CompiledAssembly => bytecode?.CompiledAssembly;

        /// <summary>
        /// The generated syntax tree
        /// </summary>
        public Ast SyntaxTree { get; private set; }

        public bool Compile(string text, Environment environment1)
        {
            this.environment = environment1;
            environment.Info($"Compiling <todo> lines");
            var success = false;
            try
            {
                success = 
                    GenerateSyntaxTree(text) &&
                    AnalyzeSyntaxTree() && 
                    GenerateCode();
            }
            catch (Exception ex)
            {
                do
                {
                    environment.Error($"Exception: {ex.Message}");
                    environment.Error($"Details: {ex}");
                    ex = ex.InnerException;
                } while (ex != null);
                success = false;
            }
            return success;
        }


        #region Debugging functions
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

        public string SymbolTableToText()
        {
            var st = new StringWriter();
            if (symbolTable != null)
                symbolTable.Dump(st);
            return st.ToString();
        }

        public string CodegenToText()
        {
            var sb = new StringBuilder();
            foreach (var inst in codeGen)
                sb.AppendLine(inst.ToString());
            return sb.ToString();
        }


        public List<Token> GetTokens()
        {
            if (parser != null)
                return parser.GetTokens();
            return new List<Token>();
        }
        #endregion

        #region Implementation
        Lexer.Lexer lexer;
        Parser.Parser parser;
        List<Instruction> codeGen = new List<Instruction>();
        SymbolTableManager symbolTable;
        BytecodeGen bytecode;
        Environment environment;

        // Generate the syntax tree
        // return true on success
        bool GenerateSyntaxTree(string text)
        {
            SyntaxTree = null;
            lexer      = new Lexer.Lexer(text, environment);
            parser     = new Parser.Parser(lexer);
            SyntaxTree = parser.Parse(environment);
            return SyntaxTree != null;
        }

        // analyze tree, perform tree manipulations, build symbol table, etc.
        // return true on success
        bool AnalyzeSyntaxTree()
        {
            environment.Info("Building symbol table...");
            symbolTable = BuildSymbolTableVisitor.BuildTable(SyntaxTree, environment);
            if (environment.ErrorCount == 0)
            {
                environment.Info("Semantic Analysis...");
                SemanticAnalyzerVisitor.Check(symbolTable, SyntaxTree, environment);
            }
            return environment.ErrorCount == 0;
        }

        // generate code from the abstract syntax tree and symbol table
        // return true on success
        bool GenerateCode()
        {
            var cg = new CodeGeneratorVisitor();
            var code = cg.Generate(symbolTable, SyntaxTree, environment);
            if (environment.ErrorCount > 0)
                return false;

            codeGen.Clear();
            codeGen.AddRange(code);
            bytecode = new BytecodeGen();
            return bytecode.Generate(environment, symbolTable, codeGen);
        }


        #endregion

    }
}
