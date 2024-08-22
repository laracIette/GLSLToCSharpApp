using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GLSLToCSharpApp
{
    public struct Uniform
    {
        public string Type { get; set; }

        public string Name { get; set; }

        public bool IsArray { get; set; }
    }
}
