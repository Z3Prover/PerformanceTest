using System;
using System.Collections.Generic;
using System.IO;

namespace Measurement
{
    public class Measure
    {
        public Measure(TimeSpan totalProcessorTime, TimeSpan wallClockTime, double peakMemorySizeMB, LimitsStatus limits)
        {
            TotalProcessorTime = totalProcessorTime;
            WallClockTime = wallClockTime;
            PeakMemorySizeMB = peakMemorySizeMB;
            Limits = limits;
        }

        public TimeSpan TotalProcessorTime { get; private set; }
        public TimeSpan WallClockTime { get; private set; }

        /// <summary>
        /// Gets the maximum amount of virtual memory, in Mega Bytes, allocated for the process.
        /// </summary>
        public double PeakMemorySizeMB { get; private set; }

        public LimitsStatus Limits { get; private set; }

        public enum LimitsStatus
        {
            /// <summary>Process exited without exceeding memory or time limit.</summary>
            WithinLimits,
            /// <summary>Process exceed memory limit.</summary>
            MemoryOut,
            /// <summary>Process exceed time limit.</summary>
            TimeOut
        }
    }

    public class ProcessRunMeasure : Measure
    {
        public ProcessRunMeasure(TimeSpan totalProcessorTime, TimeSpan wallClockTime, double peakMemorySizeMB, LimitsStatus status, int? exitCode, Stream stdOut, Stream stdErr) :
            base(totalProcessorTime, wallClockTime, peakMemorySizeMB, status)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public int? ExitCode { get; private set; }
        public Stream StdOut { get; private set; }
        public Stream StdErr { get; private set; }

        public string OutputToString()
        {
            StreamReader r = new StreamReader(StdOut);
            string s = r.ReadToEnd();
            StdOut.Position = 0L;
            return s;
        }
        public string ErrorToString()
        {
            StreamReader r = new StreamReader(StdErr);
            string s = r.ReadToEnd();
            StdErr.Position = 0L;
            return s;
        }
    }

}
