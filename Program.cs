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
            new Decompiler($"C:\\Users\\200123\\Documents\\GitHub\\networker\\bin\\Debug\\net8.0\\networker.exe");
        }
    }
}
