using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{

    class EnterMethodEvent : MethodEventBase
    {
        public int methodId;

        public override string ToString()
        {
            return "EnterMethodEvent{Method: " + methodId + ", Thread: " + threadId + ", Time: " + time + "}";
        }
    }
}
