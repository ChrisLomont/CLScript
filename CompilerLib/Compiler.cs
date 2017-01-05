using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lomont.ClScript.CompilerLib.AST;
using Lomont.ClScript.CompilerLib.Visitors;

/* TODO
 *
 * DONE  1. Redo array syntax - only C like, not multidim
 * DONE  2. Assignment of complex types needs done
 *  3. Const implemented - values in ROM
 *  4. Remove warning when type member and global var has same name
 *  5. Output C header for link entries
 * DONE  6. Line continues if last non-whitespace is whitespace isolated '\' char
 * DONE  7. Short circuited booleans
 * DONE  8. Add RuntimeException for things that happen during runtime execution
 * DONE  9. Expression eval right to left - reverse this!
 * DONE 10. Code must put array headers on stack
 * 11. Make globals section, binary blob simply loaded into startup memory/stack
 * DONE 12. Need global init code to make stacks (or premake, load buffer)
 * DONE 13. Zero stack creating on locals?
 * DONE 14. Trace capability for runtime
 * DONE 15. Runtime: standalone function call not clearing return values from stack on return
 * 16. Get ++ and -- working (now work as standalone lines)
 * DONE 17. call imports of external functions
 * 18. string testing
 * 19. string interpolation
 * 20. string able to print to external function call
 * DONE 21. Import of files, ensure single import
 * DONE 22. Return complex types
 * 23. Library functions: print, array size, mem used, stack left, etc
 * 24. Type promotion (int=>float, byte=>int, etc, where appropriate?)
 * DONE 25. Locate unused functions, variables, and symbols everywhere
 * 26. Remove unused functions, variables, and symbols everywhere (tricky - can remove functions)
 * 27. assignment of "streams" to "streams" weakens typing - perhaps only allow array setting, setting complex types from same type of piece by piece...
 * 28. Redo sizing so can put bytes on stack, elsewhere (stack node size then 4 bytes, not 1 "stack item")
 * DONE 29. If first item on a line is comment, do not change Indent - indented comments break parser
 * 30. Make assignment generated code much more efficient, especially for single items
 * 31. Add "test" button that runs all regression tests
 * 32. Make an 'unpack' instruction that unpacks a stream of constants from code into an array for those cases
 * 33. Run code "smell" tools to check code for problems
 * 34. Command line compiler
 * 35. Nicer GUI
 * 36. Triple dimension arryas (and higher?) cannot set highest index values on any slot - crashes
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
        /// Tracks messages and info, warning, and errors
        /// </summary>
        public Environment Env { get; }

        /// <summary>
        /// The generated syntax tree
        /// </summary>
        public Ast SyntaxTree { get; private set; }

        public Compiler(Environment environment)
        {
            Env = environment;
        }


        /// <summary>
        /// Provides a way for the compiler to get files. Return null on fail
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public delegate string GetFileText(string filename);

        public bool Compile(string filename, GetFileText fileReader)
        {
            bool success;
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
                    Env.Error($"Exception: {ex.Message}");
                    Env.Error($"Details: {ex}");
                    ex = ex.InnerException;
                } while (ex != null);
                success = false;
            }
            Env.Info($"Compile: {Env.ErrorCount} errors, {Env.WarningCount} warnings.");
            Env.Info(success
                ? "Compilation is successful"
                : $"Failed: {Env.ErrorCount} errors, {Env.WarningCount} warnings");
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


        // Generate the syntax tree
        // return true on success
        bool GenerateSyntaxTree(string startFilename, GetFileText fileReader)
        {
            SyntaxTree = null;

            var filesToImport = new Queue<string>();
            var filesEnqueued = new HashSet<string>();
            filesToImport.Enqueue(startFilename);
            filesEnqueued.Add(startFilename);

            // keep replacing all import statements
            while (filesToImport.Any())
            {
                var filename = filesToImport.Dequeue();

                var source = fileReader(filename);
                if (source == null)
                {
                    Env.Error($"Cannot open file {filename}");
                    return false;
                }

                Env.Info($"Compiling file {filename}");

                lexer = new Lexer.Lexer(Env, source, filename);
                parser = new Parser.Parser(Env, lexer);
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
                            name = name.Trim('"'); // remove quotes
                            if (!filesEnqueued.Contains(name))
                            {
                                filesToImport.Enqueue(name);
                                filesEnqueued.Add(name);
                            }
                        }
                    }
                }
            }

            // reorder tree to make later stages easier to perform
            ReorderSyntaxTree(SyntaxTree);

            return SyntaxTree != null && Env.ErrorCount == 0;
        }

        // reorder tree:
        // remove import statements - they are fulfilled by now
        // first is enums, then types, 
        // then imported items (they might use types), then globals, then functions
        // must track attributes
        void ReorderSyntaxTree(Ast tree)
        {
            if (tree == null) return;

            tree.Children.RemoveAll(n => n is ImportAst);

            var enums     = Extract(tree.Children, n => n is EnumAst);
            var types     = Extract(tree.Children,n => n is TypeDeclarationAst);
            var imports = Extract(tree.Children, n => (n as FunctionDeclarationAst)?.ImportToken != null);
            var globals   = Extract(tree.Children,n => n is VariableDefinitionAst);
            var functions = Extract(tree.Children,n => n is FunctionDeclarationAst && ((FunctionDeclarationAst) n).ImportToken == null);

            if (imports.Count + enums.Count + types.Count + globals.Count + functions.Count != tree.Children.Count)
                Env.Error("Mismatch in ast node counts when reordering syntax tree");
            else
            {
                tree.Children.Clear();
                tree.Children.AddRange(enums);
                tree.Children.AddRange(types);
                tree.Children.AddRange(imports);
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
            Env.Info("Building symbol table...");
            var builder = new BuildSymbolTableVisitor(Env);
            symbolTable = builder.BuildTable(SyntaxTree);
            if (Env.ErrorCount == 0)
            {
                Env.Info("Semantic Analysis...");
                var analyzer = new SemanticAnalyzerVisitor(Env);
                analyzer.Check(symbolTable, SyntaxTree);
                if (Env.ErrorCount == 0)
                {
                    var usage = new SymbolUsageVisitor(Env);
                    usage.Check(symbolTable, SyntaxTree);
                }
            }
            return Env.ErrorCount == 0;
        }

        // generate code from the abstract syntax tree and symbol table
        // return true on success
        bool GenerateCode()
        {
            var cg = new CodeGeneratorVisitor(Env);
            Env.Info("Intermediate Code Generation...");
            var code = cg.Generate(symbolTable, SyntaxTree);
            if (Env.ErrorCount > 0)
                return false;

            generatedInstructions.Clear();
            generatedInstructions.AddRange(code);

            var peep = new PeepholeOptimizer(Env);
            peep.Optimize(generatedInstructions);

            bytecode = new BytecodeGen(Env);
            Env.Info("Bytecode Generation...");
            var retval = bytecode.Generate(generatedInstructions);
            if (retval)
                Env.Info($"  ...bytecode assembly {bytecode.CompiledAssembly.Length} bytes");
            return retval;
        }


        #endregion

    }
}
