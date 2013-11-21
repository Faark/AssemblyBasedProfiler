using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            TestLib.Work.DoIt();
            Console.WriteLine(ProfilerLib.Profiler.GetCurrentReport());
            Console.ReadLine();
        }
    }

}
