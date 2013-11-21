using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{
    public class LeaveMethodEvent : MethodEventBase
    {
        //#if DEBUG
        //        public int methodId;
        //#endif
        public override string ToString()
        {
            return "LeaveMethodEvent{Thread: " + threadId + ", Time: " + time + "}";
            //return "LeaveMethodEvent{Method: " + methodId + ", Thread: " + threadId + ", Time: " + time + "}";
        }
    }
}
