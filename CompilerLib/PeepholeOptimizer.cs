using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lomont.ClScript.CompilerLib
{
    class PeepholeOptimizer
    {
        Environment env;

        public PeepholeOptimizer(Environment environment)
        {
            env = environment;
        }

        public void Optimize(List<Instruction> instructions)
        {
            var countRemoved = 0;
            var length = instructions.Count;
            for (var i = 0; i < instructions.Count; ++i)
            {
                var cur = instructions[i];
                var dual = i < instructions.Count - 1;
                var nxt = dual?instructions[i + 1]:null;
                if (dual && cur.Opcode == Opcode.Not && nxt.Opcode == Opcode.Not)
                {
                    instructions.RemoveAt(i);
                    instructions.RemoveAt(i);
                    i-=2;
                    countRemoved+=2;
                }
                if (dual & cur.Opcode == Opcode.BrAlways && nxt.Opcode == Opcode.BrAlways)
                {
                    instructions.RemoveAt(i + 1);
                    i--;
                    countRemoved++;
                }
                if (cur.Opcode == Opcode.ClearStack && (int)cur.Operands[0] == 0)
                {
                    instructions.RemoveAt(i);
                    i--;
                    countRemoved++;
                }
            }
            env.Info($"Peephole optimizer removed {countRemoved} instruction(s) out of {length}");
        }
    }
}
