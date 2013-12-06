using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{
    class LeaveMethodEvent : MethodEventBase
    {
        public override string ToString()
        {
            return "LeaveMethodEvent{Thread: " + threadId + ", Time: " + time + "}";
        }
    }
    class LeaveMethodExEvent : LeaveMethodEvent
    {
        public int MethodId;
        public override string ToString()
        {
            return "LeaveMethodEvent{Method: " + MethodId + ", Thread: " + threadId + ", Time: " + time + "}";
        }
    }
}
