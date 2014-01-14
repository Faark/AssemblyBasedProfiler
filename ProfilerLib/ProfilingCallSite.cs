using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{
    class ProfilingCallSite
    {
        public int MethodId;
        public int NumberOfCalls;
        public long RunDuration;
        public Dictionary<int, ProfilingCallSite> SubCalls = new Dictionary<int, ProfilingCallSite>();
        public ProfilingCallSite Parent;
    }
}
