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
                    EmitI(Emit.BrAlways(loopContinueLabels.Peek()));
                    break;
                case TokenType.Break:
                    EmitI(Emit.BrAlways(loopBreakLabels.Peek()));
                    break;
                case TokenType.Return:
                    EmitReturn(node);
                    break;
                default:
                    throw new InternalFailure($"Emit does not know return type {node}");
            }
        }


        // for loop stores a counter and a delta
        const int ForLoopStackSize = 8; // for loop stack entries

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
                EmitI(Emit.Push(0));
            }
            else
            {
                // array form, or error 
                // todo - array form
                throw new InternalFailure("For loop on array not done");
                arrayLoop = true;
                EmitI(Emit.Push(0)); // array start
                EmitI(Emit.Push(0)); // array end - 1 TODO
                EmitI(Emit.Push(1)); // increment
            }


            // compute for loop start into this spot
            var forLoopVaribleAddress = node.VariableSymbol.Address.Value;
            var forLoopVariableName = node.VariableSymbol.Name;

            EmitI(Emit.ForStart(forLoopVaribleAddress, forLoopVariableName));

            var startLabel = "for_" + GetLabel();
            var continueLabel = "for_" + GetLabel();
            var endLabel = "for_" + GetLabel();

            loopContinueLabels.Push(continueLabel);
            loopBreakLabels.Push(endLabel);

            EmitI(Emit.Label(startLabel));
            EmitBlock(node.Children[1] as BlockAst);

            EmitI(Emit.Label(continueLabel));

            // if more to do, go to top
            if (arrayLoop)
                throw new InternalFailure("Loop not implemented");
            else
                EmitExpression(exprs.Children[1] as ExpressionAst);

            EmitI(Emit.ForLoop(forLoopVariableName, startLabel)); // update increment, loop if more

            // end of for loop
            EmitI(Emit.Label(endLabel));

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

            EmitI(Emit.Label(startLabel));
            EmitExpression(node.Children[0] as ExpressionAst);
            EmitI(Emit.BrFalse(endLabel));
            EmitBlock(node.Children[1] as BlockAst);
            EmitI(Emit.BrAlways(startLabel));
            EmitI(Emit.Label(endLabel));

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
                EmitI(Emit.BrFalse(label)); // branch if false to next case
                EmitBlock(node.Children[i + 1] as BlockAst);
                EmitI(Emit.BrAlways(finalLabel)); // done, leave if statement
                EmitI(Emit.Label(label)); // label next block
            }
            if ((node.Children.Count & 1) == 1)
            {
                // final else
                EmitBlock(node.Children.Last() as BlockAst);
            }
            EmitI(Emit.Label(finalLabel)); // end of if
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
                        EmitI(Emit.Add(operandType));
                        break;
                    case TokenType.SubEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.Sub(operandType));
                        break;
                    case TokenType.MulEq:
                        ReadValue(symbol);
                        EmitI(Emit.Mul(operandType));
                        break;
                    case TokenType.DivEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.Div(operandType));
                        break;
                    case TokenType.XorEq:
                        ReadValue(symbol);
                        EmitI(Emit.Xor());
                        break;
                    case TokenType.AndEq:
                        ReadValue(symbol);
                        EmitI(Emit.And());
                        break;
                    case TokenType.OrEq:
                        ReadValue(symbol);
                        EmitI(Emit.Or());
                        break;
                    case TokenType.ModEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.Mod(operandType));
                        break;
                    case TokenType.RightShiftEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.RightShift(operandType));
                        break;
                    case TokenType.LeftShiftEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.LeftShift(operandType));
                        break;
                    case TokenType.RightRotateEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.RightRotate(operandType));
                        break;
                    case TokenType.LeftRotateEq:
                        ReadValue(symbol);
                        EmitI(Emit.Swap());
                        EmitI(Emit.LeftRotate(operandType));
                        break;
                }

                WriteValue(symbol);
            }
        }

        #region Read/Write variable values and addresses

        // write value on stack top into given symbol
        void WriteValue(SymbolEntry symbol)
        {
            var operandType = GetOperandType(symbol);
            LoadAddress(symbol);
            EmitI(Emit.Store(operandType));
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
                EmitI(Emit.LoadGlobal(symbol.Address.Value, symbol.Name));
            else if (symbol.VariableUse == VariableUse.Local || symbol.VariableUse == VariableUse.ForLoop)
                EmitI(Emit.LoadLocal(symbol.Address.Value + callStackSuffixSize, symbol.Name));
            else if (symbol.VariableUse == VariableUse.Param)
                EmitI(Emit.LoadLocal(symbol.Address.Value - callStackPrefixSize, symbol.Name));
            else
                throw new InternalFailure($"Unsupported address type {symbol}");
        }

        // given a symbol, load it's address
        void LoadAddress(SymbolEntry symbol)
        {
            if (symbol.VariableUse == VariableUse.Global)
                EmitI(Emit.Push(symbol.Address, OperandType.Int32, symbol.Name));
            else if (symbol.VariableUse == VariableUse.Local)
                // todo - this wrong - fix
                EmitI(Emit.LocalAddress(symbol.Address.Value, symbol.Name));
            else if (symbol.VariableUse == VariableUse.Param)
                // todo - this wrong - fix
                EmitI(Emit.LocalAddress(-symbol.Address.Value, symbol.Name));
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
            // emit known value, ignore children
            switch (t.SymbolType)
            {
                case SymbolType.Bool:
                case SymbolType.Byte:
                    return OperandType.Byte;
                case SymbolType.Int32:
                    return OperandType.Int32;
                case SymbolType.Float32:
                    return OperandType.Int32;
                default:
                    throw new InternalFailure($"Unsupported type in symbol {symbol}");
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

        void EmitI(Instruction instruction)
        {
            instructions.Add(instruction);
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
         *    -----------    |
         *    |ret val n|   / 
         *    -----------  /  
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
         *    | local n |  /
         *    -----------     <----- stack points here
         *    
         *    
         *    Caller then either consumes or cleans stack
         *    
         *    To do return a,b,c, push values on stack, call return (which handles copies and cleaning)
         *    
         *    return instruction: takes # M of bytes for params 1-N
         *    Executes:
         *       s = ret size = cur stack - top of frame
         *       copy s bytes to return varible locations
         *       r = ret addr
         *       sp <- bp
         *       restore bp
         *       pop M from stack
         *       return to address r. 
         * 
         */

        // used to compute spacing later for variable access
        // symbol table addresses are based on base pointer pointing to 
        // address 0, where first local is stored, and negative is parameters
        // these are byte counts of things stored on stack before base pointer and after base pointer
        static int callStackSuffixSize = 0, callStackPrefixSize = 8;

        void EmitFunctionCall(FunctionCallAst node)
        {
            // call stack: return item space, parameters, then call

            // return space
            var symbol = mgr.Lookup(node.Token.TokenValue);
            var retSize = symbol.Type.ReturnSize;
            if (retSize < 0)
                throw new InternalFailure($"Return size < 0 {symbol}");
            else if (retSize > 0)
                EmitI(Emit.AddStack(retSize,"function return value space"));

            // basic types passed by value, others by address
            foreach (var child in node.Children)
                EmitExpression((ExpressionAst)child);

            EmitI(Emit.Call(node.Token.TokenValue));
        }

        void EmitFunction(FunctionDeclarationAst node)
        {
            EmitI(Emit.Label(node.Name,node.Symbol.Type.ToString()));

            // reserve stack space
            EmitI(Emit.AddStack(node.SymbolTable.StackSize,""));

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
            Ast func = node;
            while (!(func is FunctionDeclarationAst))
                func = func.Parent;

            EmitI(Emit.Return((func as FunctionDeclarationAst).SymbolTable.ParamsSize));
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
                        EmitI(Emit.Push(node.BoolValue.Value ? 1 : 0));
                        break;
                    case SymbolType.Int32:
                        EmitI(Emit.Push(node.IntValue.Value));
                        break;
                    case SymbolType.Float32:
                        EmitI(Emit.Push(node.FloatValue.Value, OperandType.Float32));
                        break;
                    case SymbolType.Byte:
                        EmitI(Emit.Push(node.ByteValue.Value, OperandType.Int32));
                        break;
                    default:
                        throw new InternalFailure($"Unsupported type in expression {node}");

                }
            }
            else
            {
                if (node is FunctionCallAst)
                    EmitFunctionCall(node as FunctionCallAst);
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

        void EmitUnaryOp(ExpressionAst node)
        {
            switch (node.Token.TokenType)
            {
                case TokenType.Exclamation:
                {  // bool toggle
                    EmitI(Emit.Push(1));
                    EmitI(Emit.Xor());
                }
                    break;
                case TokenType.Tilde:
                    EmitI(Emit.Not()); // i32 bitflip
                    break;
                case TokenType.Plus:
                    // do nothing
                    break;
                case TokenType.Minus:
                    var s = node.Type.SymbolType;
                    if (s == SymbolType.Byte || s == SymbolType.Int32)
                        EmitI(Emit.Neg());
                    else if (s == SymbolType.Float32)
                        EmitI(Emit.Neg(OperandType.Float32));
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
            public SymbolType [] SymbolType;
            public Instruction Instruction;

            public BinOp(TokenType t, Instruction op, params SymbolType [] s)
            {
                TokenType = t;
                SymbolType = s;
                Instruction = op;
            }

        }

        static BinOp [] binTbl =
        {
            // !=
            new BinOp(TokenType.NotEqual,Emit.NotEqual(),SymbolType.Bool,SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.NotEqual,Emit.NotEqual(OperandType.Float32),SymbolType.Float32),

            // ==
            new BinOp(TokenType.Compare,Emit.IsEqual(),SymbolType.Bool,SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.Compare,Emit.IsEqual(OperandType.Float32),SymbolType.Float32),

            // >
            new BinOp(TokenType.GreaterThan,Emit.GreaterThan(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.GreaterThan,Emit.GreaterThan(OperandType.Float32),SymbolType.Float32),

            // >=
            new BinOp(TokenType.GreaterThanOrEqual,Emit.GreaterThanOrEqual(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.GreaterThanOrEqual,Emit.GreaterThanOrEqual(OperandType.Float32),SymbolType.Float32),

            // <=
            new BinOp(TokenType.LessThanOrEqual,Emit.LessThanOrEqual(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.LessThanOrEqual,Emit.LessThanOrEqual(OperandType.Float32),SymbolType.Float32),

            // <
            new BinOp(TokenType.LessThan,Emit.LessThan(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.LessThan,Emit.LessThan(OperandType.Float32),SymbolType.Float32),

            // ||, &&
            new BinOp(TokenType.LogicalOr,Emit.Or(),SymbolType.Bool),
            new BinOp(TokenType.LogicalAnd,Emit.And(),SymbolType.Bool),

            // >>, <<, >>>, <<<, &, |, ^, %
            new BinOp(TokenType.RightShift,Emit.RightShift(OperandType.Byte),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.LeftShift,Emit.LeftShift(OperandType.Byte),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.RightRotate,Emit.RightRotate(OperandType.Byte),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.LeftRotate,Emit.LeftRotate(OperandType.Byte),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Ampersand,Emit.And(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Pipe,Emit.Or(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Caret,Emit.Xor(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Percent,Emit.Mod(),SymbolType.Byte, SymbolType.Int32),

            // +,-,*,/
            new BinOp(TokenType.Plus,Emit.Add(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Minus,Emit.Sub(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Asterix,Emit.Mul(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Slash,Emit.Div(),SymbolType.Byte, SymbolType.Int32),

            new BinOp(TokenType.Plus,Emit.Add(OperandType.Float32),SymbolType.Float32),
            new BinOp(TokenType.Minus,Emit.Sub(OperandType.Float32),SymbolType.Float32),
            new BinOp(TokenType.Asterix,Emit.Mul(OperandType.Float32),SymbolType.Float32),
            new BinOp(TokenType.Slash,Emit.Div(OperandType.Float32),SymbolType.Float32)

        };


        void EmitBinaryOp(ExpressionAst node)
        {
            foreach (var entry in binTbl)
            {
                var s = node.Children[0].Type.SymbolType;
                if (s != node.Children[1].Type.SymbolType)
                    throw new InternalFailure("Operand on different types not implemented");
                if (node.Token.TokenType == entry.TokenType &&
                    entry.SymbolType.Contains(s))
                {
                    EmitI(entry.Instruction);
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
                total += entry.Type.Size.Value;
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
            foreach (var e in tbl.Entries.Where(e => e.Type.Size.HasValue))
            {
                if (e.VariableUse == VariableUse.Param)
                {
                    e.Address = paramSize;
                    if (e.Type.PassByRef)
                        paramSize += 4;
                    else
                        paramSize += e.Type.Size.Value;
                }
                else if (e.VariableUse == VariableUse.ForLoop)
                { // todo - rethink this....
                    e.Address = size; // stores for loop info here
                    size += ForLoopStackSize; 
                }
                else
                {
                    e.Address = size; // stores here
                    size += e.Type.Size.Value;
                }
            }
            
            // invert parameter sizes
            foreach (var e in tbl.Entries.Where(e => e.Type.Size.HasValue))
            {
                if (e.VariableUse == VariableUse.Param)
                    e.Address = -(paramSize - e.Address.Value);
            }
 
            // max of child block sizes:
            var maxChildSize = tbl.Children.Any() ? tbl.Children.Max(ch => ch.StackSize) : 0;
            tbl.StackSize = size + maxChildSize;
            tbl.ParamsSize = paramSize;
        }

        #endregion

    }
}
