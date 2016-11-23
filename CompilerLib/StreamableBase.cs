using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    public class TokenizableStreamBase<T> where T : class
    {
        public TokenizableStreamBase(Func<List<T>> extractor)
        {
            Index = 0;

            Items = extractor();

            SnapshotIndexes = new Stack<int>();
        }

        protected List<T> Items { get; set; }

        protected int Index { get; set; }

        private Stack<int> SnapshotIndexes { get; set; }

        public virtual T Current
        {
            get
            {
                if (EOF(0))
                {
                    return null;
                }

                return Items[Index];
            }
        }

        public T Consume()
        {
            var a = Current;
            Index++;
            return a;
        }

        private Boolean EOF(int lookahead)
        {
            if (Index + lookahead >= Items.Count)
            {
                return true;
            }

            return false;
        }

        public Boolean End()
        {
            return EOF(0);
        }

        public virtual T Peek(int lookahead)
        {
            if (EOF(lookahead))
            {
                return null;
            }

            return Items[Index + lookahead];
        }

        public void TakeSnapshot()
        {
            SnapshotIndexes.Push(Index);
        }

        public void RollbackSnapshot()
        {
            Index = SnapshotIndexes.Pop();
        }

        public void CommitSnapshot()
        {
            SnapshotIndexes.Pop();
        }
    }
}
