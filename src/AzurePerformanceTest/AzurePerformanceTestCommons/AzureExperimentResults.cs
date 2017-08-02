using Measurement;
using PerformanceTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AzurePerformanceTest.AzureExperimentStorage;

namespace AzurePerformanceTest
{
    public class AzureExperimentResults : ExperimentResults
    {
        /// <summary>Etag of the blob which contained the given result. Null means that the blob didn't exist and no results.</summary>
        private readonly string etag;
        private readonly int expId;
        private readonly Dictionary<BenchmarkResult, AzureBenchmarkResult> externalOutputs;
        private readonly AzureExperimentStorage storage;

        public AzureExperimentResults(AzureExperimentStorage storage, int expId, AzureBenchmarkResult[] results, string etag) : base(expId, Parse(results, storage))
        {
            this.expId = expId;
            this.storage = storage;
            this.etag = etag;

            var benchmarks = Benchmarks;
            externalOutputs = new Dictionary<BenchmarkResult, AzureBenchmarkResult>();
            for (int i = 0; i < results.Length; i++)
            {
                var r = results[i];
                if (!string.IsNullOrEmpty(r.StdOutExtStorageIdx) || !string.IsNullOrEmpty(r.StdErrExtStorageIdx))
                    externalOutputs.Add(benchmarks[i], r);
            }
        }

        public override async Task<bool> TryDelete(IEnumerable<BenchmarkResult> toRemove)
        {
            if (toRemove == null) throw new ArgumentNullException(nameof(toRemove));

            var benchmarks = Benchmarks;
            var removeSet = new HashSet<BenchmarkResult>(toRemove);
            if (removeSet.Count == 0) return true;

            int n = benchmarks.Length;
            List<AzureBenchmarkResult> newAzureResults = new List<AzureBenchmarkResult>(n);
            List<BenchmarkResult> newResults = new List<BenchmarkResult>(n);
            List<AzureBenchmarkResult> deleteOuts = new List<AzureBenchmarkResult>();
            for (int i = 0; i < n; i++)
            {
                var b = benchmarks[i];
                if (!removeSet.Contains(b)) // remains
                {
                    var azureResult = AzureExperimentStorage.ToAzureBenchmarkResult(b);
                    newAzureResults.Add(azureResult);
                    newResults.Add(b);
                }
                else // to be removed
                {
                    removeSet.Remove(b);

                    AzureBenchmarkResult ar;
                    if (externalOutputs.TryGetValue(b, out ar))
                    {
                        deleteOuts.Add(ar);
                    }
                }
            }
            if (removeSet.Count != 0) throw new ArgumentException("Some of the given results to remove do not belong to the experiment results");

            // Updating blob with results table
            bool success = await Upload(newAzureResults.ToArray());
            if (!success) return false;

            // Update benchmarks array
            Replace(newResults.ToArray());

            // Deleting blobs with output
            foreach (var ar in deleteOuts)
            {
                try
                {
                    var _ = storage.DeleteOutputs(ar);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(string.Format("Exception when deleting output: {0}", ex));
                }
            }

            return true;
        }

        public override async Task<Dictionary<BenchmarkResult, BenchmarkResult>> TryUpdateStatus(IEnumerable<BenchmarkResult> toModify, ResultStatus status)
        {
            if (toModify == null) throw new ArgumentNullException(nameof(toModify));

            var mod = new Dictionary<BenchmarkResult, BenchmarkResult>();
            foreach (var oldRes in toModify) mod.Add(oldRes, null);
            if (mod.Count == 0) return mod;

            int n = Benchmarks.Length;
            var newBenchmarks = (BenchmarkResult[])Benchmarks.Clone();
            var newAzureBenchmarks = new AzureBenchmarkResult[n];

            for (int i = 0; i < n; i++)
            {
                var b = newBenchmarks[i];
                AzureBenchmarkResult azureResult;

                if (mod.ContainsKey(b))
                {
                    if (b.Status != status) // updating status of this result
                    {
                        newBenchmarks[i] = new BenchmarkResult(b.ExperimentID, b.BenchmarkFileName,
                            b.AcquireTime, b.NormalizedRuntime, b.TotalProcessorTime, b.WallClockTime,
                            b.PeakMemorySizeMB,
                            status, // <-- new status
                            b.ExitCode, b.StdOut, b.StdErr, b.Properties);

                        azureResult = AzureExperimentStorage.ToAzureBenchmarkResult(newBenchmarks[i]);
                        mod[b] = newBenchmarks[i];
                    }
                    else // status is as required already
                    {
                        azureResult = AzureExperimentStorage.ToAzureBenchmarkResult(b);
                        mod.Remove(b);
                    }
                }
                else // result doesn't change
                {
                    azureResult = AzureExperimentStorage.ToAzureBenchmarkResult(b);
                }

                AzureBenchmarkResult ar;
                if (externalOutputs.TryGetValue(b, out ar))
                {
                    azureResult.StdOut = ar.StdOut;
                    azureResult.StdOutExtStorageIdx = ar.StdOutExtStorageIdx;

                    azureResult.StdErr = ar.StdErr;
                    azureResult.StdErrExtStorageIdx = ar.StdErrExtStorageIdx;
                }
                else
                {
                    b.StdOut.Seek(0, System.IO.SeekOrigin.Begin);
                    azureResult.StdOut = Utils.StreamToString(b.StdOut, true);
                    azureResult.StdOutExtStorageIdx = string.Empty;

                    b.StdErr.Seek(0, System.IO.SeekOrigin.Begin);
                    azureResult.StdErr = Utils.StreamToString(b.StdErr, true);
                    azureResult.StdErrExtStorageIdx = string.Empty;
                }

                newAzureBenchmarks[i] = azureResult;
            }

            if (mod.Count == 0) return new Dictionary<BenchmarkResult, BenchmarkResult>(); // no changes
            foreach (var item in mod)
                if (item.Value == null) throw new ArgumentException("Some of the given results to update do not belong to the experiment results");

            bool success = await Upload(newAzureBenchmarks);
            if (!success) return null;

            // Update benchmarks array
            Replace(newBenchmarks.ToArray());

            return mod;
        }

        private async Task<bool> Upload(AzureBenchmarkResult[] newAzureBenchmarks)
        {
            bool success;
            if (etag != null) // blob already exists
                success = await storage.PutAzureExperimentResults(expId, newAzureBenchmarks, UploadBlobMode.ReplaceExact, etag);
            else // blob didn't exist
                success = await storage.PutAzureExperimentResults(expId, newAzureBenchmarks, UploadBlobMode.CreateNew);
            return success;
        }

        private static BenchmarkResult[] Parse(AzureBenchmarkResult[] results, AzureExperimentStorage storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (results == null) throw new ArgumentNullException(nameof(results));

            int n = results.Length;
            BenchmarkResult[] benchmarks = new BenchmarkResult[n];
            for (int i = 0; i < n; i++)
            {
                benchmarks[i] = storage.ParseAzureBenchmarkResult(results[i]);
            }
            return benchmarks;
        }
    }
}
