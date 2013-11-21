using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{
    public class ProfilingCallSite
    {
        /// <summary>
        /// 0-MaxInt = Method in  MethodLibrary
        /// -1 = Recursive call ? How do we want to handle recursion? Skip on first occurance?
        /// (-Int - 1) = Recursive call?
        /// Update: No recursion "detection" for now...
        /// </summary>
        public int Method;
        public int NumberOfCalls;
        public long RunDuration;
        public Dictionary<int, ProfilingCallSite> SubCalls = new Dictionary<int, ProfilingCallSite>();
        public ProfilingCallSite Parent;
    }
}
