using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{
    class MethodEventBase : IProfilingEvent
    {
        public long time;
        public int threadId;
    }
}
