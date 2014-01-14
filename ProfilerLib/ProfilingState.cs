using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace ProfilerLib
{
    /// <summary>
    /// 
    /// </summary>
    class ProfilingState
    {
        public class StackFrame
        {
            public long EnterTime;
            public ProfilingCallSite Site;
            public long FrameErrorCorrectionReduction;
        }
        class ThreadData
        {
            public int thread_id;
            //public string Name; does not look like we are able to query a threads .net name...
            public long FirstRecorded;
            public long LastRecorded;
            public Stack<StackFrame> Stack = new Stack<StackFrame>();
        }
        Dictionary<int, ProfilingCallSite> entryPoints = new Dictionary<int, ProfilingCallSite>();
        Dictionary<int, ThreadData> threads = new Dictionary<int, ThreadData>();

        public long PerCallErrorToSubtract { get; set; }
        public bool CorrectPerCallOffset { get; set; }

        public ProfilingState(long ownAvgOffsetPerCall_toSubtract)
        {
            PerCallErrorToSubtract = ownAvgOffsetPerCall_toSubtract;
            CorrectPerCallOffset = true;
        }

        void processEvent_captureState(CaptureStateEvent info)
        {
            info.InitialCallSites = entryPoints.Values;
            info.StackFrames = threads.Select(t => t.Value.Stack.Reverse());
            info.OnThreadStoppedAndDataSet.Set();
            info.OnReadyToResume.WaitOne();
        }
        void processEvent_resetData()
        {
            entryPoints.Clear();
            foreach (var t in threads.Values.ToList())
            {
                if (t.Stack.Count > 0)
                {
                    // Now we have to re-build data for the current stacks... :(
                    // warning/todo: Test this...
                    var lastLogTime = Math.Max(t.LastRecorded, t.Stack.Peek().EnterTime);
                    ProfilingCallSite parent = null;
                    foreach (var el in t.Stack.Reverse())
                    {
                        var newFrame = new ProfilingCallSite() { MethodId = el.Site.MethodId, Parent = parent, NumberOfCalls = 0 };
                        el.EnterTime = lastLogTime;
                        if (parent != null)
                        {
                            newFrame = parent.SubCalls.GetOrSet(el.Site.MethodId, newFrame);
                        }
                        else
                        {
                            newFrame = entryPoints.GetOrSet(el.Site.MethodId, newFrame);
                        }
                    }
                    t.FirstRecorded = lastLogTime;
                    t.LastRecorded = lastLogTime;
                }
                else
                {
                    threads.Remove(t.thread_id);
                }
            }
        }
        void processEvent_Enter(EnterMethodEvent enterMethodEvent, ThreadData current_thread)
        {
            ProfilingCallSite enteredSite;
            if (current_thread.Stack.Count > 0)
            {
                var callingSize = current_thread.Stack.Peek().Site;
                var siteSubs = callingSize.SubCalls;
                if (!siteSubs.TryGetValue(enterMethodEvent.methodId, out enteredSite))
                {
                    siteSubs.Add(enterMethodEvent.methodId, enteredSite = new ProfilingCallSite() { Parent = callingSize, MethodId = enterMethodEvent.methodId });
                }
            }
            else
            {
                if (!entryPoints.TryGetValue(enterMethodEvent.methodId, out enteredSite))
                {
                    entryPoints.Add(enterMethodEvent.methodId, enteredSite = new ProfilingCallSite() { MethodId = enterMethodEvent.methodId });
                }
            }
            current_thread.Stack.Push(new StackFrame()
            {
                EnterTime = enterMethodEvent.time,
                Site = enteredSite
            });
            enteredSite.NumberOfCalls++;
        }
        void processEvent_Leave(LeaveMethodEvent leaveMethodEvent, ThreadData current_thread)
        {
            current_thread.LastRecorded = leaveMethodEvent.time;
            var leaveMethodExEvent = leaveMethodEvent as LeaveMethodExEvent;
            if (current_thread.Stack.Count <= 0)
            {
                if (leaveMethodExEvent != null)
                    throw new Exception("Unexpected leave of " + MethodLibrary.GetText(leaveMethodExEvent.MethodId) + ". The stack of this thread is empty!");
                else
                    throw new Exception("Unexpected leave. The stack of this thread is empty!");
            }
            var leftFrame = current_thread.Stack.Pop();
            if ((leaveMethodExEvent != null) && (leftFrame.Site.MethodId != leaveMethodExEvent.MethodId))
            {
                throw new Exception("Leaving unexpected site. Expected: " + MethodLibrary.GetText(leftFrame.Site.MethodId) + ", got " + MethodLibrary.GetText(leaveMethodExEvent.MethodId));
            }
            if (CorrectPerCallOffset)
            {
                var actualUncorrectedRuntime = leaveMethodEvent.time - leftFrame.EnterTime;
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
                //leftFrame.Site.SubtractedRecordingOffset += frameTotalErrorCorrectionReduction;
                if (current_thread.Stack.Count > 0)
                {
                    current_thread.Stack.Peek().FrameErrorCorrectionReduction += frameTotalErrorCorrectionReduction;
                }
            }
            else
            {
                leftFrame.Site.RunDuration += leaveMethodEvent.time - leftFrame.EnterTime;
            }
        }
        public void ProcessEvent(ConcurrentQueue<IProfilingEvent> item_source)
        {
            IProfilingEvent item;
            int last_thread_id = -1;
            ThreadData current_thread = null;
            while (item_source.TryDequeue(out item))
            {
                var methodEvent = item as MethodEventBase;
                if (methodEvent != null)
                {
                    if (methodEvent.threadId != last_thread_id)
                    {
                        last_thread_id = methodEvent.threadId;
                        if (!threads.TryGetValue(last_thread_id, out current_thread))
                        {
                            threads.Add(last_thread_id, current_thread = new ThreadData() { FirstRecorded = methodEvent.time, thread_id = last_thread_id });
                        }
                    }

                    var enterMethodEvent = methodEvent as EnterMethodEvent;
                    var leaveMethodEvent = methodEvent as LeaveMethodEvent;
                    if (enterMethodEvent != null)
                    {
                        processEvent_Enter(enterMethodEvent, current_thread);
                    }
                    else if (leaveMethodEvent != null)
                    {
                        processEvent_Leave(leaveMethodEvent, current_thread);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown MethodEvent type: " + item.GetType().FullName);
                    }
                }
                else
                {
                    var dumpData = item as CaptureStateEvent;
                    var resetData = item as ResetStateEvent;
                    if (dumpData != null)
                    {
                        processEvent_captureState(dumpData);
                    }
                    else if (resetData != null)
                    {
                        processEvent_resetData();
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown Event type: "+ item.GetType().FullName);
                    }
                }
            }
            if (autoSaveInterval > 0 && (autoSaveLastDate+TimeSpan.FromSeconds(autoSaveInterval) < DateTime.Now) )
            {
                autoSaveStatus();
            }
        }

        #region automatic report saving
        DateTime autoSaveLastDate;
        System.IO.FileInfo autoSaveTargetFile;
        int autoSaveInterval;
        public void SetAutoSaving(int interval, System.IO.FileInfo file)
        {
            autoSaveInterval = interval;
            autoSaveLastDate = DateTime.Now;
            autoSaveTargetFile = file;
        }

        void autoSaveStatus()
        {
            // Todo: No stack frames here, as well...

            using (var tw = autoSaveTargetFile.CreateText())
            {
                //autoSaveStream.SetLength(0);
                //var tw = new System.IO.StreamWriter(autoSaveStream);
                tw.Write("SubtractedPerCallError = " + PerCallErrorToSubtract + Environment.NewLine + Environment.NewLine);
                foreach (var ep in entryPoints.Values.OrderByDescending(el => el.RunDuration))
                {
                    ProfilerLib.ReportGeneration.GetReportRecursiveLineBased(line => tw.Write(line), new StringBuilder(), ep, 0, ep.RunDuration, ep.RunDuration);
                }
                autoSaveLastDate = DateTime.Now;
            }
        }
        #endregion
    }
}
