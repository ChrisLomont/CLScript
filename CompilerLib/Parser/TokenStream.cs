﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lomont.ClScript.CompilerLib.AST;

namespace Lomont.ClScript.CompilerLib.Parser
{
    internal class Memo
    {
        public Ast Ast { get; set; }
        public int NextIndex { get; set; }
    }

    public class ParseableTokenStream : TokenizableStreamBase<Token>
    {
        public ParseableTokenStream(Lexer.Lexer lexer) : base(() => lexer.Lex().ToList())
        {
        }

        readonly Dictionary<int, Memo> cachedAst = new Dictionary<int, Memo>();

        public bool IsMatch(TokenType type)
        {
            if (Current.TokenType == type)
            {
                return true;
            }

            return false;
        }

        public Ast Capture(Func<Ast> ast)
        {
            if (Alt(ast))
            {
                return Get(ast);
            }

            return null;
        }

        /// <summary>
        /// Retrieves a cached version if it was found during any alternate route
        /// otherwise executes it
        /// </summary>
        /// <param name="getter"></param>
        /// <returns></returns>
        public Ast Get(Func<Ast> getter)
        {
            Memo memo;
            if (!cachedAst.TryGetValue(Index, out memo))
            {
                return getter();
            }

            Index = memo.NextIndex;

            //Console.WriteLine("Returning type {0} from index {1} as memo", memo.Ast, Index);
            return memo.Ast;
        }

        public Token Take(TokenType type)
        {
            if (IsMatch(type))
            {
                var current = Current;

                Consume();

                return current;
            }

            throw new InvalidSyntax($"Invalid Syntax. Expecting {type} but got {Current.TokenType}");
        }




        public Boolean Alt(Func<Ast> action)
        {
            TakeSnapshot();

            Boolean found = false;

            try
            {
                var currentIndex = Index;

                var ast = action();

                if (ast != null)
                {
                    found = true;

                    cachedAst[currentIndex] = new Memo
                    {
                        Ast = ast,
                        NextIndex = Index
                    };
                }
            }
            catch
            {

            }

            RollbackSnapshot();

            return found;
        }

        public override Token Peek(int lookahead)
        {
            var peeker = base.Peek(lookahead);

            if (peeker == null)
            {
                return new Token(TokenType.EndOfFile);
            }

            return peeker;
        }

        public override Token Current
        {
            get
            {
                var current = base.Current;
                if (current == null)
                {
                    return new Token(TokenType.EndOfFile);
                }
                return current;
            }
        }

        public List<Token> GetTokens()
        {
            var items = new List<Token>();
            items.AddRange(Items);
            return items;
        }
    }
}
