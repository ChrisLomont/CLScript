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
            for (var i = 0; i < instructions.Count-1; ++i)
            {
                if (instructions[i].Opcode == Opcode.Not && instructions[i + 1].Opcode == Opcode.Not)
                {
                    instructions.RemoveAt(i);
                    instructions.RemoveAt(i);
                    i-=2;
                    countRemoved+=2;
                }
                if (instructions[i].Opcode == Opcode.BrAlways && instructions[i + 1].Opcode == Opcode.BrAlways)
                {
                    instructions.RemoveAt(i + 1);
                    i--;
                    countRemoved++;
                }
            }
            env.Info($"Peephole optimizer removed {countRemoved} instructions out of {length}");
        }
    }
}
