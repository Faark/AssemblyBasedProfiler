using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Concurrent;


namespace ProfilerLib
{
    /*
     * 
     * How does it work?
     * * add text*
     * 
     * 
     * Todos:
     * - add thread statistics (should be easy to add to profilingstate); but not very useful for ksp & we need a more general data collection method anyway...
     * 
     * 
     */
    /*
     * 
     * Just dumping some thougths:
     * 
     * Todo: Sleep about how we want to handle methods.
     * - Using PTR would be awesome, since it allows to safly handle multiple assemblies.
     * => Turns out to be .... "complicated" on generics
     * 
     * - Methods are currently registerd in the classes static constructor. Thats fine. We can't register a static constructor, since its not accessible (though i think that was PTR as well), but they are only executed once anyway
     *
     * - Another slow and likely not more reliable method: Flag all methods with an attribute & register them via reflection / check all method addresses...
     * 
     */
#warning Todo: Timeouts for all Waits?


    /// <summary>
    /// The Profiler class is the public interface of this profiler.
    /// </summary>
    /// <remarks>
    /// The user of this profiler should not have to touch anything but this static interface.
    /// </remarks>
    public static class Profiler
    {

        static readonly ConcurrentQueue<IProfilingEvent> unprocessedEvents;
        static readonly AutoResetEvent ewh;
        static long profileOffset()
        {
            var start = Stopwatch.GetTimestamp();
            var testCnt = 500000;
            for (var i = 0; i < testCnt; i++)
            {
                Enter(0);
                try
                {
                    // some kind of "Nop" here?
                }
                finally
                {
                    Leave();
                }
            }
            var stop = Stopwatch.GetTimestamp();

            IProfilingEvent tmp;
            while (unprocessedEvents.Count > 0)
            {
                unprocessedEvents.TryDequeue(out tmp);
            }

            return (stop - start) / testCnt;
        }
        static Profiler()
        {
            ewh = new AutoResetEvent(false);
            unprocessedEvents = new ConcurrentQueue<IProfilingEvent>();
            State = new ProfilingState(profileOffset());
            var t = new Thread(ProfConsumerThread);
            t.IsBackground = true;
            t.Name = "ProfilingDataProcessor";
            t.Start();
        }
        static ProfilingState State;
        static System.IO.FileStream autoSaveStream;
        static void ProfConsumerThread()
        {
            try
            {
                while (true)
                {
                    ewh.WaitOne();
                    State.ProcessEvent(unprocessedEvents);
                }
            }
            catch (Exception err)
            {
                Exception = err;
            }
        }
        static void checkNotCrashed()
        {
            if (Exception != null)
            {
                throw new InvalidOperationException("Profiler thread is crashed, cannot reset data.", Exception);
            }
        }
        static class ReportGeneration
        {
            public static CaptureStateEvent AquireLock()
            {
                checkNotCrashed();
                var cse = new CaptureStateEvent();
                unprocessedEvents.Enqueue(cse);
                ewh.Set();
                while (!cse.OnThreadStoppedAndDataSet.WaitOne(TimeSpan.FromMilliseconds(50)))
                {
                    checkNotCrashed();
                }
                return cse;
            }
            public static void FreeLock(CaptureStateEvent cse)
            {
                cse.OnReadyToResume.Set();
            }

        }


        /// <summary>
        /// In case the profiler thread has crashed will this contain the correspondig exception.
        /// </summary>
        public static Exception Exception { get; private set; }

        public static int AutoDataSavingInterval { get; private set; }
        public static string AutoDataSavingLocation { get; private set; }


        public static void SetAutoDataSaving(int secs_between, string loc)
        {
            AutoDataSavingInterval = Math.Max(secs_between, 0);
            if (secs_between > 0)
            {
                if (loc != AutoDataSavingLocation)
                {
                    if (autoSaveStream != null)
                        autoSaveStream.Close();
                    autoSaveStream = System.IO.File.Open(loc, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write);
                }
                State.SetAutoSaving(secs_between, autoSaveStream);
            }
            else
            {
                State.SetAutoSaving(0, null);
                if (autoSaveStream != null)
                    autoSaveStream.Close();
                autoSaveStream = null;
            }
            AutoDataSavingLocation = loc;
        }
        public static void SetAutoDataSaving(int secs_between = 0)
        {
            SetAutoDataSaving(secs_between, AutoDataSavingLocation);
        }

        /// <summary>
        /// Enter(methodId) is called by a method when it is entered.
        /// </summary>
        /// <param name="methodId"></param>
        /// <remarks>It adds the acording data to the profilers internal event queue.</remarks>
        public static void Enter(int methodId)
        {
            unprocessedEvents.Enqueue(new EnterMethodEvent()
            {
                methodId = methodId,
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                time = Stopwatch.GetTimestamp()
            });
            ewh.Set();
        }
        public static void Leave()
        {
            unprocessedEvents.Enqueue(new LeaveMethodEvent()
            {
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                time = Stopwatch.GetTimestamp()
            });
            ewh.Set();
        }
        public static void LeaveEx(int methodId)
        {
            unprocessedEvents.Enqueue(new LeaveMethodExEvent()
            {
                MethodId = methodId,
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                time = Stopwatch.GetTimestamp()
            });
            ewh.Set();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <remarks>This method is blocking. It will wait </remarks>
        public static string GetCurrentReport()
        {
            var cse = ReportGeneration.AquireLock();
            var sb = new StringBuilder();
            sb.AppendLine("SubtractedPerCallError = " + State.PerCallErrorToSubtract + Environment.NewLine + Environment.NewLine);
            foreach (var ep in cse.InitialCallSites.OrderByDescending(el => el.RunDuration))
            {
                ProfilerLib.ReportGeneration.GetReportRecursive(sb, ep, 0, ep.RunDuration, ep.RunDuration);
            }
            ReportGeneration.FreeLock(cse);
            return sb.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="line_consumer"></param>
        public static void GetCurrentReport(Action<string> line_consumer)
        {
            var cse = ReportGeneration.AquireLock();
            line_consumer("SubtractedPerCallError = " + State.PerCallErrorToSubtract + Environment.NewLine + Environment.NewLine);
            var sb = new StringBuilder();
            foreach (var ep in cse.InitialCallSites.OrderByDescending(el => el.RunDuration))
            {
                ProfilerLib.ReportGeneration.GetReportRecursiveLineBased(line_consumer, sb, ep, 0, ep.RunDuration, ep.RunDuration);
            }
            ReportGeneration.FreeLock(cse);
        }

        /// <summary>
        /// Requests the profiling data to be cleared/reset.
        /// </summary>
        /// <remarks>It just adds the request to the profilers internal event queue.</remarks>
        public static void ClearProfilingData()
        {
            checkNotCrashed();
            unprocessedEvents.Enqueue(new ResetStateEvent());
            ewh.Set();
        }
    }
}
