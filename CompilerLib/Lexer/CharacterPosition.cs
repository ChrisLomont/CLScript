using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib.Lexer
{
    public class CharacterPosition
    {
        public CharacterPosition(CharacterPosition position = null)
        {
            LineNumber = 1;
            LinePosition = 1;
            if (position != null)
            {
                LineNumber = position.LineNumber;
                LinePosition = position.LinePosition;
                TextIndex = position.TextIndex;
            }
        }
        public int LineNumber { get; private set; }
        public int LinePosition { get; private set; }
        public int TextIndex { get; private set; }

        public void Advance(char currentChar)
        {
            TextIndex++;
            LinePosition++;

            if (currentChar == '\n')
            {
                LinePosition = 1;
                LineNumber++;
            }

        }

        public override string ToString()
        {
            return $"[{LineNumber}:{LinePosition}]";
        }
    }

}
