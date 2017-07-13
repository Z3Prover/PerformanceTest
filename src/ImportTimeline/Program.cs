using AzurePerformanceTest;
using Measurement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Z3Data;

namespace ImportTimeline
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("ImportTimeline.exe <path-to-data> <storage connection string>");
                return;
            }

            string pathToData = args[0];
            string connectionString = args[1];

            AzureExperimentStorage storage = null;
            try
            {
                storage = new AzureExperimentStorage(connectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to connect to the storage: " + ex.Message);
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();


            Console.Write("Reading experiments table from {0}... ", pathToData);
            var experiments = PrepareExperiments(pathToData, storage);
            Console.WriteLine("{0} experiments found.", experiments.Count);


            Console.WriteLine("\nUploading results tables...");
            var experimentInfo = UploadResults(pathToData, experiments, storage);
            //var experimentInfo = AddStatisticsNoUpload(pathToData, experiments, storage);

            Console.Write("\nUploading experiments table from... ");
            UploadExperiments(experiments, experimentInfo, storage);


            Console.WriteLine("\nUpdating timeline...");
            UpdateTimeline(new AzureSummaryManager(connectionString, new PerformanceTest.MEFDomainResolver()),
                experiments.Select(e => e.Key).ToArray()).Wait();

            sw.Stop();

            Console.WriteLine("Done, total time is {0}", sw.Elapsed);
        }

        private static async Task UpdateTimeline(AzureSummaryManager azureSummaryManager, int[] experiments)
        {
            await azureSummaryManager.Update("Z3Nightly", experiments);
            Console.WriteLine("Done");
        }

        private static void UploadExperiments(ConcurrentDictionary<int, ExperimentEntity> experiments, IDictionary<int, TimeSpan> experimentInfo, AzureExperimentStorage storage)
        {
            storage.ImportExperiments(experiments.Select(e => e.Value)).Wait();
        }

        static ConcurrentDictionary<int, ExperimentEntity> PrepareExperiments(string pathToData, AzureExperimentStorage storage)
        {
            var experiments = new ConcurrentDictionary<int, ExperimentEntity>(Environment.ProcessorCount, 10000);
            Directory.EnumerateFiles(pathToData, "*_meta.csv")
            .AsParallel()
            .ForAll(file =>
            {
                var metadata = new MetaData(file);
                var exp = new ExperimentEntity((int)metadata.Id);
                exp.Submitted = metadata.SubmissionTime;
                exp.BenchmarkContainerUri = PerformanceTest.ExperimentDefinition.LocalDiskContainerUri;
                exp.BenchmarkDirectory = metadata.BaseDirectory;
                exp.DomainName = "Z3";
                exp.BenchmarkFileExtension = "smt2";
                exp.Category = "smtlib-latest";
                exp.Executable = metadata.BinaryId.ToString();
                exp.Parameters = metadata.Parameters;
                exp.BenchmarkTimeout = metadata.Timeout;
                exp.MemoryLimitMB = metadata.Memoryout / 1024.0 / 1024.0;

                exp.Flag = false;
                exp.Creator = "Imported from Nightly data";
                exp.ExperimentTimeout = 0;
                exp.GroupName = "";

                exp.Note = String.Format("Cluster: {0}, cluster job id: {1}, node group: {2}, locality: {3}, finished: {4}, reference: {5}",
                    metadata.Cluster, metadata.ClusterJobId, metadata.Nodegroup, metadata.Locality, metadata.isFinished, metadata.Reference);

                experiments.TryAdd((int)metadata.Id, exp);
            });
            return experiments;
        }

        static IDictionary<int, TimeSpan> UploadResults(string pathToData, ConcurrentDictionary<int, ExperimentEntity> experiments, AzureExperimentStorage storage)
        {
            List<int> missingExperiments = new List<int>();
            ConcurrentDictionary<string, string> uploadedOutputs = new ConcurrentDictionary<string, string>();
            ConcurrentDictionary<int, TimeSpan> experimentInfo = new ConcurrentDictionary<int, TimeSpan>();

            var upload =
                Directory.EnumerateFiles(pathToData, "*.zip")
                .AsParallel()
                .Select(async file =>
                {
                    int expId = int.Parse(Path.GetFileNameWithoutExtension(file));
                    Console.WriteLine("Uploading experiment {0}...", expId);

                    ExperimentEntity e;
                    if (!experiments.TryGetValue(expId, out e))
                    {
                        missingExperiments.Add(expId);
                        Console.WriteLine("Experiment {0} has results but not metadata");
                        return 0;
                    }

                    CSVData table = new CSVData(file, (uint)expId);
                    var entities =
                        table.Rows
                        .OrderBy(r => r.Filename)
                        .Select(r => PrepareBenchmarkResult(r, storage, uploadedOutputs, expId, e.Submitted))
                        .ToArray();

                    var totalRunTime = table.Rows.Sum(r => r.Runtime);
                    e.TotalRuntime = totalRunTime;
                    e.CompletedBenchmarks = e.TotalBenchmarks = table.Rows.Count;

                    try
                    {
                        await storage.PutAzureExperimentResults(expId, entities, AzureExperimentStorage.UploadBlobMode.CreateOrReplace);
                        Console.WriteLine("Done uploading results for {0}.", expId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to upload experiment results of {0}: {1}", expId, ex.ToString());
                    }
                    return 0;
                });

            Task.WhenAll(upload).Wait();
            Console.WriteLine("Done (uploaded {0} output & error blobs).", uploadedOutputs.Count);

            if (missingExperiments.Count > 0)
            {
                Console.WriteLine("\nFollowing experiments have results but not metadata:");
                foreach (var item in missingExperiments)
                {
                    Console.WriteLine(item);
                }
            }

            return experimentInfo;
        }

        static IDictionary<int, TimeSpan> AddStatisticsNoUpload(string pathToData, ConcurrentDictionary<int, ExperimentEntity> experiments, AzureExperimentStorage storage)
        {
            List<int> missingExperiments = new List<int>();
            ConcurrentDictionary<int, TimeSpan> experimentInfo = new ConcurrentDictionary<int, TimeSpan>();

            Directory.EnumerateFiles(pathToData, "*.zip")
            .AsParallel()
            .ForAll(file =>
            {
                int expId = int.Parse(Path.GetFileNameWithoutExtension(file));

                ExperimentEntity e;
                if (!experiments.TryGetValue(expId, out e))
                {
                    missingExperiments.Add(expId);
                    Console.WriteLine("Experiment {0} has results but not metadata");
                    return;
                }

                CSVData table = new CSVData(file, (uint)expId);
                var totalRunTime = table.Rows.Sum(r => r.Runtime);
                e.TotalRuntime = totalRunTime;
                e.CompletedBenchmarks = e.TotalBenchmarks = table.Rows.Count;
                Console.WriteLine("Done for {0}", expId);
            });


            if (missingExperiments.Count > 0)
            {
                Console.WriteLine("\nFollowing experiments have results but not metadata:");
                foreach (var item in missingExperiments)
                {
                    Console.WriteLine(item);
                }
            }

            return experimentInfo;
        }

        private static AzureBenchmarkResult PrepareBenchmarkResult(CSVRow r, AzureExperimentStorage storage, ConcurrentDictionary<string, string> uploadedOutputs, int expId, DateTime submittedTime)
        {
            var properties = new Dictionary<string, string>()
                            {
                                { "SAT", r.SAT.ToString() },
                                { "UNSAT", r.UNSAT.ToString() },
                                { "UNKNOWN", r.UNKNOWN.ToString() },
                                { "TargetSAT", r.TargetSAT.ToString() },
                                { "TargetUNSAT", r.TargetUNSAT.ToString() },
                                { "TargetUNKNOWN", r.TargetUNKNOWN.ToString() },
                            };


            var b = new AzureBenchmarkResult
            {
                AcquireTime = submittedTime,
                BenchmarkFileName = r.Filename.Replace('\\', '/'),
                ExitCode = r.ReturnValue,
                ExperimentID = expId,
                NormalizedRuntime = r.Runtime,
                PeakMemorySizeMB = 0,
                Properties = properties,
                Status = ResultCodeToStatus(r.ResultCode),
                StdErr = r.StdErr,
                StdOut = r.StdOut,
                StdErrExtStorageIdx = "",
                StdOutExtStorageIdx = "",
                TotalProcessorTime = TimeSpan.FromSeconds(r.Runtime),
                WallClockTime = TimeSpan.FromSeconds(r.Runtime),
            };
            return b;
        }

        private static ResultStatus ResultCodeToStatus(uint resultCode)
        {
            switch (resultCode)
            {
                case 0: return ResultStatus.Success;
                case 3: return ResultStatus.Bug;
                case 4: return ResultStatus.Error;
                case 5: return ResultStatus.Timeout;
                case 6: return ResultStatus.OutOfMemory;
                default: throw new ArgumentException("Unknown result code: " + resultCode);
            }
        }


        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            if (!String.IsNullOrEmpty(s))
            {
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(s);
                writer.Flush();
                stream.Position = 0;
            }
            return stream;
        }
    }
}
