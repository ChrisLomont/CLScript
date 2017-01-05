using System;
using System.Collections.Generic;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    // character stream capable of backtracking
    public class CharacterStream
    {
        // when end of file reached, this is returned
        public static char EndOfFile = Char.MaxValue;

        public CharacterStream(string source)
        {
            Text = source;
        }

        readonly Stack<CharacterPosition> snapshotIndexes = new Stack<CharacterPosition>();

        public string Text { get; }

        public CharacterPosition  Position = new CharacterPosition();

        public bool End => IsEndOfFile(0);
        public char Current => End ? EndOfFile : Text[Position.TextIndex];

        public void Consume(int count = 1)
        {
            for (var i = 0; i < count; ++i)
                Position.Advance(Text[Position.TextIndex]);
        }

        bool IsEndOfFile(int lookahead)
        {
            return Position.TextIndex + lookahead >= Text.Length;
        }

        public override string ToString()
        {
            return $"Line {Position.LineNumber} Position {Position.LinePosition}";
        }


        public char Peek(int lookahead)
        {
            return IsEndOfFile(lookahead) ? EndOfFile : Text[Position.TextIndex + lookahead];
        }

        public void TakeSnapshot()
        {
            snapshotIndexes.Push(new CharacterPosition(Position));
        }

        public void RollbackSnapshot()
        {
            Position = snapshotIndexes.Pop();
        }

        public void CommitSnapshot()
        {
            snapshotIndexes.Pop();
        }

        public bool StartsWith(string search)
        {
            for (var i = 0; i < search.Length; ++i)
                if (Peek(i) != search[i])
                    return false;
            return true;
        }

    }
}
