using System;
using System.Collections.Generic;
using System.Linq;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Visitors
{
    class CodeGeneratorVisitor
    {
        SymbolTableManager mgr;
        Environment env;

        // where we store instructions as they are generated
        List<Instruction> instructions = new List<Instruction>();

        // stack of labels for break and continue
        Stack<string> loopBreakLabels = new Stack<string>();
        Stack<string> loopContinueLabels = new Stack<string>();

        public CodeGeneratorVisitor(Environment environment)
        {
            env = environment;
        }


        public List<Instruction> Generate(SymbolTableManager symbolTable, Ast ast)
        {
            // set object fields 
            mgr = symbolTable;

            // layout variables in memory
            symbolTable.Start();
            LayoutMemory(ast);
            if (env.ErrorCount > 0)
                return null;

            // generate code
            symbolTable.Start();
            Recurse(ast);
            if (env.ErrorCount > 0)
                return null;

            return instructions;
        }

        bool globalStarted = false;
        bool globalEnded = false;
        int globalStartIndex = 0;
        public static string GlobalStartSymbol = "<global_start>";
        public static string GlobalEndSymbol = "<global_end>";
        void EmitGlobalDelimiter(bool start)
        {
            if (start)
            {
                if (!globalStarted && !globalEnded)
                {
                    EmitS(Opcode.Label, GlobalStartSymbol);
                    globalStartIndex = instructions.Count;
                    Emit2(Opcode.ClearStack, OperandType.None, "globals stack space", GetGlobalsStackSize());
                    MakeArrays(); // make any array structures
                }
                globalStarted = true;
            }
            else
            {
                if (!globalEnded && globalStarted)
                {
                    EmitS(Opcode.Label, GlobalEndSymbol);
                    if (instructions.Count != globalStartIndex)
                    {
                        Emit2(Opcode.Return, OperandType.None, "",
                            0, // parameter count
                            0 // local stack entries
                        );
                    }
                }
                globalEnded = true;
            }
        }

        // size in stack entries of global variables
        int GetGlobalsStackSize()
        {
            var size = 0;
            foreach (var e in mgr.RootTable.Entries)
                if (e.StackSize > 0 && e.VariableUse == VariableUse.Global)
                    size += e.StackSize;
            return size; // mgr.RootTable.Entries.Sum(e => e.StackSize);
        }

        void Recurse(Ast node)
        {
            if (node is ExpressionAst)
            {
                EmitExpression((ExpressionAst) node);
                return;
            }
            else if (node is AssignStatementAst)
            {
                EmitAssignStatement((AssignStatementAst) node);
                return;
            }
            else if (node is VariableDefinitionAst)
            {
                EmitGlobalDelimiter(true); // possible start of globals
                EmitVariableDef((VariableDefinitionAst) node);
                return;
            }
            else if (node is FunctionDeclarationAst)
            {
                if ((node as FunctionDeclarationAst).ImportToken == null)
                    EmitGlobalDelimiter(false); // first non-import function declaration marks end of globals
                EmitFunction((FunctionDeclarationAst) node);
                return;
            }
            else if (node is TypeDeclarationAst)
                return; // no code emitted for types
            else if (node is IfStatementAst)
            {
                EmitIfStatement((IfStatementAst) node);
                return;
            }
            else if (node is WhileStatementAst)
            {
                EmitWhileStatement((WhileStatementAst) node);
                return;
            }
            else if (node is ForStatementAst)
            {
                EmitForStatement((ForStatementAst) node);
                return;
            }
            else if (node is JumpStatementAst)
            {
                EmitJumpStatement((JumpStatementAst) node);
                return;
            }
            else if (node is AttributeAst)
                return; // skip this - atttibute attached to symbol elsewhere

            mgr.EnterAst(node);

            if (node is BlockAst)
                MakeArrays(); // make any array structures


            // recurse children
            foreach (var child in node.Children)
                Recurse(child);
            mgr.ExitAst(node);
        }

        void EmitJumpStatement(JumpStatementAst node)
        {
            switch (node.Token.TokenType)
            {
                case TokenType.Continue:
                    EmitS(Opcode.BrAlways, loopContinueLabels.Peek());
                    break;
                case TokenType.Break:
                    EmitS(Opcode.BrAlways, loopBreakLabels.Peek());
                    break;
                case TokenType.Return:
                    EmitReturn(node);
                    break;
                default:
                    throw new InternalFailure($"Emit does not know return type {node}");
            }
        }


        // for loop stores a counter and a delta
        public const int ForLoopStackSize = 2; // number of for loop stack entries (index,delta)

        void EmitForStatement(ForStatementAst node)
        {
            var exprs = node.Children[0];

            // put three integers on stack: 
            // a - start loop index
            // b - end loop index
            // c - increment if known, else 0
            var arrayLoop = false;
            if (exprs.Children.Count == 3)
            {
                // a,b,c form, var from a to b by c
                EmitExpression(exprs.Children[0] as ExpressionAst);
                EmitExpression(exprs.Children[1] as ExpressionAst);
                EmitExpression(exprs.Children[2] as ExpressionAst);
            }
            else if (exprs.Children.Count == 2)
            {
                // a,b form, var from a to b by c, where c is determined below
                EmitExpression(exprs.Children[0] as ExpressionAst);
                EmitExpression(exprs.Children[1] as ExpressionAst);
                EmitI(Opcode.Push, 0);
            }
            else
            {
                // array form, or error 
                // todo - array form
                throw new InternalFailure("For loop on array not done");
                arrayLoop = true;
                EmitI(Opcode.Push, 0); // array start
                EmitI(Opcode.Push, 0); // array end - 1 TODO
                EmitI(Opcode.Push, 1); // increment
            }


            // compute for loop start into this spot
            var forLoopVaribleAddress = GetLoadAddress(node.VariableSymbol);
            var forLoopVariableName = node.VariableSymbol.Name;

            Emit2(Opcode.ForStart, OperandType.None, forLoopVariableName, forLoopVaribleAddress);

            var startLabel = "for_" + GetLabel();
            var continueLabel = "for_" + GetLabel();
            var endLabel = "for_" + GetLabel();

            loopContinueLabels.Push(continueLabel);
            loopBreakLabels.Push(endLabel);

            EmitS(Opcode.Label, startLabel);
            EmitBlock(node.Children[1] as BlockAst);

            EmitS(Opcode.Label, continueLabel);

            // if more to do, go to top
            if (arrayLoop)
                throw new InternalFailure("Loop not implemented");
            else // evaluate end expression again
                EmitExpression(exprs.Children[1] as ExpressionAst);

            Emit2(Opcode.ForLoop, OperandType.None, forLoopVariableName, forLoopVaribleAddress, startLabel);
                // update increment, loop if more

            // end of for loop
            EmitS(Opcode.Label, endLabel);

            // pop labels
            loopContinueLabels.Pop();
            loopBreakLabels.Pop();

        }

        void EmitWhileStatement(WhileStatementAst node)
        {
            var startLabel = "while_" + GetLabel();
            var endLabel = "while_" + GetLabel();

            loopContinueLabels.Push(startLabel);
            loopBreakLabels.Push(endLabel);

            EmitS(Opcode.Label, startLabel);
            EmitExpression(node.Children[0] as ExpressionAst);
            EmitS(Opcode.BrFalse, endLabel);
            EmitBlock(node.Children[1] as BlockAst);
            EmitS(Opcode.BrAlways, startLabel);
            EmitS(Opcode.Label, endLabel);

            loopContinueLabels.Pop();
            loopBreakLabels.Pop();
        }

        void EmitIfStatement(IfStatementAst node)
        {
            if (node == null)
                throw new InternalFailure("Node must be an if statement");
            var finalLabel = "endif_" + GetLabel();
            for (var i = 0; i < node.Children.Count - 1; i += 2)
            {
                var label = "if_" + GetLabel();
                EmitExpression(node.Children[i] as ExpressionAst);
                EmitS(Opcode.BrFalse, label); // branch if false to next case
                EmitBlock(node.Children[i + 1] as BlockAst);
                EmitS(Opcode.BrAlways, finalLabel); // done, leave if statement
                EmitS(Opcode.Label, label); // label next block
            }
            if ((node.Children.Count & 1) == 1)
            {
                // final else
                EmitBlock(node.Children.Last() as BlockAst);
            }
            EmitS(Opcode.Label, finalLabel); // end of if
        }

        int labelCount = 0;

        string GetLabel()
        {
            ++labelCount;
            return $"label_{labelCount}";
        }

        // make arrays for the current symbol table
        void MakeArrays()
        {
            foreach (var entry in mgr.SymbolTable.Entries)
            {
                var type = entry.Type as ArrayType;
                if (type != null && entry.VariableUse != VariableUse.Param && type.ArrayDimension>0)
                {
                    // make this array - do not make them for param
                    var n = type.ArrayDimension;// # of dimensions

                    var operands = new List<object>();
                    operands.Add(GetLoadAddress(entry)); // address of array to create
                    operands.Add(n);

                    operands.Add(entry.StackSize);

#if true // testing
//                    var size = 1; // assume basic type
//                    if (!String.IsNullOrEmpty(type.UserTypeName))
//                        size = mgr.Lookup(type.UserTypeName).StackSize;
//                    var s = size;
//                    for (var i = 0; i < type.ArrayDimension; ++i)
//                        s = entry.ArrayDimensions[n - i - 1]*s + Runtime.ArrayHeaderSize;
//                    if (s != entry.StackSize)
//                        throw new InternalFailure($"Mismatched sizes {s} {entry.StackSize}");
#endif
                    foreach (var d in entry.ArrayDimensions)
                        operands.Add(d);

                    // create it
                    var use = entry.VariableUse == VariableUse.Global?OperandType.Global : OperandType.Local;
                    Emit2(Opcode.MakeArr, use, entry.Name, operands.ToArray());
                }
            }
        }

        void EmitBlock(BlockAst node)
        {
            if (node == null)
                throw new InternalFailure("Node must be a block");
            Recurse(node);
        }

        // walks an expression structure, yields data about sequential items
        // each item is something that can be assigned to
        class ItemStructureWalker :IEnumerable<ItemStructureWalker.ItemData>
        {
            ExpressionAst ast;
            public ItemStructureWalker(ExpressionAst ast)
            {
                this.ast = ast;
            }

            public IEnumerator<ItemData> GetEnumerator()
            {
                //todo - implement this
                yield return new ItemData
                {
                    OperandType = GetOperandType(GetSymbolType(ast)),
                    Skip = 0,
                    SymbolName = ast.Symbol?.Name
                };
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            internal class ItemData
            {
                public OperandType OperandType = OperandType.None;
                public int Skip = 0;
                public bool More => Skip > 0;
                public string SymbolName;
            }
        }

        void AssignHelper(int stackSlots, List<Ast> items, List<Ast> exprs, TokenType assignType)
        {
            if (stackSlots < 1)
                throw new InternalFailure($"Assign requires positive stack slots, got {stackSlots}");
            // todo.... some items put multiple on stack....

            // push expr on stack in order. This expands complex types
            foreach (var expr in exprs)
                EmitExpression(expr as ExpressionAst);

            if (stackSlots > 1)
                EmitI(Opcode.Reverse,stackSlots);

            // store in variables in order
            var simpleItemsRead = 0;
            foreach (var item in items)
            {
                EmitExpression(item as ExpressionAst, true); // address of expression

                foreach (var itemData in new ItemStructureWalker(item as ExpressionAst))
                {
                    var operandType = itemData.OperandType;

                    // stack has (push order) new value, then addr on top
                    var depth = 0; // extra added for tuple
                    if (itemData.More)
                    { 
                        // EmitO(Opcode.Dup); // save address - more of them needed
                        throw new InternalFailure("Needs special write that leaves address....");
                        depth = 1;
                    }

                    // todo - reversed - no pick

                    // for +=, etc. read var, perform, 
                    // todo - make much shorter, table driven
                    if (assignType != TokenType.Equals)
                    {
                        EmitO(Opcode.Dup); // address already already there 
                        // read it (note address already global here)
                        Emit2(Opcode.Read, OperandType.Global, itemData.SymbolName);
                        // stack now new val,addr,old val
                        // get value from back on stack
                        EmitO(Opcode.Rot3);
                    }

                    // stack now (in push order): 
                    // Equals : addr, newValue
                    // Else   : addr, oldValue, newValue

                    switch (assignType)
                    {
                        case TokenType.Equals:
                            break;
                        case TokenType.AddEq:
                            EmitT(Opcode.Add, operandType);
                            break;
                        case TokenType.SubEq:
                            EmitT(Opcode.Sub, operandType);
                            break;
                        case TokenType.MulEq:
                            EmitT(Opcode.Mul, operandType);
                            break;
                        case TokenType.DivEq:
                            EmitT(Opcode.Div, operandType);
                            break;
                        case TokenType.XorEq:
                            EmitO(Opcode.Xor);
                            break;
                        case TokenType.AndEq:
                            EmitO(Opcode.And);
                            break;
                        case TokenType.OrEq:
                            EmitO(Opcode.Or);
                            break;
                        case TokenType.ModEq:
                            EmitT(Opcode.Mod, operandType);
                            break;
                        case TokenType.RightShiftEq:
                            EmitT(Opcode.RightShift, operandType);
                            break;
                        case TokenType.LeftShiftEq:
                            EmitT(Opcode.LeftShift, operandType);
                            break;
                        case TokenType.RightRotateEq:
                            EmitT(Opcode.RightRotate, operandType);
                            break;
                        case TokenType.LeftRotateEq:
                            EmitT(Opcode.LeftRotate, operandType);
                            break;
                        default:
                            throw new InternalFailure($"Unknown operation {assignType} in AssignHelper");
                    }

                    if (assignType != TokenType.Equals)
                        EmitO(Opcode.Swap);

                    // stack now (push order) newValue, addr
                    WriteValue(operandType);
                    simpleItemsRead++;

                    if (itemData.More)
                    {
                        // next address
                        EmitI(Opcode.Push, itemData.Skip);
                        EmitT(Opcode.Add, OperandType.Int32);
                        throw new InternalFailure("Needs special write that leaves address....");
                    }

                }
            }
        }

#region Read/Write variable values and addresses

        // push value, then address, then writes value into address
        void WriteValue(OperandType operandType)
        {
            EmitT(Opcode.Write, operandType);
        }

        // Put the value of the variable in the symbol on the stack
        void LoadValue(SymbolEntry symbol)
        {
            if (symbol.VariableUse == VariableUse.Global)
                Emit2(Opcode.Load, OperandType.Global, symbol.Name, symbol.ReferenceAddress);
            else if (symbol.VariableUse == VariableUse.Local || symbol.VariableUse == VariableUse.ForLoop)
                Emit2(Opcode.Load, OperandType.Local, symbol.Name, symbol.ReferenceAddress + callStackSuffixSize);
            else if (symbol.VariableUse == VariableUse.Param)
                Emit2(Opcode.Load, OperandType.Local, symbol.Name, symbol.ReferenceAddress - callStackPrefixSize);
            else if (symbol.VariableUse == VariableUse.Member)
                Emit2(Opcode.Load, OperandType.Local, symbol.Name, symbol.ReferenceAddress);
            else
                throw new InternalFailure($"Unsupported address type {symbol}");
        }

        int GetLoadAddress(SymbolEntry symbol)
        {
            if (symbol.VariableUse == VariableUse.Global)
                return symbol.ReferenceAddress;
            else if (symbol.VariableUse == VariableUse.Local)
                return symbol.ReferenceAddress + callStackSuffixSize;
            else if (symbol.VariableUse == VariableUse.Param)
                return symbol.LayoutAddress.Value - callStackPrefixSize;
            else if (symbol.VariableUse == VariableUse.ForLoop)
                return symbol.ReferenceAddress + callStackSuffixSize;
            else if (symbol.VariableUse == VariableUse.Member)
                return symbol.ReferenceAddress;
            else
                throw new InternalFailure($"Unsupported address type {symbol}");

        }

        bool PassByRef(InternalType type)
        {
            return !(type is SimpleType);
        }

        // given a symbol, load it's address
        void LoadAddress(SymbolEntry symbol)
        {
            var addr = GetLoadAddress(symbol);

            if (symbol.VariableUse == VariableUse.Global)
                Emit2(Opcode.Addr, OperandType.Global, symbol.Name, addr);
            else if (symbol.VariableUse == VariableUse.Local)
                Emit2(Opcode.Addr, OperandType.Local, symbol.Name, addr);
            else if (symbol.VariableUse == VariableUse.Param)
            {
                if (PassByRef(symbol.Type))
                {
                    // address passed in param, copy it
                    Emit2(Opcode.Push, OperandType.Int32, symbol.Name, addr);
                    Emit2(Opcode.Read, OperandType.Local, symbol.Name);
                }
                else
                { // item value on param stack, get its address
                    Emit2(Opcode.Addr, OperandType.Local, symbol.Name, addr);
                }
            }
            else if (symbol.VariableUse == VariableUse.Member)
                Emit2(Opcode.Addr, OperandType.Local, symbol.Name, addr);
            else
                throw new InternalFailure($"Unsupported address type {symbol}");
        }

//        OperandType GetOperandType(SymbolEntry symbol)
//        {
//            if (symbol == null)
//                throw new InternalFailure("Null symbol");
//            var t = symbol.Type;
//            if (t.ArrayDimension > 0)
//                throw new InternalFailure("Cannot put array type in simple operand");
//            return GetOperandType(t.SymbolType);
//        }

        public static OperandType GetOperandType(SymbolType type)
        {
            // emit known value, ignore children
            switch (type)
            {
                case SymbolType.Bool:
                case SymbolType.Byte:
                    return OperandType.Int32; // todo - would like byte packing someday
                case SymbolType.Int32:
                    return OperandType.Int32;
                case SymbolType.Float32:
                    return OperandType.Float32;
                default:
                    throw new InternalFailure($"Unsupported type in GetOperandType {type}");
            }
        }

#endregion

        void EmitAssignStatement(AssignStatementAst node)
        {
            if (node.Token.TokenType == TokenType.Increment || node.Token.TokenType == TokenType.Decrement)
            {
                var child = node.Children[0].Children[0] as ExpressionAst;
                EmitExpression(child, true); // address of expression
                // todo - can add inc/dec instructions
                EmitO(Opcode.Dup);
                Emit2(Opcode.Read, OperandType.Global, child.Name); // convert address to value
                if (node.Token.TokenType == TokenType.Increment)
                    EmitI(Opcode.Push, 1);
                else
                    EmitI(Opcode.Push, -1);
                EmitO(Opcode.Add);
                EmitO(Opcode.Swap);
                WriteValue(OperandType.Int32);
            }
            else
            {
                var items = node.Children[0].Children;
                var expr = node.Children[1].Children;
                AssignHelper(node.StackCount, items, expr, node.Token.TokenType);
            }
        }

        void EmitVariableDef(VariableDefinitionAst node)
        {
            if (node.Children.Count == 2)
            {
                // process assignments
                var items = node.Children[0].Children;
                var expr = node.Children[1].Children;
                AssignHelper(node.StackCount,items, expr, TokenType.Equals);
            }
        }

        void Emit2(Opcode opcode, OperandType type, string comment, params object[] operands)
        {
            var inst = new Instruction(opcode, type, comment, operands);
            instructions.Add(inst);
        }

        // emit opcode with string
        void EmitS(Opcode opcode, string label)
        {
            var inst = new Instruction(opcode, OperandType.None, "", label);
            instructions.Add(inst);
        }

        // emit opcode with integer
        void EmitI(Opcode opcode, int value)
        {
            var inst = new Instruction(opcode, OperandType.Int32, "", value);
            instructions.Add(inst);
        }

        // emit opcode only
        void EmitO(Opcode opcode)
        {
            var inst = new Instruction(opcode, OperandType.Int32, "");
            instructions.Add(inst);
        }

        // emit opcode and type
        void EmitT(Opcode opcode, OperandType type)
        {
            var inst = new Instruction(opcode, type, "");
            instructions.Add(inst);
        }

#region Function/Return

        /* Functions and call stacks
        * 
        * Variables passed to and from function in call/return order, i32/byte/float/bool on stack, else address
        * 
        * 
        *    -----------
        *    | param 1 |  \
        *    -----------   \
        *    |   ...   |    | done by caller before call
        *    -----------   /  
        *    | param N |  /
        *    -----------   
        * 
        *    -----------  
        *    | ret addr|  \
        *    -----------   | done by call instruction, cleaned by ret instruction
        *    | base ptr|  /
        *    -----------    <--- base pointer points here = sp at the time
        * 
        *    -----------      
        *    | local 1 |  \ 
        *    -----------   \
        *    |   ...   |    | local variable space created by callee, cleaned by ret instruction
        *    -----------   /
        *    | local M |  /
        *    -----------     <----- stack points here on entry, and is stack position where callee does work
        *    
        *    -----------
        *    | return 1 |   \
        *    -----------     \
        *    |    ...   |    | return items pushed by callee right before executing ret instruction
        *    -----------     /  
        *    | return n |   /
        *    -----------      <----- stack points here right before ret instuction
        *    
        *    
        *    On return, the ret instruction cleans the stack, then pushes all returned values (fully expanded) onto the stack
        *    
        *    Caller then either consumes or cleans stack
        *    
        *    To do return a,b,c, push values on stack, call return (which handles copies and cleaning)
        *    
        *    return instruction: takes N = # parameters then M = # of stack entries for locals
        *    Executes:
        *       n = # return entries        = cur stack - (base pointer + M)
        *       s = source stack entry      = cur stack - n = base pointer + M
        *       d = dest source stack entry = base pointer - 2 - N
        *       sp <- bp
        *       bp = pop stack
        *       r  = pop stack
        *       pop N entries
        *       copy n return entries from s to d (not modifying stack pointer value)
        *       sp = right after last of n entries copied = d + n
        *       return to address r. 
        * 
        */

        // used to compute spacing later for variable access
        // symbol table addresses are based on base pointer pointing to 
        // address 0, where first local is stored, and negative is parameters
        // these are byte counts of things stored on stack before base pointer and after base pointer
        static int callStackSuffixSize = 0, callStackPrefixSize = 2;

        void EmitFunctionCall(FunctionCallAst node)
        {
            // call stack: parameters, then call

            // return space

            var symbol = node.Symbol;
            var type = symbol.Type as FunctionType;
            if (type == null) throw new InternalFailure($"Required function type, got {symbol.Type}");
            if (type.CallStackReturnSize < 0)
                throw new InternalFailure($"Function return stack size not set {node}");

            //            var retSize = type.ReturnType.Tuple.Count;
            //            if (retSize < 0)
            //                throw new InternalFailure($"Return size < 0 {symbol}");
            //            else if (retSize > 0)
            //                Emit2(Opcode.ClearStack, OperandType.None, "function return value space", retSize);

            // basic types passed by value, others by address
            foreach (var child in node.Children)
            {
                var addrOnly = PassByRef(child.Type);
                EmitExpression((ExpressionAst) child,addrOnly);
            }

            if (symbol.Attrib.HasFlag(SymbolAttribute.Import))
                Emit2(Opcode.Call, OperandType.Const, "", ImportName(node.Name));
            else
                Emit2(Opcode.Call, OperandType.Local, "", node.Name);

            // if result ignored, pop them
            if (!FunctionResultIsUsed(node))
                Emit2(Opcode.PopStack, OperandType.None, "clean unused return values", type.CallStackReturnSize);
        }

        public static string ImportPrefix = "<import>";
        string ImportName(string nodeName)
        {
            return ImportPrefix + nodeName;
        }

        // return true if the function return values need to removed from the stack
        bool FunctionResultIsUsed(FunctionCallAst node)
        {
            var p = node.Parent;
            if (p is BlockAst)
                return false;
            if (p is ExpressionAst || p is ExpressionListAst || p is IfStatementAst)
                return true;
            throw new InternalFailure($"Function return use unknown via parent {p}");
        }

        void EmitFunction(FunctionDeclarationAst node)
        {
            Emit2(Opcode.Symbol, OperandType.None, "", node.Symbol);
            if (node.ImportToken != null)
                return; 
            Emit2(Opcode.Label, OperandType.None, node.Symbol.Type.ToString(), node.Name);

            // reserve stack space
            EmitI(Opcode.ClearStack, node.SymbolTable.StackEntries);

            var block = node.Children[2] as BlockAst;
            EmitBlock(block);
            if (block.Children.Last().Token.TokenType != TokenType.Return)
                EmitReturn(node); // last was not a return. Needs one
        }

        // emit a return. 
        // If node is null, was added at function end, needs no parameters
        void EmitReturn(Ast node)
        {
            if (node != null)
            {
                // parameters on stack
                var exprs = node.Children[0].Children;
                foreach (var expr in exprs)
                    EmitExpression(expr as ExpressionAst);
            }
            // find function declaration
            Ast decl = node;
            while (decl != null && !(decl is FunctionDeclarationAst))
                decl = decl.Parent;

            var func = decl as FunctionDeclarationAst;
            var funcType = func.Symbol.Type as FunctionType;
            if (funcType == null) throw new InternalFailure($"Required function type, got {func.Symbol.Type}");

            Emit2(Opcode.Return, OperandType.None, "",
                funcType.ParamsType.Tuple.Count,
                func.SymbolTable.StackEntries
            );
        }

#endregion


#region Expression

        // process expression, leaves a value on stack, unless address asked for
        void EmitExpression(ExpressionAst node, bool leaveAddressOnly = false)
        {
            if (node == null)
                throw new InternalFailure("Expression node cannot be null");
            if (node.HasValue)
            {
                if (leaveAddressOnly)
                    throw new InternalFailure("EmitExpression cannot emit address for const");
                // emit known value, ignore children
                if (node.Type is SimpleType)
                {
                    switch ((node.Type as SimpleType).SymbolType)
                    {
                        case SymbolType.Bool:
                            EmitI(Opcode.Push, node.BoolValue.Value ? 1 : 0);
                            break;
                        case SymbolType.Int32:
                            EmitI(Opcode.Push, node.IntValue.Value);
                            break;
                        case SymbolType.Float32:
                            Emit2(Opcode.Push, OperandType.Float32, "", node.FloatValue.Value);
                            break;
                        case SymbolType.Byte:
                            EmitI(Opcode.Push, node.ByteValue.Value);
                            break;
                        default:
                            throw new InternalFailure($"Unsupported type in expression {node}");
                    }
                }
                else
                {
                    throw new InternalFailure($"Unsupported type in expression {node}");
                }
            }
            else
            {
                if (node is FunctionCallAst)
                    EmitFunctionCall(node as FunctionCallAst);
                else if (node is ArrayAst || node is DotAst)
                {
                    EmitItemAddressOrValue(node, leaveAddressOnly);
                }
                else if (node.Children.Count == 2)
                {
                    EmitExpression((ExpressionAst) node.Children[0]); // get left value
                    EmitExpression((ExpressionAst) node.Children[1]); // get right value
                    EmitBinaryOp(node); // do operation
                }
                else if (node.Children.Count == 1)
                {
                    EmitExpression((ExpressionAst) node.Children[0]); // get value
                    EmitUnaryOp(node); // do operation
                }
                else if (node.Children.Count == 0)
                {
                    if ((node is IdentifierAst) || (node is TypedItemAst))
                    {
                        if (leaveAddressOnly)
                            LoadAddress(node.Symbol);
                        else
                            LoadValue(node.Symbol);
                    }
                    else
                        throw new InternalFailure($"ExpressionAst not emitted {node}");

                }
                else
                    throw new InternalFailure($"Expression must have 0 to 2 children! {node}");
            }
        }

#region Item Address

        // Structure:
        // 1. DotAst must have one child of type ArrayAst or TypedItemAst. 
        // 2. ArrayAst must have 2 children, first is index (LiteralAst or ExpressionAst), second TypedItemAst or ArrayAst or DotAst
        // 3. TypedItemAst has no children
        // 
        // How to evaluate: (goal, compute address of item)
        // 1. DotAst has field name and type of field, DotAst.child has enclosing type
        // 2. TypedItemAst has a Name, a Type, and a Symbol (for addresses)
        // 
        // todo - optimization: for all that are const, pack the value into offset

        OperandType EmitDotAddress(DotAst node, OperandType operandType)
        {
            if (node.Children.Count != 1)
                throw new InternalFailure($"Malformed dot ast {node}");

            // address of item of which to take '.'
            var child = node.Children[0] as ExpressionAst;
            if (child is TypedItemAst)
            {
                LoadAddress(child.Symbol);
                operandType = GetLocality(child.Symbol);
            }
            else if (child is ArrayAst)
                EmitArrayAddress(child as ArrayAst, operandType);
            else
                throw new InternalFailure($"Malformed dot ast {node}");

            // type from which to take '.' to get offset
            var type = child.Type as UserType;
            if (type == null) throw new InternalFailure($"Required user type, got {child.Type}");
            var nameOfOffset = node.Name;
            var offsetOfType = mgr.GetTypeOffset(type.Name, nameOfOffset);
            if (offsetOfType != 0)
            {
                // compute offset
                Emit2(Opcode.Push, OperandType.Int32, nameOfOffset, offsetOfType);
                EmitO(Opcode.Add);
            }
            return operandType;
        }

        OperandType GetLocality(SymbolEntry symbol)
        {
            var use = symbol.VariableUse;
            if (use == VariableUse.Local ||
                use == VariableUse.ForLoop || 
                use == VariableUse.Param
                )
                return OperandType.Local;
            if (use == VariableUse.Global)
                return OperandType.Global;
            throw new InternalFailure($"Unsupported variable use {use} in GetLocality");
        }

        OperandType EmitArrayAddress(ArrayAst node, OperandType operandType)
        {
            // walk down array dimensions, putting index values on stack
            var k = 0; // number of items
            // todo - check first level structure
            Ast current = node;
            while (current.Children.Count == 2 && current is ArrayAst)
            {
                if (current.Children.Count != 2 || !(current.Children[0] is ExpressionAst) || !(current.Children[1] is ExpressionAst))
                    throw new InternalFailure($"Malformed ArrayAst {node}");

                ++k;

                // offset into array
                var offsetNode = current.Children[0] as ExpressionAst;
                EmitExpression(offsetNode);

                current = current.Children[1]; // next possible array level
            }

            // array address
            int n = -1; // total array dimension
            if (current is TypedItemAst)
            {
                var ti = (TypedItemAst) current;
                LoadAddress(ti.Symbol);
                operandType = GetLocality(ti.Symbol);
                n = ti.Symbol.ArrayDimensions.Count;
            }
            else if (current is DotAst)
            {
                operandType = EmitDotAddress(current as DotAst, operandType);
                throw new NotImplementedException("DotAst needs array size n");
            }
            else
                throw new InternalFailure($"Array needs typed item {current.Children[1]}");

            // emit code to turn all this into an address on stack
            Emit2(Opcode.Array, OperandType.None, "", k, n);
            return operandType;
        }

        // emit expression address. Node is an array or a '.'
        // These items are expressions of form a[1].b[3].c.d[2][1].e
        void EmitItemAddressOrValue(ExpressionAst node, bool leaveAddressOnly)
        {
            OperandType opType = OperandType.None;
            if (node is DotAst)
                opType = EmitDotAddress((DotAst) node, opType);
            else if (node is ArrayAst)
                opType = EmitArrayAddress((ArrayAst) node, opType);
            else
                throw new InternalFailure($"Node must be DotAst or ArrayAst {node}");

            //if(opType != OperandType.Local && opType != OperandType.Global && opType != OperandType.Const)
            //    throw new InternalFailure($"Illegal OperandType {opType} in {node}");

            // todo - eval items if param, global, local, const?
            // if want value instead of simply address, now get the value from the address
            if (!leaveAddressOnly)
            {
                // note that at this point, the address on stack is already global 
                Emit2(Opcode.Read, OperandType.Global, node.Name); // convert address to value
//                Emit2(Opcode.Read, opType, node.Name); // convert address to value
            }
        }

#endregion

        // given value on stack, and unary operation, evaluate the operation
        void EmitUnaryOp(ExpressionAst node)
        {
            switch (node.Token.TokenType)
            {
                case TokenType.Exclamation:
                {  // bool toggle 
                    EmitI(Opcode.Push,1);
                    EmitO(Opcode.Xor);
                }
                    break;
                case TokenType.Tilde:
                    EmitO(Opcode.Not); // i32 bitflip
                    break;
                case TokenType.Plus:
                    // do nothing
                    break;
                case TokenType.Minus:
                    var s = GetSymbolType(node);
                    if (s == SymbolType.Byte || s == SymbolType.Int32)
                        EmitO(Opcode.Neg);
                    else if (s == SymbolType.Float32)
                        EmitT(Opcode.Neg, OperandType.Float32);
                    else
                        throw new InternalFailure($"Unknown negation emitted {node}");
                    break;
                default:
                    throw new InternalFailure($"Unknown unary op emitted {node}");
            }
        }

        class BinOp
        {
            public TokenType TokenType;
            public SymbolType [] SymbolTypes;
            public Opcode Opcode;

            public BinOp(TokenType tokType, Opcode opcode, params SymbolType [] matchingSymbols)
            {
                TokenType = tokType;
                SymbolTypes = matchingSymbols;
                Opcode = opcode;
            }

        }

        static BinOp [] binTbl =
        {
            // !=
            new BinOp(TokenType.NotEqual,Opcode.NotEqual,SymbolType.Bool,SymbolType.Byte,SymbolType.Int32, SymbolType.Float32),

            // ==
            new BinOp(TokenType.Compare,Opcode.IsEqual,SymbolType.Bool,SymbolType.Byte,SymbolType.Int32, SymbolType.Float32),

            // >
            new BinOp(TokenType.GreaterThan,Opcode.GreaterThan,SymbolType.Byte,SymbolType.Int32, SymbolType.Float32),

            // >=
            new BinOp(TokenType.GreaterThanOrEqual,Opcode.GreaterThanOrEqual,SymbolType.Byte,SymbolType.Int32, SymbolType.Float32),

            // <=
            new BinOp(TokenType.LessThanOrEqual,Opcode.LessThanOrEqual,SymbolType.Byte,SymbolType.Int32, SymbolType.Float32),

            // <
            new BinOp(TokenType.LessThan,Opcode.LessThan,SymbolType.Byte,SymbolType.Int32, SymbolType.Float32),

            // ||, &&
            new BinOp(TokenType.LogicalOr,Opcode.Or,SymbolType.Bool),
            new BinOp(TokenType.LogicalAnd,Opcode.And,SymbolType.Bool),

            // >>, <<, >>>, <<<, &, |, ^, %
            new BinOp(TokenType.RightShift,Opcode.RightShift,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.LeftShift,Opcode.LeftShift,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.RightRotate,Opcode.RightRotate,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.LeftRotate,Opcode.LeftRotate,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Ampersand,Opcode.And,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Pipe,Opcode.Or,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Caret,Opcode.Xor,SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Percent,Opcode.Mod,SymbolType.Byte, SymbolType.Int32),

            // +,-,*,/
            new BinOp(TokenType.Plus,Opcode.Add,SymbolType.Byte, SymbolType.Int32, SymbolType.Float32),
            new BinOp(TokenType.Minus,Opcode.Sub,SymbolType.Byte, SymbolType.Int32, SymbolType.Float32),
            new BinOp(TokenType.Asterix,Opcode.Mul,SymbolType.Byte, SymbolType.Int32, SymbolType.Float32),
            new BinOp(TokenType.Slash,Opcode.Div,SymbolType.Byte, SymbolType.Int32, SymbolType.Float32),
        };

        // get symbol type, node must have SimpleType, else throw
        public static SymbolType GetSymbolType(Ast node)
        {
            var type = node.Type as SimpleType;
            if (type != null)
                return type.SymbolType;
            throw new InternalFailure($"Required simple type {node.Type}");
        }

        // given two values on stack, emit the binary operation on them
        // leaves value on stack
        void EmitBinaryOp(ExpressionAst node)
        {
            foreach (var entry in binTbl)
            {
                var s = GetSymbolType(node.Children[0]);
                if (s != GetSymbolType(node.Children[1]))
                    throw new InternalFailure("Operand on different types not implemented");
                if (node.Token.TokenType == entry.TokenType &&
                    entry.SymbolTypes.Contains(s))
                {
                    EmitT(entry.Opcode, GetOperandType(s));
                    return;
                }
            }
            throw new InternalFailure($"Cannot emit binary op {node}");
        }

#endregion Expression

#region Memory Layout
        void LayoutMemory(Ast node)
        {
            // size each symbol
            mgr.ComputeSizes();
            if (env.ErrorCount > 0)
                return;

            // size all blocks
            SizeBlocks(mgr.SymbolTable);

            // final stack locations for locals
            foreach (var child in mgr.RootTable.Children.Where(t=>t.IsFunctionBlock))
                PlaceLocals(child, 0);
        }

        // place local variables on stack relative to parent
        void PlaceLocals(SymbolTable table, int shift)
        {
            var total = 0;
            foreach (var entry in table.Entries.Where(t=>t.VariableUse == VariableUse.Local || t.VariableUse == VariableUse.ForLoop))
            {
                if (entry.StackSize < 0)
                    throw new InternalFailure("Stack size not set in Place Locals");
                total += entry.StackSize;
                entry.LayoutAddress += shift;
            }
            foreach (var child in table.Children)
                PlaceLocals(child,total+shift);
        }

        // compute
        void SizeBlocks(SymbolTable tbl)
        {
            // do children first
            foreach (var ch in tbl.Children)
                SizeBlocks(ch);

            // now layout this one
            var size = 0;
            var paramSize = 0;
            foreach (var e in tbl.Entries.Where(e => e.StackSize > 0 && e.VariableUse != VariableUse.None))
            {
                if (e.VariableUse == VariableUse.Param)
                {
                    e.LayoutAddress = paramSize;
                    paramSize += 1; // every parameter takes one stack slot
//                    if (e.Type.PassByRef)
//                        paramSize += 4;
//                    else
//                        paramSize += e.Type.ByteSize.Value;
                }
                else if (e.VariableUse == VariableUse.ForLoop)
                { // todo - rethink this....
                    e.LayoutAddress = size; // stores for loop info here
                    size += ForLoopStackSize; 
                }
                else
                {
                    e.LayoutAddress = size; // stores here
                    size += e.StackSize;
                }
            }
            
            // invert parameter sizes
            foreach (var e in tbl.Entries.Where(e => e.ByteSize > 0))
            {
                if (e.VariableUse == VariableUse.Param)
                    e.LayoutAddress = -(paramSize - e.LayoutAddress.Value);
            }
 
            // max of child block sizes:
            var maxChildSize = tbl.Children.Any() ? tbl.Children.Max(ch => ch.StackEntries) : 0;
            tbl.StackEntries = size + maxChildSize;
            // tbl.ParamsSize = paramSize;
        }

#endregion

    }
}
