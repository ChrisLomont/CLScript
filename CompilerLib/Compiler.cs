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
 * 4. remove warning when type member and global var has same name
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

        public Compiler(Environment environment)
        {
            env = environment;
        }

        public bool Compile(string text)
        {
            env.Info($"Compiling <todo> lines");
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
                    env.Error($"Exception: {ex.Message}");
                    env.Error($"Details: {ex}");
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
            symbolTable?.Dump(st);
            return st.ToString();
        }

        public string CodegenToText()
        {
            var sb = new StringBuilder();
            foreach (var inst in generatedInstructions)
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
        readonly List<Instruction> generatedInstructions = new List<Instruction>();
        SymbolTableManager symbolTable;
        BytecodeGen bytecode;
        readonly Environment env;

        // Generate the syntax tree
        // return true on success
        bool GenerateSyntaxTree(string text)
        {
            SyntaxTree = null;
            lexer      = new Lexer.Lexer(env, text);
            parser     = new Parser.Parser(env, lexer);
            SyntaxTree = parser.Parse();
            return SyntaxTree != null;
        }

        // analyze tree, perform tree manipulations, build symbol table, etc.
        // return true on success
        bool AnalyzeSyntaxTree()
        {
            env.Info("Building symbol table...");
            var builder = new BuildSymbolTableVisitor(env);
            symbolTable = builder.BuildTable(SyntaxTree);
            if (env.ErrorCount == 0)
            {
                env.Info("Semantic Analysis...");
                var analyzer = new SemanticAnalyzerVisitor(env);
                analyzer.Check(symbolTable, SyntaxTree);
            }
            return env.ErrorCount == 0;
        }

        // generate code from the abstract syntax tree and symbol table
        // return true on success
        bool GenerateCode()
        {
            var cg = new CodeGeneratorVisitor(env);
            env.Info("Intermediate Code Generation...");
            var code = cg.Generate(symbolTable, SyntaxTree);
            if (env.ErrorCount > 0)
                return false;

            generatedInstructions.Clear();
            generatedInstructions.AddRange(code);
            bytecode = new BytecodeGen(env);
            env.Info("Bytecode Generation...");
            return bytecode.Generate(symbolTable, generatedInstructions);
        }


        #endregion

    }
}
