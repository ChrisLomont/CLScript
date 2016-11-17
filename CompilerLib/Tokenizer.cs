using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace Lomont.ClScript.CompilerLib
{
    public class TokenizerOld
    {
#if false
        // the text being tokenized
        string text;

        class SourcePosition
        {
            public int lineNumber = 0;
            public int linePosition = 0;
            public string filename = "";
        }

        SourcePosition position = new SourcePosition();

#region Lexers

#endregion

        // next line to parse into tokens
        int nextLineIndex = 0;
        
        // index of last token handed out
        int lastTokenIndex = -1;

        // tokenized file
        List<Token> tokenList = new List<Token>();
        
        // stack to store indices of tokens for backing up
        Stack<int> tokenIndexStack = new Stack<int>();

        // create a characterStream for the lines of code
        public TokenizerOld(string text)
        {
            this.text = text;
            nextLineIndex = 0;
            lastTokenIndex = -1;
            indentLevel.Push(0);
        }

        // get next token, update internals
        public Token TakeToken()
        {
            if (lastTokenIndex + 1 >= tokenList.Count)
                AddTokens();
            if (lastTokenIndex + 1 < tokenList.Count)
                return tokenList[++lastTokenIndex];

            // no tokens left, add dedent to end file until back to start level 0
            if (indentLevel.Count > 1)
            {
                indentLevel.Pop();
                return new Token(TokenType.Dedent, "[DEDENT]", lines.Length + 1, 0);
            }

            return new Token(TokenType.EndOfFile, "[EOF]", lines.Length+1, 0);
        }

        /// <summary>
        /// Store current token state for backing up, or comitting
        /// </summary>
        public void PushState()
        {
            tokenIndexStack.Push(lastTokenIndex);
        }
        
        /// <summary>
        /// Restore token state for backing up
        /// </summary>
        public void PopState()
        {
            lastTokenIndex = tokenIndexStack.Pop();
        }

        /// <summary>
        /// remove last backup point
        /// </summary>
        public void CommitState()
        {
            tokenIndexStack.Pop();
        }

        // store indentation levels
        readonly Stack<int> indentLevel = new Stack<int>();

        /// <summary>
        /// Add tokens for the next line if possible
        /// If the line has end of line continue marker, do next line
        /// skips comments, tracks indent levels
        /// </summary>
        void AddTokens()
        {
            if (nextLineIndex >= lines.Length)
                return; // nothing to do
            // next line to parse, merge long lines, remove comments

            // todo - merge lines, remove comments
            string line = null;

            // get next line
            
            do
            {
                line = lines[nextLineIndex++];
                line = line.Replace("\r", "");
            } while (line.Trim().StartsWith("//") || String.IsNullOrEmpty(line.Trim()));


            var indent = IndentLevel(line);

            // remove strings
            var strMap = new Dictionary<string,string>();
            line = RemoveStrings(line,strMap);

            var words = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            // replace strings
            for (var i = 0; i < words.Length; ++i)
            {
                if (strMap.ContainsKey(words[i]))
                    words[i] = strMap[words[i]];
            }

            while (indent < indentLevel.Peek())
            {
                tokenList.Add(new Token(TokenType.Dedent, "[DEDENT]", nextLineIndex - 1, 0));
                indentLevel.Pop();
            }


            if (indent > indentLevel.Peek())
            {
                tokenList.Add(new Token(TokenType.Indent, "[INDENT]", nextLineIndex - 1, 0));
                indentLevel.Push(indent);
            }

            var startIndex = 0;
            foreach (var item in words)
            {
                if (String.IsNullOrEmpty(item))
                    continue; // some words packed into string
                var index = line.IndexOf(item, startIndex, StringComparison.Ordinal);
                startIndex += item.Length;

                var type = GetTokenType(item, tokenList.LastOrDefault());

                tokenList.Add(new Token(type, item, nextLineIndex-1, index));
            }

            tokenList.Add(new Token(TokenType.EndOfLine, "[EOL]", nextLineIndex - 1, line.Length));
        }

        // given a line, remove strings, putting them into a string dict for later lookup
        string RemoveStrings(string line, Dictionary<string, string> strMap)
        {
            var inString = false;
            var strCount = 0;
            var strStart = -1;
            for (var i = 0; i < line.Length; ++i)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (!inString)
                    {
                        strStart = i; // first char
                    }
                    else
                    {
                        var str = line.Substring(strStart, i - strStart + 1);
                        var rep = "\027" + $"<{++strCount}>";
                        strMap.Add(rep,str);
                        line = line.Replace(str, rep);
                    }
                    inString = !inString;
                }
            }
            return line;
        }

        // todo - this needs to be block sensitive
        List<string> typeNames = new List<string>
        {
            "char","bool","u32","i32","u8","i8","r32","string","void"
        };

        // determine token type from text
        TokenType GetTokenType(string item, Token last)
        {
            if (item.ToLower().StartsWith("0x"))
            {
                if (AllIn(item, 2, "09","AF","af","__"))
                    return TokenType.HexadecimalLiteral;
                throw new Exception($"Invalid hex number {item}");
            }
            else if (item.ToLower().StartsWith("0b"))
            {
                if (AllIn(item, 2, "01","__"))
                    return TokenType.BinaryLiteral;
                throw new Exception($"Invalid binary number {item}");
            }
            else if (Char.IsDigit(item[0]))
            {
                if (AllIn(item, 0, "09","__"))
                    return TokenType.DecimalLiteral;
                if (AllIn(item, 0, "09","__",".."))//todo && item.Count(cc=>cc=='.') == 1)
                    return TokenType.FloatLiteral;
                throw new Exception($"Invalid float or decimal number {item}");
            }
            else if (item[0] == '"')
                return TokenType.StringLiteral;
            else if (item[0] == '\'')
                return TokenType.CharacterLiteral;
            else if (last.TokenValue == "type")
            {
                // new typename - todo - scope so can move out
                typeNames.Add(item);
                return TokenType.TypeName;
            }
            else if (typeNames.Contains(item))
                return TokenType.TypeName;
            else if (AllIn(item,0,"AZ","az","09","__"))
                return TokenType.Identifier;

            return TokenType.Other;
        }

        bool AllIn(string item, int start, params string [] ranges)
        {
            for (var i = start; i < item.Length; ++i)
            {
                var c = item[i];
                var found = false;
                for (var j = 0; j < ranges.Length && !found; ++j)
                {
                    var r = ranges[j];
                    if (r[0] <= c && c <= r[1])
                        found = true;
                }
                if (!found)
                    return false;
            }
            return true;
        }

        // determine identation level for a line of code
        int IndentLevel(string line)
        {
            var c = 0;
            while (line[c] == ' ')
                ++c;
            return c;
        }

        public Token PeekToken()
        {
            PushState();
            var token = TakeToken();
            PopState();
            return token;
        }
#endif
    }
}
