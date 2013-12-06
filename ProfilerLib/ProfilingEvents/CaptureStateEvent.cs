using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ProfilerLib
{
    class CaptureStateEvent : IProfilingEvent
    {
        /*
         * This one is critical. I have to suspend the processing thread, but would prefer to generate the string in the worker thread for now (exception handling...)
         * => ResetEvents for both stoping and resuming
         * => Pointer to data struct (only allowed to be used between AutoResetEvents!)
         */
        public AutoResetEvent OnThreadStoppedAndDataSet = new AutoResetEvent(false);
        public AutoResetEvent OnReadyToResume = new AutoResetEvent(false);
        public IEnumerable<ProfilingCallSite> InitialCallSites;
        public IEnumerable<IEnumerable<ProfilingState.StackFrame>> StackFrames;

        public override string ToString()
        {
            return "CaptureStateEvent";
        }
    }
}
