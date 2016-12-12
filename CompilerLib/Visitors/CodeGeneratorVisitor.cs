using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

            // generate code
            symbolTable.Start();
            Recurse(ast);

            return instructions;
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
                EmitVariableDef((VariableDefinitionAst) node);
                return;
            }
            else if (node is FunctionDeclarationAst)
            {
                EmitFunction((FunctionDeclarationAst) node);
                return;
            }
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
        const int ForLoopStackSize = 2; // number of for loop stack entries (index,delta)

        void EmitForStatement(ForStatementAst node)
        {
            // for statement: index var is on stack, then limit, then (if array) array address
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
                // a,b form, var from a to b by c, where c is determined here
                EmitExpression(exprs.Children[0] as ExpressionAst);
                EmitExpression(exprs.Children[1] as ExpressionAst);
                EmitI(Opcode.Push,0);
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
            var forLoopVaribleAddress = node.VariableSymbol.Address.Value+callStackSuffixSize;
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
            else
                EmitExpression(exprs.Children[1] as ExpressionAst);

            Emit2(Opcode.ForLoop, OperandType.None, forLoopVariableName, forLoopVaribleAddress, startLabel); // update increment, loop if more

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
                throw new InternalFailure($"Node must be a if statement {node}");
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


        void EmitBlock(BlockAst node)
        {
            if (node == null)
                throw new InternalFailure($"Node must be a block {node}");
            Recurse(node);
        }

        void AssignHelper(List<Ast> items, List<Ast> expr, TokenType assignType)
        {
            // push expr on stack in order
            for (var i = 0; i < items.Count; ++i)
                EmitExpression(expr[i] as ExpressionAst);

            // store in variables in reverse order
            for (var i = 0; i < items.Count; ++i)
            {
                var item = items[items.Count - i - 1];
                var symbol = (item as TypedItemAst).Symbol;
                var operandType = GetOperandType(symbol);

                // for +=, etc. read var, perform, 
                // todo - make much shorter, table driven
                switch (assignType)
                {
                    case TokenType.AddEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Add);
                        break;
                    case TokenType.SubEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.Sub,operandType);
                        break;
                    case TokenType.MulEq:
                        ReadValue(symbol);
                        EmitT(Opcode.Mul, operandType);
                        break;
                    case TokenType.DivEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.Div, operandType);
                        break;
                    case TokenType.XorEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Xor);
                        break;
                    case TokenType.AndEq:
                        ReadValue(symbol);
                        EmitO(Opcode.And);
                        break;
                    case TokenType.OrEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Or);
                        break;
                    case TokenType.ModEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.Mod, operandType);
                        break;
                    case TokenType.RightShiftEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.RightShift, operandType);
                        break;
                    case TokenType.LeftShiftEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.LeftShift, operandType);
                        break;
                    case TokenType.RightRotateEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.RightRotate, operandType);
                        break;
                    case TokenType.LeftRotateEq:
                        ReadValue(symbol);
                        EmitO(Opcode.Swap);
                        EmitT(Opcode.LeftRotate, operandType);
                        break;
                }
                WriteValue(symbol);
            }
        }

        #region Read/Write variable values and addresses

        // write value on stack top into given symbol
        void WriteValue(SymbolEntry symbol)
        {
            LoadAddress(symbol);
            var opType = GetOperandType(symbol);
            EmitT(Opcode.Store, opType);
        }

        // Put the value of the variable in the symbol on the stack
        void ReadValue(SymbolEntry symbol)
        {
            if (symbol.Type.PassByRef)
            {
                LoadAddress(symbol);
                return;
            }

            if (symbol.VariableUse == VariableUse.Global)
                Emit2(Opcode.Load, OperandType.Global, symbol.Name, symbol.Address.Value);
            else if (symbol.VariableUse == VariableUse.Local || symbol.VariableUse == VariableUse.ForLoop)
                Emit2(Opcode.Load, OperandType.Local, symbol.Name, symbol.Address.Value + callStackSuffixSize);
            else if (symbol.VariableUse == VariableUse.Param)
                Emit2(Opcode.Load, OperandType.Local, symbol.Name, symbol.Address.Value - callStackPrefixSize);
            else
                throw new InternalFailure($"Unsupported address type {symbol}");
        }

        // given a symbol, load it's address
        void LoadAddress(SymbolEntry symbol)
        {
            if (symbol.VariableUse == VariableUse.Global)
                Emit2(Opcode.Addr, OperandType.Global, symbol.Name, symbol.Address.Value);
            else if (symbol.VariableUse == VariableUse.Local)
                // todo - this wrong - fix
                Emit2(Opcode.Addr, OperandType.Local, symbol.Name,  symbol.Address.Value + callStackSuffixSize);
            else if (symbol.VariableUse == VariableUse.Param)
                // todo - this wrong - fix
                Emit2(Opcode.Addr, OperandType.Local, symbol.Name,  symbol.Address.Value - callStackPrefixSize);
            else
                throw new InternalFailure($"Unsupported address type {symbol}");
        }

        OperandType GetOperandType(SymbolEntry symbol)
        {
            if (symbol == null)
                throw new InternalFailure("Null symbol");
            var t = symbol.Type;
            if (t.ArrayDimensions.Any())
                throw new InternalFailure("Cannot put array type in simple operand");
            return GetOperandType(t.SymbolType);
        }

        OperandType GetOperandType(SymbolType type)
        {
            // emit known value, ignore children
            switch (type)
            {
                case SymbolType.Bool:
                case SymbolType.Byte:
                    return OperandType.Byte;
                case SymbolType.Int32:
                    return OperandType.Int32;
                case SymbolType.Float32:
                    return OperandType.Int32;
                default:
                    throw new InternalFailure($"Unsupported type in GetOperandType {type}");
            }
        }

        #endregion

        void EmitAssignStatement(AssignStatementAst node)
        {
            var items = node.Children[0].Children;
            var expr = node.Children[1].Children;
            AssignHelper(items, expr, node.Token.TokenType);
        }

        void EmitVariableDef(VariableDefinitionAst node)
        {
            if (node.Children.Count == 2)
            { // process assignments
                var items = node.Children[0].Children;
                var expr  = node.Children[1].Children;
                AssignHelper(items, expr, TokenType.Equals);
            }
        }

        void Emit2(Opcode opcode, OperandType type, string comment, params object [] operands)
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
         *    |ret val 1|  \
         *    -----------   \
         *    |   ...   |    |  space made by caller for return values/addresses
         *    -----------   /
         *    |ret val n|  / 
         *    -----------    
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
         *    -----------   | done by call instruction, cleaned by ret function
         *    | base ptr|  /
         *    -----------    <--- base pointer points here = sp at the time
         * 
         *    -----------      
         *    | local 1 |  \ 
         *    -----------   \
         *    |   ...   |    | space saved by callee
         *    -----------   /
         *    | local M |  /
         *    -----------     <----- stack points here
         *    
         *    
         *    Caller then either consumes or cleans stack
         *    
         *    To do return a,b,c, push values on stack, call return (which handles copies and cleaning)
         *    
         *    return instruction: takes N = # parameters then M = # of stack entries for locals
         *    Executes:
         *       n = # return entries        = cur stack - (base pointer + M)
         *       s = source stack entry      = cur stack - n
         *       d = dest source stack entry = base pointer - 2 - N - n
         *       copy n stack entries to return variable locations
         *       sp <- bp
         *       bp = pop stack
         *       r  = pop stack
         *       pop N entries
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
            // call stack: return item space, parameters, then call

            // return space
            var symbol = mgr.Lookup(node.Name);
            var retSize = symbol.Type.ReturnType.Count;
            if (retSize < 0)
                throw new InternalFailure($"Return size < 0 {symbol}");
            else if (retSize > 0)
                Emit2(Opcode.AddStack, OperandType.None, "function return value space", retSize);

            // basic types passed by value, others by address
            foreach (var child in node.Children)
                EmitExpression((ExpressionAst)child);

            EmitS(Opcode.Call, node.Name);
        }

        void EmitFunction(FunctionDeclarationAst node)
        {
            Emit2(Opcode.Symbol, OperandType.None, "", node.Symbol);
            Emit2(Opcode.Label, OperandType.None, node.Symbol.Type.ToString(), node.Name);

            // reserve stack space
            EmitI(Opcode.AddStack, node.SymbolTable.StackEntries);

            var block = node.Children[2] as BlockAst;
            EmitBlock(block);
            if (block.Children.Last().Token.TokenType != TokenType.Return)
                EmitReturn(null); // last was not a return. Needs one
        }

        // emit a return. 
        // If node is null, was added at function end, needs no parameters
        void EmitReturn(JumpStatementAst node)
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
            while (!(decl is FunctionDeclarationAst))
                decl = decl.Parent;

            var func = decl as FunctionDeclarationAst;

            Emit2(Opcode.Return, OperandType.None, "",
                func.Symbol.Type.ParamsType.Count,
                func.SymbolTable.StackEntries
                );
        }
        #endregion


        #region Expression

        // process expression
        void EmitExpression(ExpressionAst node)
        {
            if (node == null)
                throw new InternalFailure("Expression node cannot be null");
            if (node.HasValue)
            {
                // emit known value, ignore children
                switch (node.Type.SymbolType)
                {
                    case SymbolType.Bool:
                        EmitI(Opcode.Push, node.BoolValue.Value ? 1 : 0);
                        break;
                    case SymbolType.Int32:
                        EmitI(Opcode.Push, node.IntValue.Value);
                        break;
                    case SymbolType.Float32:
                        Emit2(Opcode.Push, OperandType.Float32,"", node.FloatValue.Value);
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
                if (node is FunctionCallAst)
                    EmitFunctionCall(node as FunctionCallAst);
                else if (node is ArrayAst)
                    throw new InternalFailure("Array not yet implemented");
                else if (node is DotAst)
                    EmitDot(node as DotAst);
                else if (node.Children.Count == 2)
                {
                    EmitExpression((ExpressionAst) node.Children[0]); // do left
                    EmitExpression((ExpressionAst) node.Children[1]); // do right
                    EmitBinaryOp((ExpressionAst) node); // do operation
                }
                else if (node.Children.Count == 1)
                {
                    EmitExpression((ExpressionAst) node.Children[0]); // do child
                    EmitUnaryOp((ExpressionAst) node); // do operation
                }
                else if (node.Children.Count == 0)
                {
                    if (node is IdentifierAst)
                        ReadValue((node as IdentifierAst).Symbol);
                    else
                        throw new InternalFailure($"ExpressionAst not emitted {node}");

                }
                else
                    throw new InternalFailure($"Expression must have 0 to 2 children! {node}");
            }
        }

        void EmitDot(DotAst node)
        {
            var d = node as DotAst;
            if (d.Children.Count != 1 || !(d.Children[0] is ExpressionAst))
                throw new InternalFailure("Malformed dot ast {node}");

            // address of item of which to take '.'
            var c = d.Children[0] as ExpressionAst;
            EmitExpression(c);

            // type from which to take '.'
            var type = c.Type;
            var nameOfOffset = c.Name;
            var offsetOfType = mgr.GetTypeOffset(type.UserTypeName, nameOfOffset);

            env.Error("EmitDot unfinished");

            //todo
        }

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
                    var s = node.Type.SymbolType;
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


        void EmitBinaryOp(ExpressionAst node)
        {
            foreach (var entry in binTbl)
            {
                var s = node.Children[0].Type.SymbolType;
                if (s != node.Children[1].Type.SymbolType)
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
            // size each symbol type
            mgr.ComputeSizes(env);

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
                if (entry.Type.StackSize < 0)
                    throw new InternalFailure("Stack size not set in Place Locals");
                total += entry.Type.StackSize;
                entry.Address += shift;
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
            foreach (var e in tbl.Entries.Where(e => e.Type.StackSize > 0))
            {
                if (e.VariableUse == VariableUse.Param)
                {
                    e.Address = paramSize;
                    paramSize += 1; // every parameter takes one stack slot
//                    if (e.Type.PassByRef)
//                        paramSize += 4;
//                    else
//                        paramSize += e.Type.ByteSize.Value;
                }
                else if (e.VariableUse == VariableUse.ForLoop)
                { // todo - rethink this....
                    e.Address = size; // stores for loop info here
                    size += ForLoopStackSize; 
                }
                else
                {
                    e.Address = size; // stores here
                    size += e.Type.StackSize;
                }
            }
            
            // invert parameter sizes
            foreach (var e in tbl.Entries.Where(e => e.Type.ByteSize > 0))
            {
                if (e.VariableUse == VariableUse.Param)
                    e.Address = -(paramSize - e.Address.Value);
            }
 
            // max of child block sizes:
            var maxChildSize = tbl.Children.Any() ? tbl.Children.Max(ch => ch.StackEntries) : 0;
            tbl.StackEntries = size + maxChildSize;
            // tbl.ParamsSize = paramSize;
        }

        #endregion

    }
}
