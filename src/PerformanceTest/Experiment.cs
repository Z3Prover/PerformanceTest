using Measurement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using ExperimentID = System.Int32;


namespace PerformanceTest
{
    public class ExperimentDefinition
    {
        public static ExperimentDefinition Create(string executable, string benchmarkContainerUri, string benchmarkDirectory, string benchmarkFileExtension, string parameters,
            TimeSpan benchmarkTimeout, TimeSpan experimentTimeout,
            string domainName,
            string category = null, double memoryLimitMB = 0, int adaptiveRunMaxRepetitions = 10, double adaptiveRunMaxTimeInSeconds = 10)
        {
            return new ExperimentDefinition()
            {
                Executable = executable,
                BenchmarkContainerUri = benchmarkContainerUri,
                BenchmarkDirectory = benchmarkDirectory,
                BenchmarkFileExtension = benchmarkFileExtension,
                Parameters = parameters,
                BenchmarkTimeout = benchmarkTimeout,
                ExperimentTimeout = experimentTimeout,
                Category = category,
                MemoryLimitMB = memoryLimitMB,
                DomainName = domainName,
                AdaptiveRunMaxRepetitions = adaptiveRunMaxRepetitions,
                AdaptiveRunMaxTimeInSeconds = adaptiveRunMaxTimeInSeconds
            };
        }

        private ExperimentDefinition()
        {
        }

        /// <summary>
        /// A path to a file which is either an executable file or a zip file which contains a main executable and supporting files.
        /// The executable will run for multiple specified benchmark files to measure its performance.
        /// </summary>
        public string Executable { get; private set; }

        /// <summary>
        /// Name of a domain which determines an additional analysis and process results interpretation.
        /// </summary>
        public string DomainName { get; private set; }

        /// <summary>
        /// Command-line parameters for the executable.
        /// Special symbols:
        ///  - "{0}" will be replaced with a path to a benchmark file.
        /// </summary>
        public string Parameters { get; private set; }

        public const string LocalDiskContainerUri = "local";
        public const string DefaultContainerUri = "default";
        /// <summary>
        /// A uri of a container with the benchmark files.
        /// "local" for files on local disc
        /// "default" for default container
        /// </summary>
        public string BenchmarkContainerUri { get; private set; }

        /// <summary>
        /// A directory within the container with the benchmark files.
        /// </summary>
        public string BenchmarkDirectory { get; private set; }


        /// <summary>
        /// A category name to draw benchmarks from. Can be null or empty string.
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// The extension of benchmark files, e.g., "smt2" for SMT-Lib version 2 files.
        /// </summary>
        public string BenchmarkFileExtension { get; private set; }



        /// <summary>
        /// The memory limit per benchmark (megabytes).
        /// Zero means no limit.
        /// </summary>
        public double MemoryLimitMB { get; private set; }

        /// <summary>
        /// The time limit per benchmark.
        /// </summary>
        public TimeSpan BenchmarkTimeout { get; private set; }

        /// <summary>
        /// The time limit per experiment.
        /// </summary>
        public TimeSpan ExperimentTimeout { get; private set; }

        /// <summary>
        /// Maximum number of repetitions of short benchmarks (1 for turning adaptivity off)
        /// </summary>
        public int AdaptiveRunMaxRepetitions { get; private set; }

        /// <summary>
        /// Maximum total duration of adaptive runs in seconds
        /// </summary>
        public double AdaptiveRunMaxTimeInSeconds { get; private set; }

        public string GroupName { get; private set; }

    }

    /// <summary>
    /// Contains execution status of an experiment.
    /// </summary>
    public class ExperimentStatus
    {
        public ExperimentStatus(ExperimentID id, string category, DateTime submitted, string creator, string note, bool flag, int done, int total, TimeSpan totalRuntime, string workerInformation)
        {
            ID = id;
            Category = category;
            Creator = creator;
            SubmissionTime = submitted;
            Note = note;
            Flag = flag;
            BenchmarksDone = done;
            BenchmarksTotal = total;
            TotalRuntime = totalRuntime;
            WorkerInformation = workerInformation;
        }

        public ExperimentID ID { get; private set; }

        public DateTime SubmissionTime;
        public string Creator;

        public string Category { get; private set; }

        /// <summary>
        /// A descriptive note, if you like.
        /// </summary>
        public string Note { get; set; }
        public bool Flag;

        public int BenchmarksDone { get; private set; }

        public int BenchmarksTotal { get; private set; }

        public int BenchmarksQueued { get { return BenchmarksTotal - BenchmarksDone; } }

        public TimeSpan TotalRuntime { get; private set; }

        public string WorkerInformation { get; private set; }
    }

    public enum ExperimentExecutionState
    {
        Active,
        Completed,
        Terminated,
        NotFound,
        Failed
    }


