using Measurement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Angara.Data;
using Angara.Data.DelimitedFile;
using Microsoft.FSharp.Core;
using PerformanceTest;
using System.Globalization;

namespace AzurePerformanceTest
{
    [Serializable]
    public class AzureBenchmarkResult : ISerializable
    {
        public AzureBenchmarkResult() { }

        /// <summary>
        /// An experiment this benchmark is part of.
        /// </summary>
        public int ExperimentID { get; set; }

        /// <summary>
        /// Name of a file that is passed as an argument to the target executable.
        /// </summary>
        /// <example>smtlib-latest\sample\z3.01234.smt2</example>
        public string BenchmarkFileName { get; set; }

        public DateTime AcquireTime { get; set; }

        public ResultStatus Status { get; set; }

        /// <summary>
        /// A normalized total processor time that indicates the amount of time that the associated process has spent utilizing the CPU.
        /// </summary>
        public double NormalizedRuntime { get; set; }

        public TimeSpan TotalProcessorTime { get; set; }

        public TimeSpan WallClockTime { get; set; }

        /// <summary>
        /// Gets the maximum amount of virtual memory, in Mega Bytes, allocated for the process.
        /// </summary>
        public double PeakMemorySizeMB { get; set; }

        public int? ExitCode { get; set; }

        public string StdOut { get; set; }

        public string StdErr { get; set; }

        public string StdOutExtStorageIdx { get; set; }

        public string StdErrExtStorageIdx { get; set; }

        /// <summary>
        /// Domain-specific properties of the result.
        /// </summary>
        public Dictionary<string, string> Properties { get; set; }

        public AzureBenchmarkResult(SerializationInfo info, StreamingContext context)
        {
            foreach (SerializationEntry entry in info)
            {
                switch (entry.Name)
                {
                    case nameof(ExperimentID):
                        ExperimentID = (int)entry.Value; break;
                    case nameof(BenchmarkFileName):
                        BenchmarkFileName = (string)entry.Value; break;                    
                    case nameof(NormalizedRuntime):
                        NormalizedRuntime = (double)entry.Value; break;
                    case nameof(TotalProcessorTime):
                        TotalProcessorTime = (TimeSpan)entry.Value; break;
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
                        Properties = (Dictionary<string, string>)entry.Value; break;
                    case nameof(StdOut):
                        StdOut = (string)entry.Value; break;
                    case nameof(StdErr):
                        StdErr = (string)entry.Value; break;
                    case nameof(StdOutExtStorageIdx):
                        StdOutExtStorageIdx = (string)entry.Value; break;
                    case nameof(StdErrExtStorageIdx):
                        StdErrExtStorageIdx = (string)entry.Value; break;
                }
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(ExperimentID), this.ExperimentID);
            info.AddValue(nameof(BenchmarkFileName), this.BenchmarkFileName, typeof(string));
            info.AddValue(nameof(NormalizedRuntime), this.NormalizedRuntime);
            info.AddValue(nameof(TotalProcessorTime), this.TotalProcessorTime, typeof(TimeSpan));
            info.AddValue(nameof(WallClockTime), this.WallClockTime, typeof(TimeSpan));
            info.AddValue(nameof(PeakMemorySizeMB), this.PeakMemorySizeMB);
            if (this.ExitCode.HasValue) info.AddValue(nameof(ExitCode), this.ExitCode);
            info.AddValue(nameof(AcquireTime), this.AcquireTime);
            info.AddValue(nameof(Status), (int)this.Status);            
            info.AddValue(nameof(Properties), this.Properties, typeof(Dictionary<string, string>));
            info.AddValue(nameof(StdOut), this.StdOut);
            info.AddValue(nameof(StdErr), this.StdErr);
            info.AddValue(nameof(StdOutExtStorageIdx), this.StdOutExtStorageIdx);
            info.AddValue(nameof(StdErrExtStorageIdx), this.StdErrExtStorageIdx);
        }

