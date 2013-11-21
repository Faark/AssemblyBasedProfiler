using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{

    public class ResetStateEvent : IProfilingEvent {
        public override string ToString()
        {
            return "ResetStateEvent";
        }
    }
}
