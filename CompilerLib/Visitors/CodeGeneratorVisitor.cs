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
        List<Opcode> instructions = new List<Opcode>();

        Stack<string> loopBreakLabels = new Stack<string>();
        Stack<string> loopContinueLabels = new Stack<string>();

        public void Generate(SymbolTableManager symbolTable, Ast ast, Environment environment)
        {
            env = environment;
            mgr = symbolTable;
            symbolTable.Start();
            Recurse(ast);

            Dump(instructions, env.Output);
        }

        void Dump(List<Opcode> instructions, TextWriter output)
        {
            foreach (var inst in instructions)
                output.WriteLine(inst);
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
                EmitAssignStatement((AssignStatementAst)node);
                return;
            }
            else if (node is VariableDefinitionAst)
            {
                EmitVariableDef((VariableDefinitionAst) node);
                return;
            }
            else if (node is FunctionDeclarationAst)
            {
                EmitFunction((FunctionDeclarationAst)node);
                return;
            }
            else if (node is IfStatementAst)
            {
                EmitIfStatement((IfStatementAst)node);
                return;
            }
            else if (node is WhileStatementAst)
            {
                EmitWhileStatement((WhileStatementAst)node);
                return;
            }
            else if (node is ForStatementAst)
            {
                EmitForStatement((ForStatementAst)node);
                return;
            }
            else if (node is JumpStatementAst)
            {
                EmitJumpStatement((JumpStatementAst)node);
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
                    Emit(Opcode.BrAlways(loopContinueLabels.Peek()));
                    break;
                case TokenType.Break:
                    Emit(Opcode.BrAlways(loopBreakLabels.Peek()));
                    break;
                case TokenType.Return: 
                    EmitReturn(node);
                    break;
                default:
                    throw new InternalFailure($"Emit does not know return type {node}");
            }
        }

        // emit a return. 
        // If node is null, was added at function end, needs no parameters
        void EmitReturn(JumpStatementAst node)
        {
            if (node != null)
            {
                // needs to clean for stack if in loops
                var count = 0;
                Ast p = node;
                while (!(p is FunctionDeclarationAst))
                {
                    if (p is ForStatementAst)
                        count += ForLoopStackSize;
                    p = p.Parent;
                }
                if (count > 0)
                    Emit(Opcode.Pop(count));

                // todo - parameters
                var exprs = node.Children[0].Children;
                foreach (var expr in exprs)
                    EmitExpression(expr as ExpressionAst);
            }
            Emit(Opcode.Return());
        }

        const int ForLoopStackSize = 3; // stack entries
        void EmitForStatement(ForStatementAst node)
        {
            // for statement: index var is on stack, then limit, then (if array) array address
            var exprs = node.Children[0];

            // put three integers on stack: 
            // a - current loop index
            // b - end index
            // c - increment

            if (exprs.Children.Count == 3)
            { // a,b,c form, var from a to b by c
                EmitExpression(exprs.Children[0] as ExpressionAst);
                EmitExpression(exprs.Children[1] as ExpressionAst);
                EmitExpression(exprs.Children[2] as ExpressionAst);
            }
            else if (exprs.Children.Count == 2)
            { // a,b form, var from a to b by c, where c is determined here
                EmitExpression(exprs.Children[0] as ExpressionAst);
                EmitExpression(exprs.Children[1] as ExpressionAst);
                Emit(Opcode.ForStart()); // computes +1 or -1 increment
            }
            else 
            { // array form, or error 
                // todo - array form
                throw new InternalFailure("For loop on array not done");
            }

            var startLabel    = "for_" + GetLabel();
            var continueLabel = "for_" + GetLabel();
            var endLabel      = "for_" + GetLabel();
            loopContinueLabels.Push(continueLabel);
            loopBreakLabels.Push(endLabel);

            Emit(Opcode.Label(startLabel));
            EmitBlock(node.Children[1] as BlockAst);

            Emit(Opcode.Label(continueLabel));
            // if more to do, go to top
            Emit(Opcode.ForLoop(startLabel)); // update increment, loop if more

            // end of for loop
            Emit(Opcode.Label(endLabel));
            
            // clean a,b,c off stack
            Emit(Opcode.Pop(ForLoopStackSize)); // clean 'for' frame

            loopContinueLabels.Pop();
            loopBreakLabels.Pop();

        }

        void EmitWhileStatement(WhileStatementAst node)
        {
            var startLabel = "while_" + GetLabel();
            var endLabel = "while_" + GetLabel();

            loopContinueLabels.Push(startLabel);
            loopBreakLabels.Push(endLabel);

            Emit(Opcode.Label(startLabel));
            EmitExpression(node.Children[0] as ExpressionAst);
            Emit(Opcode.BrFalse(endLabel));
            EmitBlock(node.Children[1] as BlockAst);
            Emit(Opcode.BrAlways(startLabel));
            Emit(Opcode.Label(endLabel));

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
                Emit(Opcode.BrFalse(label)); // branch if false to next case
                EmitBlock(node.Children[i+1] as BlockAst);
                Emit(Opcode.BrAlways(finalLabel)); // done, leave if statement
                Emit(Opcode.Label(label)); // label next block
            }
            if ((node.Children.Count & 1) == 1)
            { // final else
                EmitBlock(node.Children.Last() as BlockAst);
            }
            Emit(Opcode.Label(finalLabel)); // end of if
        }

        int labelCount = 0;
        string GetLabel()
        {
            ++labelCount;
            return $"label_{labelCount}";
        }

        void EmitFunction(FunctionDeclarationAst node)
        {
            Emit(Opcode.Label(node.Name));
            var block = node.Children[2] as BlockAst;
            EmitBlock(block);
            if (block.Children.Last().Token.TokenType != TokenType.Return)
                EmitReturn(null); // last was not a return. Needs one
        }

        void EmitBlock(BlockAst node)
        {
            if (node == null)
                throw new InternalFailure($"Node must be a block {node}");
            Recurse(node);
        }

        private void AssignHelper(List<Ast> items, List<Ast> expr)
        {
            // push expr on stack in order
            for (var i = 0; i < items.Count; ++i)
                EmitExpression(expr[i] as ExpressionAst);

            // store in variables in reverse order
            for (var i = 0; i < items.Count; ++i)
            {
                // todo - need ops like +=, -=, etc.
                var name = items[items.Count - i - 1].Token.TokenValue;
                Emit(Opcode.LoadAddress(name));
                Emit(Opcode.Store());
            }

        }

        void EmitAssignStatement(AssignStatementAst node)
        {
            var items = node.Children[0].Children;
            var expr = node.Children[1].Children;
            AssignHelper(items, expr);
        }

        void EmitVariableDef(VariableDefinitionAst node)
        {
            if (node.Children.Count == 2)
            { // process assignments
                var items = node.Children[0].Children;
                var expr  = node.Children[1].Children;
                AssignHelper(items, expr);
            }
        }

        void Emit(Opcode opcode)
        {
            instructions.Add(opcode);
        }

        #region Process

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
                        Emit(Opcode.Push32(node.BoolValue.Value ? 1 : 0));
                        break;
                    case SymbolType.Int32:
                        Emit(Opcode.Push32(node.IntValue.Value));
                        break;
                    case SymbolType.Float32:
                        Emit(Opcode.PushF(node.FloatValue.Value));
                        break;
                    case SymbolType.Byte:
                        Emit(Opcode.Push32(node.ByteValue.Value));
                        break;
                    default:
                        throw new InternalFailure($"Unsupported type in expression {node}");

                }
            }
            else
            {
                if (node is FunctionCallAst)
                {
                    foreach (var child in node.Children)
                        EmitExpression((ExpressionAst) child); // do parameters
                    Emit(Opcode.Call(node.Token.TokenValue));
                }
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
                    {
                        var name = ((IdentifierAst) node).Name;
                        Emit(Opcode.Load(name));
                    }
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
                    Emit(Opcode.Push32(1));
                    Emit(Opcode.Xor());
                }
                    break;
                case TokenType.Tilde:
                    Emit(Opcode.Not()); // i32 bitflip
                    break;
                case TokenType.Plus:
                    // do nothing
                    break;
                case TokenType.Minus:
                    var s = node.Type.SymbolType;
                    if (s == SymbolType.Byte || s == SymbolType.Int32)
                        Emit(Opcode.Neg());
                    else if (s == SymbolType.Float32)
                        Emit(Opcode.NegF());
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
            public Opcode Opcode;

            public BinOp(TokenType t, Opcode op, params SymbolType [] s)
            {
                TokenType = t;
                SymbolType = s;
                Opcode = op;
            }

        }

        static BinOp [] binTbl =
        {
            // !=
            new BinOp(TokenType.NotEqual,Opcode.NotEqual(),SymbolType.Bool,SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.NotEqual,Opcode.NotEqualF(),SymbolType.Float32),

            // ==
            new BinOp(TokenType.Compare,Opcode.Compare(),SymbolType.Bool,SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.Compare,Opcode.CompareF(),SymbolType.Float32),

            // >
            new BinOp(TokenType.GreaterThan,Opcode.GreaterThan(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.GreaterThan,Opcode.GreaterThanF(),SymbolType.Float32),

            // >=
            new BinOp(TokenType.GreaterThanOrEqual,Opcode.GreaterThanOrEqual(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.GreaterThanOrEqual,Opcode.GreaterThanOrEqualF(),SymbolType.Float32),

            // <=
            new BinOp(TokenType.LessThanOrEqual,Opcode.LessThanOrEqual(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.LessThanOrEqual,Opcode.LessThanOrEqualF(),SymbolType.Float32),

            // <
            new BinOp(TokenType.LessThan,Opcode.LessThan(),SymbolType.Byte,SymbolType.Int32),
            new BinOp(TokenType.LessThan,Opcode.LessThanF(),SymbolType.Float32),

            // ||, &&
            new BinOp(TokenType.LogicalOr,Opcode.Or(),SymbolType.Bool),
            new BinOp(TokenType.LogicalAnd,Opcode.And(),SymbolType.Bool),

            // >>, <<, >>>, <<<, &, |, ^, %
            new BinOp(TokenType.RightShift,Opcode.RightShift(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.LeftShift,Opcode.LeftShift(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.RightRotate,Opcode.RightRotate(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.LeftRotate,Opcode.LeftRotate(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Ampersand,Opcode.And(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Pipe,Opcode.Or(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Caret,Opcode.Xor(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Percent,Opcode.Mod(),SymbolType.Byte, SymbolType.Int32),

            // +,-,*,/
            new BinOp(TokenType.Plus,Opcode.Add(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Minus,Opcode.Sub(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Asterix,Opcode.Mul(),SymbolType.Byte, SymbolType.Int32),
            new BinOp(TokenType.Slash,Opcode.Div(),SymbolType.Byte, SymbolType.Int32),

            new BinOp(TokenType.Plus,Opcode.AddF(),SymbolType.Float32),
            new BinOp(TokenType.Minus,Opcode.SubF(),SymbolType.Float32),
            new BinOp(TokenType.Asterix,Opcode.MulF(),SymbolType.Float32),
            new BinOp(TokenType.Slash,Opcode.DivF(),SymbolType.Float32)

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
                Emit(entry.Opcode);
                return;
            }
        }
        throw new InternalFailure($"Cannot emit binary op {node}");
    }

    #endregion Process
}
}