        public static void SaveBenchmarks(AzureBenchmarkResult[] benchmarks, Stream stream)
        {
            var length = FSharpOption<int>.Some(benchmarks.Length);
            List<Column> columns = new List<Column>()
            {
                Column.Create("BenchmarkFileName", benchmarks.Select(b => b.BenchmarkFileName), length),
                Column.Create("AcquireTime", benchmarks.Select(b => b.AcquireTime.ToUniversalTime().ToString(System.Globalization.CultureInfo.InvariantCulture)), length),
                Column.Create("NormalizedRuntime", benchmarks.Select(b => b.NormalizedRuntime), length),
                Column.Create("TotalProcessorTime", benchmarks.Select(b => b.TotalProcessorTime.TotalSeconds), length),
                Column.Create("WallClockTime", benchmarks.Select(b => b.WallClockTime.TotalSeconds), length),
                Column.Create("PeakMemorySizeMB", benchmarks.Select(b => b.PeakMemorySizeMB), length),
                Column.Create("Status", benchmarks.Select(b => StatusToString(b.Status)), length),
                Column.Create("ExitCode", benchmarks.Select(b => b.ExitCode.HasValue ? b.ExitCode.ToString() : null), length),
                Column.Create("StdOut", benchmarks.Select(b => b.StdOut), length),
                Column.Create("StdOutExtStorageIdx", benchmarks.Select(b => b.StdOutExtStorageIdx), length),
                Column.Create("StdErr", benchmarks.Select(b => b.StdErr), length),
                Column.Create("StdErrExtStorageIdx", benchmarks.Select(b => b.StdErrExtStorageIdx), length)
            };

            HashSet<string> props = new HashSet<string>();
            foreach (var b in benchmarks)
                foreach (var p in b.Properties.Keys)
                    props.Add(p);
            foreach (var p in props)
            {
                string key = p;
                Column c = Column.Create(key, benchmarks.Select(b =>
                {
                    string val = null;
                    b.Properties.TryGetValue(key, out val);
                    return val;
                }), length);
                columns.Add(c);
            }
            var table = Table.OfColumns(columns);
            table.SaveUTF8Bom(stream, new WriteSettings(Delimiter.Comma, true, true));
        }

        public static AzureBenchmarkResult[] LoadBenchmarks(int expId, Stream stream)
        {
            var table = Table.Load(new StreamReader(stream), new ReadSettings(Delimiter.Comma, true, true, FSharpOption<int>.None,
                FSharpOption<FSharpFunc<Tuple<int, string>, FSharpOption<Type>>>.Some(FSharpFunc<Tuple<int, string>, FSharpOption<Type>>.FromConverter(tuple => FSharpOption<Type>.Some(typeof(string))))));

            var fileName = table["BenchmarkFileName"].Rows.AsString;
            var acq = table["AcquireTime"].Rows.AsString;
            var norm = table["NormalizedRuntime"].Rows.AsString;
            var runtime = table["TotalProcessorTime"].Rows.AsString;
            var wctime = table["WallClockTime"].Rows.AsString;
            var mem = table["PeakMemorySizeMB"].Rows.AsString;
            var stat = table["Status"].Rows.AsString;
            var exitcode = table["ExitCode"].Rows.AsString;
            var stdout = table["StdOut"].Rows.AsString;
            var stdoutext = table["StdOutExtStorageIdx"].Rows.AsString;
            var stderr = table["StdErr"].Rows.AsString;
            var stderrext = table["StdErrExtStorageIdx"].Rows.AsString;

            var propColumns =
                (from c in table
                 where
                    c.Name != "BenchmarkFileName" &&
                    c.Name != "AcquireTime" &&
                    c.Name != "NormalizedRuntime" &&
                    c.Name != "TotalProcessorTime" &&
                    c.Name != "WallClockTime" &&
                    c.Name != "PeakMemorySizeMB" &&
                    c.Name != "Status" &&
                    c.Name != "ExitCode" &&
                    c.Name != "StdOut" &&
                    c.Name != "StdErr" &&
                    c.Name != "StdOutExtStorageIdx" &&
                    c.Name != "StdErrExtStorageIdx"
                 select Tuple.Create(c.Name, c.Rows.AsString))
                .ToArray();

            AzureBenchmarkResult[] results = new AzureBenchmarkResult[table.RowsCount];
            for (int i = 0; i < results.Length; i++)
            {
                Dictionary<string, string> props = new Dictionary<string, string>(propColumns.Length);
                foreach (var pc in propColumns)
                {
                    if (pc.Item2 != null)
                    {
                        props[pc.Item1] = pc.Item2[i];
                    }
                }

                results[i] = new AzureBenchmarkResult();
                results[i].AcquireTime = DateTime.Parse(acq[i], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                results[i].BenchmarkFileName = fileName[i];
                results[i].ExitCode = string.IsNullOrEmpty(exitcode[i]) ? null : (int?)int.Parse(exitcode[i], CultureInfo.InvariantCulture);
                results[i].ExperimentID = expId;
                results[i].NormalizedRuntime = double.Parse(norm[i], CultureInfo.InvariantCulture);
                results[i].PeakMemorySizeMB = double.Parse(mem[i], CultureInfo.InvariantCulture);
                results[i].Properties = props;
                results[i].Status = StatusFromString(stat[i]);
                results[i].StdErr = stderr[i];
                results[i].StdErrExtStorageIdx = stderrext[i];
                results[i].StdOut = stdout[i];
                results[i].StdOutExtStorageIdx = stdoutext[i];
                results[i].TotalProcessorTime = TimeSpan.FromSeconds(double.Parse(runtime[i], CultureInfo.InvariantCulture));
                results[i].WallClockTime = TimeSpan.FromSeconds(double.Parse(wctime[i], CultureInfo.InvariantCulture));
            }
            return results;
        }

        public static string StatusToString(ResultStatus status)
        {
            return status.ToString();
        }

        public static ResultStatus StatusFromString(string status)
        {
            return (ResultStatus)Enum.Parse(typeof(ResultStatus), status);
        }
    }
}
