#define TRY_TO_CORRECT_OVERHEAD

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace ProfilerLib
{
    public class ProfilingState
    {
        public class StackFrame
        {
            //public int MethodId;
            public long EnterTime;
            public ProfilingCallSite Site;
            public long FrameErrorCorrectionReduction;
        }
        class ThreadData
        {
            public int thread_id;
            //public string Name;
            public long FirstRecorded;
            public long LastRecorded;
            public Stack<StackFrame> Stack = new Stack<StackFrame>();
        }
        public Dictionary<int, ProfilingCallSite> Data_EntryPoints = new Dictionary<int, ProfilingCallSite>();
        Dictionary<int, ThreadData> Threads = new Dictionary<int, ThreadData>();
        public readonly long PerCallErrorToSubtract;

        public ProfilingState(long ownAvgOffsetPerCall_toSubtract)
        {
            PerCallErrorToSubtract = ownAvgOffsetPerCall_toSubtract;
        }

        void do_dumpData(CaptureStateEvent info)
        {
            info.InitialCallSites = Data_EntryPoints.Values;
            info.StackFrames = Threads.Select(t => t.Value.Stack.Reverse());
            info.OnThreadStoppedAndDataSet.Set();
            info.OnReadyToResume.WaitOne();
        }
        void do_resetData()
        {
            Data_EntryPoints.Clear();
            // No we have to re-build data for the current stacks... :(
            // warning/todo: Do not forget to test this...
            foreach (var t in Threads.Values.ToList())
            {
                if (t.Stack.Count > 0)
                {
                    var lastLogTime = Math.Max(t.LastRecorded, t.Stack.Peek().EnterTime);
                    ProfilingCallSite parent = null;
                    foreach (var el in t.Stack.Reverse())
                    {
                        var newFrame = new ProfilingCallSite() { Method = el.Site.Method, Parent = parent, NumberOfCalls = 0 };
                        el.EnterTime = lastLogTime;
                        if (parent != null)
                        {
                            newFrame = parent.SubCalls.GetOrSet(el.Site.Method, newFrame);
                        }
                        else
                        {
                            newFrame = Data_EntryPoints.GetOrSet(el.Site.Method, newFrame);
                        }
                    }
                    t.FirstRecorded = lastLogTime;
                    t.LastRecorded = lastLogTime;
                }
                else
                {
                    Threads.Remove(t.thread_id);
                }
            }
        }
//#if DEBUG
//        public List<IProfilingEvent> processedEvents = new List<IProfilingEvent>();
//#endif
        public void ProcessEvent(ConcurrentQueue<IProfilingEvent> item_source)
        {
            IProfilingEvent item;
            int last_thread_id = -1;
            ThreadData current_thread = null;
            while (item_source.TryDequeue(out item))
            {
//#if DEBUG
//                processedEvents.Add(item);
//#endif
                var methodEvent = item as MethodEventBase;
                if (methodEvent != null)
                {
                    if (methodEvent.threadId != last_thread_id)
                    {
                        last_thread_id = methodEvent.threadId;
                        if (!Threads.TryGetValue(last_thread_id, out current_thread))
                        {
                            Threads.Add(last_thread_id, current_thread = new ThreadData() { FirstRecorded = methodEvent.time, thread_id = last_thread_id });
                        }
                    }

                    var enterMethodEvent = methodEvent as EnterMethodEvent;
                    if (enterMethodEvent != null)
                    {
                        ProfilingCallSite enteredSite;
                        if (current_thread.Stack.Count > 0)
                        {
                            var callingSize = current_thread.Stack.Peek().Site;
                            var siteSubs = callingSize.SubCalls;
                            if (!siteSubs.TryGetValue(enterMethodEvent.methodId, out enteredSite))
                            {
                                siteSubs.Add(enterMethodEvent.methodId, enteredSite = new ProfilingCallSite() { Parent = callingSize, Method = enterMethodEvent.methodId });
                            }
                        }
                        else
                        {
                            if (!Data_EntryPoints.TryGetValue(enterMethodEvent.methodId, out enteredSite))
                            {
                                Data_EntryPoints.Add(enterMethodEvent.methodId, enteredSite = new ProfilingCallSite() { Method = enterMethodEvent.methodId });
                            }
                        }
                        current_thread.Stack.Push(new StackFrame() {
                            EnterTime = enterMethodEvent.time,
                            Site = enteredSite 
                        });
                        enteredSite.NumberOfCalls++;
                    }
                    else // if (methodEvent is LeaveMethodEvent)
                    {
                        current_thread.LastRecorded = methodEvent.time;
                        var leftFrame = current_thread.Stack.Pop();
//#if DEBUG
//                        if (leftFrame.Site.Method != (methodEvent as LeaveMethodEvent).methodId)
//                        {
//                            throw new Exception("Leaving unexpected site. Expected: " + MethodLibrary.GetText(leftFrame.Site.Method) + ", got " + MethodLibrary.GetText((methodEvent as LeaveMethodEvent).methodId));
//                        }
//#endif
#if TRY_TO_CORRECT_OVERHEAD
                        var actualUncorrectedRuntime = methodEvent.time - leftFrame.EnterTime;
                        var frameTotalErrorCorrectionReduction = leftFrame.FrameErrorCorrectionReduction + PerCallErrorToSubtract;
                        if (actualUncorrectedRuntime > frameTotalErrorCorrectionReduction)
                        {
                            leftFrame.Site.RunDuration += actualUncorrectedRuntime - frameTotalErrorCorrectionReduction;
                        }
                        else
                        {
                            // 0 Run Duration or even negative... QOS monitoring would be nice, to help addapt those goddamn ErrorCorrectionOffset
                            frameTotalErrorCorrectionReduction = actualUncorrectedRuntime;
                        }
                        if (current_thread.Stack.Count > 0)
                        {
                            current_thread.Stack.Peek().FrameErrorCorrectionReduction += frameTotalErrorCorrectionReduction;
                        }
#else
                        leftFrame.Site.RunDuration += methodEvent.time - leftFrame.EnterTime;
#endif
                    }
                }
                else
                {
                    var dumpData = item as CaptureStateEvent;
                    var resetData = item as ResetStateEvent;
                    if (dumpData != null)
                    {
                        do_dumpData(dumpData);
                    }
                    else if (resetData != null)
                    {
                        do_resetData();
                    }
                    else
                    {
                        throw new InvalidOperationException("Looks like you have missed to implement sth new?");
                    }
                }
            }
            if (autoSaveInterval > 0 && (autoSaveLastDate+TimeSpan.FromSeconds(autoSaveInterval) < DateTime.Now) )
            {
                do_autoSaveStatus();
            }
        }

        private void do_autoSaveStatus()
        {
            // Todo: No stack frames here, as well...
            autoSaveStream.SetLength(0);
            var tw = new System.IO.StreamWriter(autoSaveStream);
            tw.Write("SubtractedPerCallError = " + PerCallErrorToSubtract + Environment.NewLine + Environment.NewLine);
            foreach (var ep in Data_EntryPoints.Values.OrderByDescending(el => el.RunDuration))
            {
                ProfilerLib.ReportGeneration.GetReportRecursiveLineBased(line=>tw.Write(line), new StringBuilder(), ep, 0, ep.RunDuration, ep.RunDuration);
            }
            autoSaveLastDate = DateTime.Now;
        }

        private DateTime autoSaveLastDate;
        private System.IO.FileStream autoSaveStream;
        private int autoSaveInterval;
        public void SetAutoSaving(int interval, System.IO.FileStream fileSteam)
        {
            autoSaveInterval = interval;
            autoSaveLastDate = DateTime.Now;
            autoSaveStream = fileSteam;
        }
    }
}
