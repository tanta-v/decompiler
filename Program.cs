using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static decompiler.Utility;
namespace decompiler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            log("Hello, World!");
            Decompiler compt = new Decompiler($"HEVD.sys", type: DecompileType.kernelSYS, recompilationMethod: RecompilationType.RAWSIMPLE);
        }
    }
}
