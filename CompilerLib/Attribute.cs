using System.Collections.Generic;
using System.Linq;

namespace Lomont.ClScript.CompilerLib
{
    public class Attribute
    {
        public List<string> Parameters { get; set;  }
        public string Name { get; }

        public Attribute(string name, IEnumerable<string> parameters)
        {
            Name = name;
            Parameters = parameters.ToList();
        }

        public override string ToString()
        {
            return $"[{Name}]";
        }
    }
}