    /// <summary>
    /// Aka "Data".
    /// </summary>
    [Serializable]
    public class BenchmarkResult : ISerializable
    {
        public BenchmarkResult(int experimentId, string benchmarkFileName, DateTime acquireTime, double normalizedCPUTime,
            TimeSpan CPUTime, TimeSpan wallClockTime, double memorySizeMB, ResultStatus status, int? exitCode, Stream stdout, Stream stderr,
            IReadOnlyDictionary<string, string> props)
        {
            if (props == null) throw new ArgumentNullException("props");

            this.ExperimentID = experimentId;
            this.BenchmarkFileName = benchmarkFileName;
            this.NormalizedCPUTime = normalizedCPUTime;
            this.CPUTime = CPUTime;
            this.WallClockTime = wallClockTime;
            this.PeakMemorySizeMB = memorySizeMB;
            this.ExitCode = exitCode;
            this.AcquireTime = acquireTime;
            this.Status = status;
            this.Properties = props;
            this.StdOut = stdout;
            this.StdErr = stderr;
        }

        public BenchmarkResult(SerializationInfo info, StreamingContext context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case nameof(ExperimentID):
                        ExperimentID = (int)entry.Value; break;
                    case nameof(BenchmarkFileName):
                        BenchmarkFileName = (string)entry.Value; break;
                    case nameof(NormalizedCPUTime):
                    case "NormalizedRuntime":
                        NormalizedCPUTime = (double)entry.Value; break;
                    case nameof(CPUTime):
                    case "TotalProcessorTime":
                        CPUTime = (TimeSpan)entry.Value; break;
                    case nameof(WallClockTime):
                        WallClockTime = (TimeSpan)entry.Value; break;
                    case nameof(PeakMemorySizeMB):
                        PeakMemorySizeMB = (double)entry.Value; break;
                    case nameof(ExitCode):
                        ExitCode = (int)entry.Value; break;
                    case nameof(AcquireTime):
                        AcquireTime = (DateTime)entry.Value; break;
                    case nameof(Status):
                        Status = (ResultStatus)(int)entry.Value; break;
                    case nameof(Properties):
                        Properties = new ReadOnlyDictionary<string, string>((Dictionary<string, string>)entry.Value); break;
                    case nameof(StdOut):
                        StdOut = new MemoryStream((byte[])entry.Value); break;
                    case nameof(StdErr):
                        StdErr = new MemoryStream((byte[])entry.Value); break;
                }
            }
        }

        /// <summary>
        /// An experiment this benchmark is part of.
        /// </summary>
        public int ExperimentID { get; private set; }

        /// <summary>
        /// Name of a file that is passed as an argument to the target executable.
        /// </summary>
        /// <example>smtlib-latest\sample\z3.01234.smt2</example>
        public string BenchmarkFileName { get; private set; }

        public DateTime AcquireTime { get; private set; }

        public ResultStatus Status { get; private set; }

        /// <summary>
        /// A normalized total processor time that indicates the amount of time that the associated process has spent utilizing the CPU.
        /// </summary>
        public double NormalizedCPUTime { get; private set; }

        public TimeSpan CPUTime { get; private set; }

        public TimeSpan WallClockTime { get; private set; }

        /// <summary>
        /// Gets the maximum amount of virtual memory, in Mega Bytes, allocated for the process.
        /// </summary>
        public double PeakMemorySizeMB { get; private set; }

        /// <summary>
        /// Returns process exit code, if status is not either memory out nor time out;
        /// otherwise, returns null.
        /// </summary>
        public int? ExitCode { get; private set; }

        public Stream StdOut { get; private set; }

        public Stream StdErr { get; private set; }

        /// <summary>
        /// Domain-specific properties of the result.
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties { get; private set; }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (!this.StdOut.CanSeek || !this.StdErr.CanSeek)
                throw new InvalidOperationException("Can't serialize BenchmarkResult with non-seekable stream(s).");

            info.AddValue(nameof(ExperimentID), this.ExperimentID);
            info.AddValue(nameof(BenchmarkFileName), this.BenchmarkFileName, typeof(string));
            info.AddValue(nameof(NormalizedCPUTime), this.NormalizedCPUTime);
            info.AddValue(nameof(CPUTime), this.CPUTime, typeof(TimeSpan));
            info.AddValue(nameof(WallClockTime), this.WallClockTime, typeof(TimeSpan));
            info.AddValue(nameof(PeakMemorySizeMB), this.PeakMemorySizeMB);
            if(this.ExitCode.HasValue) info.AddValue(nameof(ExitCode), this.ExitCode);
            info.AddValue(nameof(AcquireTime), this.AcquireTime);
            info.AddValue(nameof(Status), (int)this.Status);
            var props = new Dictionary<string, string>(this.Properties.Count);
            foreach (var prop in this.Properties)
                props.Add(prop.Key, prop.Value);
            info.AddValue(nameof(Properties), props, typeof(Dictionary<string, string>));
            info.AddValue(nameof(StdOut), StreamToByteArray(this.StdOut), typeof(byte[]));
            info.AddValue(nameof(StdErr), StreamToByteArray(this.StdErr), typeof(byte[]));
        }

        private static byte[] StreamToByteArray(Stream stream)
        {
            if (stream is MemoryStream)
            {
                return ((MemoryStream)stream).ToArray();
            }
            else
            {
                if (!stream.CanSeek)
                    throw new ArgumentException("Non-seekable stream");
                var pos = stream.Position;
                stream.Seek(0, SeekOrigin.Begin);
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    stream.Seek(pos, SeekOrigin.Begin);
                    return ms.ToArray();
                }
            }
        }
    }

}
