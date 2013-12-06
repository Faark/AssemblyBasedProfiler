using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ProfilerLib
{
    static class ReportGeneration
    {
#warning we miss current stack frames... that might lead to invalid data /NaNs or worse..
        static void GetLine(StringBuilder trg, ProfilingCallSite site, int indent, long timeParent, long timeTotal)
        {
            var globalShare = (double)site.RunDuration / (double)timeTotal;
            var localShare = (double)site.RunDuration / (double)timeParent;
            trg.Append(' ', indent * 2);
            MethodLibrary.GetText(site.MethodId, trg);
            trg.Append(": ")
                .Append(site.NumberOfCalls)
                .Append(" calls, runtime: ");
            WriteSeconds(trg, site.RunDuration);
            trg.Append(", ")
                .AppendFormat("{0:0.00}% of parent, {1:0.00}% of entire call tree", localShare * 100.0, globalShare * 100.0)
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

        static void WriteSeconds(StringBuilder trg, double time)
        {
            // more advanced times?
            trg.AppendFormat("{0:0.00}s", time / Stopwatch.Frequency);
        }
    }
}
