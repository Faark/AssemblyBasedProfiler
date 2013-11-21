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
     * Todo: Sleep about how we want to handle methods.
     * - Using PTR would be awesome, since it allows to safly handle multiple assemblies.
     * - We NEED a way to get MethodInfos.
     * - Stupid but safe way: Generate a hidden dummy somewhere, that does nothing but to register all called methods.
     * - Slow and not more reliable method: Flag all methods with an attribute & register them via reflection / check all method addresses...
     * - Static constructor into a type... and register them there. It would only register methods for used objects... 
     * 
     * http://stackoverflow.com/questions/459560/initialize-library-on-assembly-load
     * - <Module> class... not distributed to thousands of types, though its not lazy-initialized?
     * 
     * => Static constructor. Benefits:
     * - We got them all, for sure and any time.
     * - Less initialization-impact.
     * - Allows to profile static constructors.
     * 
     * =>
     * 
     * What about a 2 lvl system? Register static constructors on Module initialization, but members to the static constructor?
     */
#warning Todo: Timeouts for all Waits?


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
/*#if DEBUG
        public static void LeaveDetailed(int methodId)
        {
            unprocessedEvents.Enqueue(new LeaveMethodEvent()
            {
                methodId = methodId,
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                time = Stopwatch.GetTimestamp()
            });
            ewh.Set();
        }
#else*/
        public static void Leave()
        {
            unprocessedEvents.Enqueue(new LeaveMethodEvent()
            {
                threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                time = Stopwatch.GetTimestamp()
            });
            ewh.Set();
        }
//#endif
        static void CheckNotCrashed()
        {
            if (Exception != null)
            {
                throw new InvalidOperationException("Profiler thread is crashed, cannot reset data.", Exception);
            }
        }
        static class ReportGeneration
        {
#warning we miss current stack frames... that might lead to invalid data /NaNs or worse..
            public static CaptureStateEvent AquireLock()
            {
                CheckNotCrashed();
                var cse = new CaptureStateEvent();
                unprocessedEvents.Enqueue(cse);
                ewh.Set();
                while (!cse.OnThreadStoppedAndDataSet.WaitOne(TimeSpan.FromMilliseconds(50)))
                {
                    CheckNotCrashed();
                }
                return cse;
            }
            public static void FreeLock(CaptureStateEvent cse)
            {
                cse.OnReadyToResume.Set();
            }

            static void WriteSeconds(StringBuilder trg, double time)
            {
                // more advanced times?
                trg.AppendFormat("{0:0.00}s", time / Stopwatch.Frequency);
            }
            static void GetLine(StringBuilder trg, ProfilingCallSite site, int indent, long timeParent, long timeTotal)
            {
                var globalShare = (double)site.RunDuration / (double)timeTotal;
                var localShare = (double)site.RunDuration / (double)timeParent;
                trg.Append(' ', indent * 2);
                MethodLibrary.GetText(site.Method, trg);
                trg.Append(": ")
                    .Append(site.NumberOfCalls)
                    .Append(" calls, runtime: ");
                WriteSeconds(trg, site.RunDuration);
                trg.Append(", ")
                    .AppendFormat("{0:0.00}% of parent, {1:0.00}% of entire call tree", localShare, globalShare)
                    .AppendLine();
            }
            public static void GetReportRecursiveLineBased(Action<string> consumer, StringBuilder trg, ProfilingCallSite site, int indent, long timeParent, long timeTotal)
            {
                GetLine(trg, site, indent, timeParent, timeTotal);
                consumer(trg.ToString());
                trg.Length = 0;
                foreach (var sub in site.SubCalls.OrderByDescending(s => s.Value.RunDuration))
                {
                    GetReportRecursiveLineBased(consumer, trg, sub.Value, indent + 1, site.RunDuration, timeTotal);
                }
            }
            public static void GetReportRecursive(StringBuilder trg, ProfilingCallSite site, int indent, long timeParent, long timeTotal)
            {
                GetLine(trg, site, indent, timeParent, timeTotal);
                foreach (var sub in site.SubCalls.OrderByDescending(s => s.Value.RunDuration))
                {
                    GetReportRecursive(trg, sub.Value, indent + 1, site.RunDuration, timeTotal);
                }
            }
        }

        public static string GetCurrentReport()
        {
            var cse = ReportGeneration.AquireLock();
            var sb = new StringBuilder();
            sb.AppendLine("SubtractedPerCallError = " + State.PerCallErrorToSubtract + Environment.NewLine + Environment.NewLine);
            foreach (var ep in cse.InitialCallSites.OrderByDescending(el => el.RunDuration))
            {
                ReportGeneration.GetReportRecursive(sb, ep, 0, ep.RunDuration, ep.RunDuration);
            }
            ReportGeneration.FreeLock(cse);
            return sb.ToString();
        }
        public static void GetCurrentReport(Action<string> line_consumer)
        {
            var cse = ReportGeneration.AquireLock();
            line_consumer("SubtractedPerCallError = " + State.PerCallErrorToSubtract + Environment.NewLine + Environment.NewLine);
            var sb = new StringBuilder();
            foreach (var ep in cse.InitialCallSites.OrderByDescending(el => el.RunDuration))
            {
                ReportGeneration.GetReportRecursiveLineBased(line_consumer, sb, ep, 0, ep.RunDuration, ep.RunDuration);
            }
            ReportGeneration.FreeLock(cse);
        }
        public static void ClearProfilingData()
        {
            CheckNotCrashed();
            unprocessedEvents.Enqueue(new ResetStateEvent());
            ewh.Set();
        }

        /// <summary>
        /// In case the profiler thread crashed will this contain the correspondig exception.
        /// </summary>
        public static Exception Exception { get; private set; }
    }
}
