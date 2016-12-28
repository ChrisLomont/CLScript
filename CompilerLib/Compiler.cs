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
 * DONE 1. Redo array syntax - only C like, not multidim
 *  2. Assignment of complex types needs done
 *  3. Const implemented - values in ROM
 *  4. Remove warning when type member and global var has same name
 *  5. Output C header for link entries
 *  6. Line continues if last non-whitespace is whitespace isolated '\' char
 *  7. Short circuited booleans
 * DONE  8. Add RuntimeException for things that happen during runtime execution
 * DONE  9. Expression eval right to left - reverse this!
 * DONE 10. Code must put array headers on stack
 * 11. Make globals section, binary blob simply loaded into startup memory/stack
 * DONE 12. Need global init code to make stacks (or premake, load buffer)
 * DONE 13. Zero stack creating on locals?
 * DONE 14. Trace capability for runtime
 * DONE 15. Runtime: standalone function call not clearing return values from stack on return
 * 16. Get ++ and -- working
 * DONE 17. call imports of external functions
 * 18. string testing
 * 19. string interpolation
 * 20. string able to print to external function call
 * DONE 21. Import of files, ensure single import
 * 22. Return complex types
 * 23. Library functions: print, array size
 * 24. Type promotion (int=>float, byte=>int, etc, where appropriate?)
 * DONE 25. Locate unused functions, variables, and symbols everywhere
 * 26. Remove unused functions, variables, and symbols everywhere (tricky - can remove functions)
 * 27. assignment of "streams" to "streams" weakens typing - perhaps only allow array setting, setting complex types from same type of piece by piece...
 * 
 * To get usable in production:  
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


        /// <summary>
        /// Provides a way for the compiler to get files. Return null on fail
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public delegate string GetFileText(string filename);

        public bool Compile(string filename, GetFileText fileReader)
        {
            var success = false;
            try
            {
                success = 
                    GenerateSyntaxTree(filename,fileReader) &&
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
            env.Info($"Compile: {env.ErrorCount} errors, {env.WarningCount} warnings.");
            if (success)
                env.Info("Compilation is successful");
            else
                env.Info($"Failed: {env.ErrorCount} errors, {env.WarningCount} warnings");
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
        bool GenerateSyntaxTree(string startFilename, GetFileText fileReader)
        {
            SyntaxTree = null;

            var filesToImport = new Queue<string>();
            var filesImported = new HashSet<string>();
            filesToImport.Enqueue(startFilename);

            // keep replacing all import statements
            while (filesToImport.Any())
            {
                var filename = filesToImport.Dequeue();
                filesImported.Add(filename);

                var source = fileReader(filename);
                if (source == null)
                {
                    env.Error($"Cannot open file {filename}");
                    return false;
                }

                env.Info($"Compiling file {filename}");

                lexer = new Lexer.Lexer(env, source, filename);
                parser = new Parser.Parser(env, lexer);
                var tree = parser.Parse();
                if (SyntaxTree == null)
                    SyntaxTree = tree;
                else
                {
                    // merge all top level into given node
                    SyntaxTree.Children.AddRange(tree.Children);
                }

                // enqueue all unseen import filenames from most recent file parsed
                if (tree != null)
                {
                    foreach (var ch in SyntaxTree.Children)
                    {
                        if (ch is ImportAst)
                        {
                            var name = (ch as ImportAst).Name;
                            name = name.Trim(new char[] {'"'}); // remove quotes
                            if (!filesImported.Contains(name))
                                filesToImport.Enqueue(name);
                        }
                    }
                }
            }

            // reorder tree to make later stages easier to perform
            ReorderSyntaxTree(SyntaxTree);

            return SyntaxTree != null;
        }

        // reorder tree:
        // remove import statements - they are fulfilled by now
        // first is imported items, then types, then globals, then functions
        // must track attributes
        void ReorderSyntaxTree(Ast tree)
        {
            if (tree == null) return;

            tree.Children.RemoveAll(n => n is ImportAst);

            var imports = Extract(tree.Children, n=>n is FunctionDeclarationAst && (n as FunctionDeclarationAst).ImportToken != null);
            var types = Extract(tree.Children,n => n is TypeDeclarationAst);
            var globals = Extract(tree.Children,n => n is VariableDefinitionAst);
            var functions = Extract(tree.Children,n => n is FunctionDeclarationAst && (n as FunctionDeclarationAst).ImportToken == null);

            if (imports.Count + types.Count + globals.Count + functions.Count != tree.Children.Count)
                env.Error($"Mismatch in ast node counts when reordering syntax tree");
            else
            {
                tree.Children.Clear();
                tree.Children.AddRange(imports);
                tree.Children.AddRange(types);
                tree.Children.AddRange(globals);
                tree.Children.AddRange(functions);
            }
        }

        static List<Ast> Extract(List<Ast> asts, Func<Ast,bool> predicate)
        {
            var list = new List<Ast>();
            for (var i = 0; i < asts.Count; ++i)
            {
                if (predicate(asts[i]))
                { // get preceeding attributes
                    var j = 0;
                    while (i-j-1>=0 && (asts[i-j-1] is AttributeAst))
                        j++;
                    for (var k = i-j; k <= i; ++k)
                        list.Add(asts[k]);
                }
            }
            return list;
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
                if (env.ErrorCount == 0)
                {
                    var usage = new SymbolUsageVisitor(env);
                    usage.Check(symbolTable, SyntaxTree);
                }
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

            var peep = new PeepholeOptimizer(env);
            peep.Optimize(generatedInstructions);

            bytecode = new BytecodeGen(env);
            env.Info("Bytecode Generation...");
            var retval = bytecode.Generate(symbolTable, generatedInstructions);
            if (retval)
                env.Info($"  ...bytecode assembly {bytecode.CompiledAssembly.Length} bytes");
            return retval;
        }


        #endregion

    }
}
