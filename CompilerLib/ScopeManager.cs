using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib
{
    class ScopeManager
    {
        public string Scope => stack.Peek();

        Stack<string> stack = new Stack<string>();

        public ScopeManager()
        {
            stack.Push("<global>");
        }

        int blockIndex = 0;
        string GetBlockName()
        {
            ++blockIndex;
            return "Block_" + blockIndex;
        }

        public void Enter(Ast node)
        {
            if (node is EnumAst)
                Push(((EnumAst) node).Name);
            else if (node is ModuleAst)
                Push(((ModuleAst) node).Name);
            else if (node is TypeDeclarationAst)
                Push(((TypeDeclarationAst) node).Name);
            else if (node is FunctionDeclarationAst)
                Push(((FunctionDeclarationAst) node).Name);
            else if (node is BlockAst)
                Push(GetBlockName());
        }
        public void Exit(Ast node)
        {
            if (node is EnumAst)
                Pop();
            else if (node is ModuleAst)
                Pop();
            else if (node is TypeDeclarationAst)
                Pop();
            else if (node is FunctionDeclarationAst)
                Pop();
            else if (node is BlockAst)
                Pop();
        }

        void Push(string name)
        {
            var top = stack.Peek();
            stack.Push(top+"." + name);
        }


        void Pop()
        {
            stack.Pop();
        }
    }
}
