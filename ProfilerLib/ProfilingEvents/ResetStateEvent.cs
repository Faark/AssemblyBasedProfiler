using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{

    class ResetStateEvent : IProfilingEvent
    {
        public override string ToString()
        {
            return "ResetStateEvent";
        }
    }
}
