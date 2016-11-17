using System;
using System.Collections.Generic;
using System.Diagnostics;

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

        public CharacterPosition  position = new CharacterPosition();

        public bool End => IsEndOfFile(0);
        public char Current => End ? EndOfFile : Text[position.TextIndex];

        public void Consume(int count = 1)
        {
            for (var i = 0; i < count; ++i)
                position.Advance(Text[position.TextIndex]);
        }

        bool IsEndOfFile(int lookahead)
        {
            return position.TextIndex + lookahead >= Text.Length;
        }

        public override string ToString()
        {
            return $"Line {position.LineNumber} Position {position.LinePosition}";
        }


        public char Peek(int lookahead)
        {
            return IsEndOfFile(lookahead) ? EndOfFile : Text[position.TextIndex + lookahead];
        }

        public void TakeSnapshot()
        {
            snapshotIndexes.Push(new CharacterPosition(position));
        }

        public void RollbackSnapshot()
        {
            position = snapshotIndexes.Pop();
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
